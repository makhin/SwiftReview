namespace ORP.Importer;

public sealed class SaaMessageDocument
{
    public SaaMessageHeader Header { get; }
    /// <summary>Text starting at line four, including its original line endings.</summary>
    public string Body { get; }
    public string FileName { get; }
    public string FilePath { get; }
    /// <summary>Lowercase SHA-256 of the entire original file, including any BOM.</summary>
    public string ContentHash { get; }

    internal SaaMessageDocument(SaaMessageHeader header, string body, string filePath, string contentHash)
    {
        Header = header;
        Body = body;
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        ContentHash = contentHash;
    }
}
