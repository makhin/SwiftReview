using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using SwiftReview.Application.Abstractions;

namespace SwiftReview.Infrastructure.Services;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public sealed class FakeAwhClient : IAwhClient
{
    public Task<IReadOnlyList<AwhMessage>> GetMessagesSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        IReadOnlyList<AwhMessage> messages = Enumerable.Range(1, 3).Select(i => new AwhMessage($"AWH-{since:yyyyMMddHH}-{i}",
            "MT199", 1, 1, DateTimeOffset.UtcNow, "FAKEBANK", "SWIFTREVIEW", $"FAKE-{i}", "EUR", 100m * i, $"AWHREF-{i}", $"{{1:F01FAKE{i}}}")).ToList();
        return Task.FromResult(messages);
    }
    public Task<AwhMessage?> GetMessageAsync(string externalId, CancellationToken ct) => Task.FromResult<AwhMessage?>(null);
}

public sealed class FakeDocumentStorage(ILogger<FakeDocumentStorage> logger) : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, byte> _processed = new();
    public Task StoreConfirmationAsync(long messageId, string content, string idempotencyKey, CancellationToken ct)
    {
        if (_processed.TryAdd(idempotencyKey, 0))
            logger.LogInformation("Fake document stored for message {MessageId}; length {Length}; key={IdempotencyKey}", messageId, content.Length, idempotencyKey);
        return Task.CompletedTask;
    }
}

public sealed class FakeNotificationSender(ILogger<FakeNotificationSender> logger) : INotificationSender
{
    private readonly ConcurrentDictionary<string, byte> _processed = new();
    public Task SendAsync(string recipient, string message, string eventName, string idempotencyKey, CancellationToken ct)
    {
        if (_processed.TryAdd(idempotencyKey, 0))
            logger.LogInformation("Fake notification: recipient={Recipient}, event={Event}, message={Message}, key={IdempotencyKey}", recipient, eventName, message, idempotencyKey);
        return Task.CompletedTask;
    }
}
