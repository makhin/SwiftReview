namespace ORP.Importer;

public sealed class SaaMessageFormatException : FormatException
{
    public string FilePath { get; }
    public int? LineNumber { get; }
    public string Reason { get; }

    internal SaaMessageFormatException(string filePath, int? lineNumber, string reason, Exception? innerException = null)
        : base($"Invalid SAA file '{filePath}'" + (lineNumber.HasValue ? $", line {lineNumber}" : "") + $": {reason}", innerException)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
        Reason = reason;
    }
}
