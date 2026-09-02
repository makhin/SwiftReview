using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwiftReview.Application.Abstractions;
using SwiftReview.Application.Messages.Import;
using SwiftReview.Domain.Outbox;
using SwiftReview.Infrastructure.Persistence;

namespace SwiftReview.Worker;

public sealed class WorkerCorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Value = new();
    public string CorrelationId => Value.Value ?? "worker";
    public static void Set(string value) => Value.Value = value;
    public static void Clear() => Value.Value = null;
}

public interface IRealtimeNotifier
{
    Task MessageChangedAsync(long id, int branchId, int departmentId,
        string idempotencyKey, CancellationToken ct);
}
public sealed class ApiRealtimeNotifier(HttpClient http, IConfiguration config) : IRealtimeNotifier
{
    public async Task MessageChangedAsync(long id, int branchId, int departmentId,
        string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/message-changed")
        { Content = JsonContent.Create(new { type = "MessageChanged", messageId = id, branchId, departmentId, eventId = idempotencyKey }) };
        request.Headers.Add("X-Internal-Key", config["InternalApiKey"]);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
    }
}

public sealed class OutboxWorker(IServiceScopeFactory scopes, IRealtimeNotifier realtime, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await ClaimAsync(stoppingToken);
                if (item is null) { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); continue; }
                await ProcessAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Outbox polling failed"); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task<OutboxMessage?> ClaimAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SwiftReviewDbContext>();
        var now = DateTimeOffset.UtcNow;
        var lockId = Guid.NewGuid();
        var lockedUntil = now.AddMinutes(5);
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            ;WITH candidate AS
            (
                SELECT TOP (1) *
                FROM [OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [ProcessedAt] IS NULL
                  AND ([LockedUntil] IS NULL OR [LockedUntil] < {now})
                  AND ([NextAttemptAt] IS NULL OR [NextAttemptAt] <= {now})
                  AND [Attempts] < 10
                ORDER BY [OccurredAt], [Id]
            )
            UPDATE candidate
            SET [LockId] = {lockId}, [LockedUntil] = {lockedUntil},
                [Attempts] = [Attempts] + 1, [NextAttemptAt] = NULL;", ct);
        return await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.LockId == lockId, ct);
    }

    private async Task ProcessAsync(OutboxMessage claimed, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SwiftReviewDbContext>();
        var lockId = claimed.LockId ?? throw new InvalidOperationException("Claimed outbox item has no lease owner.");
        var item = await db.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == claimed.Id && x.LockId == lockId && x.ProcessedAt == null, ct);
        if (item is null) return;
        WorkerCorrelationContext.Set(item.CorrelationId);
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = item.CorrelationId, ["OutboxId"] = item.Id });
        try
        {
            using var json = JsonDocument.Parse(item.PayloadJson);
            var root = json.RootElement;
            Domain.Messages.Message? message = null;
            if (root.TryGetProperty("messageId", out var messageId)) message = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == messageId.GetInt64(), ct);
            else if (root.TryGetProperty("ExternalId", out var externalId)) message = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x => x.ExternalId == externalId.GetString(), ct);
            if (message is not null)
            {
                var idempotencyKey = $"outbox:{item.Id}";
                await realtime.MessageChangedAsync(message.Id, message.BranchId,
                    message.OwningDepartmentId, idempotencyKey, ct);
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationSender>();
                await notifications.SendAsync(message.CurrentAssigneeId?.ToString() ?? "operations",
                    $"Message {message.ExternalId} changed", item.Type, idempotencyKey, ct);
                if (item.Type == "MessageCompleted")
                    await scope.ServiceProvider.GetRequiredService<IDocumentStorage>()
                        .StoreConfirmationAsync(message.Id, item.PayloadJson, idempotencyKey, ct);
            }
            var completed = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [OutboxMessages]
                SET [ProcessedAt] = {DateTimeOffset.UtcNow}, [LockedUntil] = NULL,
                    [LockId] = NULL, [NextAttemptAt] = NULL, [LastError] = NULL
                WHERE [Id] = {item.Id} AND [LockId] = {lockId} AND [ProcessedAt] IS NULL;", ct);
            if (completed != 1) logger.LogWarning("Outbox event {OutboxId} lost its lease before completion", item.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox event {OutboxId} attempt {Attempt} failed", item.Id, item.Attempts);
            var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(item.Attempts, 8)));
            var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            var error = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [OutboxMessages]
                SET [LastError] = {error}, [LockedUntil] = NULL, [LockId] = NULL,
                    [NextAttemptAt] = {nextAttemptAt}
                WHERE [Id] = {item.Id} AND [LockId] = {lockId} AND [ProcessedAt] IS NULL;", ct);
        }
        finally { WorkerCorrelationContext.Clear(); }
    }
}

public sealed class AwhIngestionWorker(IServiceScopeFactory scopes, IAwhClient awh, ILogger<AwhIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var item in await awh.GetMessagesSinceAsync(DateTimeOffset.UtcNow.AddMinutes(-5), stoppingToken))
                {
                    await using var scope = scopes.CreateAsyncScope(); WorkerCorrelationContext.Set(Guid.NewGuid().ToString("N"));
                    try
                    {
                        var result = await scope.ServiceProvider.GetRequiredService<ImportMessageHandler>().HandleAsync(new ImportMessageRequest(item.ExternalId, item.MessageType,
                            item.BranchId, item.DepartmentId, item.ReceivedAt, item.Sender, item.Receiver, item.Account, item.Currency,
                            item.Amount, item.Reference, item.RawContent), stoppingToken);
                        logger.LogInformation("AWH message {ExternalId}: created={Created}", item.ExternalId, result.Created);
                    }
                    finally { WorkerCorrelationContext.Clear(); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "AWH ingestion cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
