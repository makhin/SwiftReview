namespace ORP.Application.Abstractions;

/// <summary>Converts preformatted plain text to PDF without reflowing its lines.</summary>
public interface ITextToPdfConverter
{
    /// <summary>
    /// Preserves spaces and blank lines, expands tabs, and honors form feeds (\f).
    /// Decode file bytes to a string with the appropriate encoding before calling.
    /// Empty input produces one blank page. A final newline/form feed does not add a page.
    /// </summary>
    byte[] Convert(string text, TextPdfOptions? options = null);
}

/// <summary>Page dimensions, margins, font size and line spacing are in points (1/72 inch).</summary>
public sealed record TextPdfOptions
{
    public float PageWidth { get; init; } = 595.28f;
    public float PageHeight { get; init; } = 841.89f;
    public float Margin { get; init; } = 36;
    /// <summary>Maximum font size; reduced uniformly for the document to fit its widest line.</summary>
    public float FontSize { get; init; } = 10;
    /// <summary>Baseline spacing at FontSize; scales with the font when fitting wide lines.</summary>
    public float LineSpacing { get; init; } = 14;
    public int TabSize { get; init; } = 8;
}
