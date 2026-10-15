namespace ORP.Infrastructure.Persistence;

public sealed class IgnoredImportFile
{
    public string FilePath { get; set; } = null!;
    public string ContentHash { get; set; } = null!;
    public DateTimeOffset IgnoredAtUtc { get; set; }
    public string Reason { get; set; } = null!;
}
