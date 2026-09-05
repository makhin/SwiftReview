using Microsoft.Extensions.Options;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Automatic;

namespace ORP.Api.Infrastructure;

public sealed class AutomaticAssignmentOptions
{
    public const string SectionName = "AutoAssignment";
    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 100;
}

public sealed class AutomaticAssignmentWorker(IServiceScopeFactory scopeFactory,
    IOptions<AutomaticAssignmentOptions> options, ILogger<AutomaticAssignmentWorker> logger)
    : BackgroundService
{
    private readonly AutomaticAssignmentOptions _options = options.Value;
    private UnassignedMessageCursor? _cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Automatic-assignment batch failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested);
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<UnassignedMessageCursor> messages;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentQueries>();
            messages = await queries.GetUnassignedMessagesAsync(_cursor, _options.BatchSize,
                cancellationToken);
            if (messages.Count == 0 && _cursor is not null)
            {
                _cursor = null;
                messages = await queries.GetUnassignedMessagesAsync(null, _options.BatchSize,
                    cancellationToken);
            }
        }

        if (messages.Count > 0) _cursor = messages[^1];

        var assigned = 0;
        var correlationId = $"auto-assignment-{Guid.NewGuid():N}";
        foreach (var message in messages)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<AssignNewMessageHandler>()
                        .HandleAsync(message.MessageId, correlationId, cancellationToken))
                    assigned++;
            }
            catch (ConcurrentUpdateException exception)
            {
                logger.LogWarning(exception, "Automatic assignment lost a concurrent update for message {MessageId}",
                    message.MessageId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Automatic assignment failed for message {MessageId}", message.MessageId);
            }
        }

        if (messages.Count > 0)
            logger.LogInformation("Automatic assignment processed {MessageCount} messages and assigned {AssignedCount}",
                messages.Count, assigned);
    }
}
