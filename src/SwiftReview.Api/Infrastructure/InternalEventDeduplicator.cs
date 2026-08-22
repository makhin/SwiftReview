using System.Collections.Concurrent;

namespace SwiftReview.Api.Infrastructure;

public sealed class InternalEventDeduplicator
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processed = new();

    public bool TryBegin(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        var now = DateTimeOffset.UtcNow;
        if (!_processed.TryAdd(eventId, now)) return false;
        if (_processed.Count > 10_000)
            foreach (var expired in _processed.Where(x => x.Value < now.AddHours(-1)).Select(x => x.Key))
                _processed.TryRemove(expired, out _);
        return true;
    }
}
