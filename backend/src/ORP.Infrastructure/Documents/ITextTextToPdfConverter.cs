using System.Text;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using ORP.Application.Abstractions;

namespace ORP.Infrastructure.Documents;

public sealed class ITextTextToPdfConverter : ITextToPdfConverter
{
    private static readonly Lazy<byte[]> FontData = new(() =>
    {
        using var stream = typeof(ITextTextToPdfConverter).Assembly.GetManifestResourceStream(
            "ORP.Infrastructure.Documents.Fonts.DejaVuSansMono.ttf")!;
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    });

    public byte[] Convert(string text, TextPdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new TextPdfOptions();
        Validate(options);

        var sections = text.Split('\f').Select(section => ReadLines(section, options.TabSize)).ToList();
        // A terminal form feed finishes the last page rather than creating another one.
        if (sections.Count > 1 && sections[^1].Count == 0)
            sections.RemoveAt(sections.Count - 1);

        // PdfFont belongs to one PDF document; only the immutable font bytes are shared.
        var font = PdfFontFactory.CreateFont(FontData.Value, PdfEncodings.IDENTITY_H,
            PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        var width = options.PageWidth - 2 * options.Margin;
        var widest = sections.SelectMany(lines => lines)
            .Where(line => line.Length > 0)
            .Select(line => font.GetWidth(line, options.FontSize))
            .DefaultIfEmpty(0).Max();
        var scale = widest > width ? width / widest : 1;
        var fontSize = options.FontSize * scale;
        var metrics = font.GetFontProgram().GetFontMetrics();
        var ascent = metrics.GetAscender() * fontSize / 1000;
        var descent = metrics.GetDescender() * fontSize / 1000;
        // Include the actual glyph extents, including accents and box-drawing characters.
        foreach (var line in sections.SelectMany(lines => lines))
        {
            ascent = Math.Max(ascent, font.GetAscent(line, fontSize));
            descent = Math.Min(descent, font.GetDescent(line, fontSize));
        }
        var textHeight = ascent - descent;
        var spacing = Math.Max(options.LineSpacing * scale, textHeight);
        var availableHeight = options.PageHeight - 2 * options.Margin;
        if (textHeight > availableHeight)
            throw new ArgumentException("The page must have room for at least one line of text.", nameof(options));
        var linesPerPage = (int)Math.Min(int.MaxValue, Math.Floor((availableHeight - textHeight) / spacing) + 1);

        using var output = new MemoryStream();
        using (var writer = new PdfWriter(output))
        {
            writer.SetCloseStream(false);
            using var document = new PdfDocument(writer);
            foreach (var lines in sections)
            {
                // Even an empty section represents a page (including blank pages between form feeds).
                for (var start = 0; start < Math.Max(1, lines.Count); start += linesPerPage)
                {
                    var page = document.AddNewPage(new PageSize(options.PageWidth, options.PageHeight));
                    var canvas = new PdfCanvas(page);
                    // Default two-decimal font-size rounding can push a fitted line past the margin.
                    canvas.GetContentStream().GetOutputStream().SetLocalHighPrecision(true);
                    var count = Math.Min(linesPerPage, lines.Count - start);
                    canvas.BeginText().SetFontAndSize(font, fontSize);
                    for (var i = 0; i < count; i++)
                    {
                        if (lines[start + i].Length > 0)
                            canvas.SetTextMatrix(1, 0, 0, 1, options.Margin,
                                    options.PageHeight - options.Margin - ascent - i * spacing)
                                .ShowText(lines[start + i]);
                    }
                    canvas.EndText();
                    canvas.Release();
                }
            }
        }
        return output.ToArray();
    }

    private static List<string> ReadLines(string text, int tabSize)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var expanded = new StringBuilder();
            var column = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                if (rune.Value == '\t')
                {
                    var spaces = tabSize - column % tabSize;
                    expanded.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    expanded.Append(rune.ToString());
                    column++;
                }
            }
            lines.Add(expanded.ToString());
        }
        return lines;
    }

    private static void Validate(TextPdfOptions options)
    {
        if (!float.IsFinite(options.PageWidth) || options.PageWidth <= 0 ||
            !float.IsFinite(options.PageHeight) || options.PageHeight <= 0 ||
            !float.IsFinite(options.Margin) || options.Margin < 0 ||
            options.Margin >= options.PageWidth / 2 || options.Margin >= options.PageHeight / 2 ||
            !float.IsFinite(options.FontSize) || options.FontSize <= 0 ||
            !float.IsFinite(options.LineSpacing) || options.LineSpacing < options.FontSize ||
            options.TabSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Specify finite positive page/font sizes, usable margins, line spacing >= font size, and a positive tab size.");
    }
}
