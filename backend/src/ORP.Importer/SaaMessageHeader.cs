namespace ORP.Importer;

public enum SaaMessageDirection
{
    Incoming = 1,
    Outgoing = 2
}

public sealed class SaaMessageHeader
{
    public string Branch { get; internal set; } = null!;
    public string MessageType { get; internal set; } = null!;
    public string? ReferenceId { get; internal set; }
    public SaaMessageDirection? Direction { get; internal set; }
    public string? Sender { get; internal set; }
    public string? Receiver { get; internal set; }
    public string? Host { get; internal set; }
    public DateTime Time { get; internal set; }
}
