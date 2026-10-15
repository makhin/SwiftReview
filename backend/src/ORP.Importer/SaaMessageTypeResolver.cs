namespace ORP.Importer;

public interface ISaaMessageTypeResolver
{
    string? ResolveMessageType(string messageType);
}

/// <summary>Matches complete message types against the application's explicit allowlist.</summary>
public sealed class SaaMessageTypeResolver : ISaaMessageTypeResolver
{
    private readonly Dictionary<string, string> allowedTypes = new(StringComparer.OrdinalIgnoreCase);

    public SaaMessageTypeResolver(IEnumerable<string> allowedMessageTypes)
    {
        if (allowedMessageTypes is null) throw new ArgumentNullException(nameof(allowedMessageTypes));
        foreach (var messageType in allowedMessageTypes)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("Allowed message types must not be empty.", nameof(allowedMessageTypes));
            var canonical = messageType.Trim();
            if (!allowedTypes.ContainsKey(canonical))
                allowedTypes.Add(canonical, canonical);
        }
    }

    public string? ResolveMessageType(string messageType) =>
        !string.IsNullOrWhiteSpace(messageType) && allowedTypes.TryGetValue(messageType.Trim(), out var canonical)
            ? canonical
            : null;
}
