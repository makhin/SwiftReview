using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ORP.Scheduler.Documents;

public sealed class ITextTextToPdfConverter : ITextToPdfConverter
{
    static ITextTextToPdfConverter()
    {
        // iText 5 uses a process-wide switch; avoid rounding fitted font sizes to two decimals.
        ByteBuffer.HIGH_PRECISION = true;
    }

    private static readonly Lazy<byte[]> FontData = new(() =>
    {
        using var stream = typeof(ITextTextToPdfConverter).Assembly.GetManifestResourceStream(
            "ORP.Scheduler.Documents.Fonts.DejaVuSansMono.ttf")!;
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    });

    public byte[] Convert(string text, TextPdfOptions? options = null)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        options ??= new TextPdfOptions();
        Validate(options);

        var sections = text.Split('\f').Select(section => ReadLines(section, options.TabSize)).ToList();
        // A terminal form feed finishes the last page rather than creating another one.
        if (sections.Count > 1 && sections[sections.Count - 1].Count == 0)
            sections.RemoveAt(sections.Count - 1);

        var font = BaseFont.CreateFont("DejaVuSansMono.ttf", BaseFont.IDENTITY_H,
            BaseFont.EMBEDDED, false, FontData.Value, null);
        var width = options.PageWidth - 2 * options.Margin;
        var widest = sections.SelectMany(lines => lines)
            .Where(line => line.Length > 0)
            .Select(line => font.GetWidthPoint(line, options.FontSize))
            .DefaultIfEmpty(0).Max();
        var scale = widest > width ? width / widest : 1;
        var fontSize = options.FontSize * scale;
        var ascent = font.GetFontDescriptor(BaseFont.ASCENT, fontSize);
        var descent = font.GetFontDescriptor(BaseFont.DESCENT, fontSize);
        // Include the actual glyph extents, including accents and box-drawing characters.
        foreach (var line in sections.SelectMany(lines => lines))
        {
            ascent = Math.Max(ascent, font.GetAscentPoint(line, fontSize));
            descent = Math.Min(descent, font.GetDescentPoint(line, fontSize));
        }
        var textHeight = ascent - descent;
        var spacing = Math.Max(options.LineSpacing * scale, textHeight);
        var availableHeight = options.PageHeight - 2 * options.Margin;
        if (textHeight > availableHeight)
            throw new ArgumentException("The page must have room for at least one line of text.", nameof(options));
        var linesPerPage = (int)Math.Min(int.MaxValue, Math.Floor((availableHeight - textHeight) / spacing) + 1);

        using var output = new MemoryStream();
        using (var document = new Document(new Rectangle(options.PageWidth, options.PageHeight)))
        {
            var writer = PdfWriter.GetInstance(document, output);
            writer.CloseStream = false;
            document.Open();
            foreach (var lines in sections)
            {
                // Even an empty section represents a page (including blank pages between form feeds).
                for (var start = 0; start < Math.Max(1, lines.Count); start += linesPerPage)
                {
                    document.NewPage();
                    var canvas = writer.DirectContent;
                    var count = Math.Min(linesPerPage, lines.Count - start);
                    canvas.BeginText();
                    canvas.SetFontAndSize(font, fontSize);
                    for (var i = 0; i < count; i++)
                    {
                        if (lines[start + i].Length > 0)
                        {
                            canvas.SetTextMatrix(options.Margin,
                                options.PageHeight - options.Margin - ascent - i * spacing);
                            canvas.ShowText(lines[start + i]);
                        }
                    }
                    canvas.EndText();
                    // iText 5 normally omits empty pages.
                    writer.PageEmpty = false;
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
            for (var index = 0; index < line.Length; index++)
            {
                if (line[index] == '\t')
                {
                    var spaces = tabSize - column % tabSize;
                    expanded.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    expanded.Append(line[index]);
                    if (char.IsHighSurrogate(line[index]) && index + 1 < line.Length && char.IsLowSurrogate(line[index + 1]))
                        expanded.Append(line[++index]);
                    column++;
                }
            }
            lines.Add(expanded.ToString());
        }
        return lines;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void Validate(TextPdfOptions options)
    {
        if (!IsFinite(options.PageWidth) || options.PageWidth <= 0 ||
            !IsFinite(options.PageHeight) || options.PageHeight <= 0 ||
            !IsFinite(options.Margin) || options.Margin < 0 ||
            options.Margin >= options.PageWidth / 2 || options.Margin >= options.PageHeight / 2 ||
            !IsFinite(options.FontSize) || options.FontSize <= 0 ||
            !IsFinite(options.LineSpacing) || options.LineSpacing < options.FontSize ||
            options.TabSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Specify finite positive page/font sizes, usable margins, line spacing >= font size, and a positive tab size.");
    }
}
