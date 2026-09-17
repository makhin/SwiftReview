using System.Drawing;
using System.Text;
using DevExpress.Drawing;
using DevExpress.Pdf;
using ORP.Application.Abstractions;

namespace ORP.Infrastructure.Documents;

public sealed class DevExpressTextToPdfConverter : ITextToPdfConverter
{
    private const string FontName = "DejaVu Sans Mono";
    private static readonly Lazy<bool> FontLoaded = new(() =>
    {
        using var stream = typeof(DevExpressTextToPdfConverter).Assembly.GetManifestResourceStream(
            "ORP.Infrastructure.Documents.Fonts.DejaVuSansMono.ttf")!;
        DXFontRepository.Instance.AddFont(stream);
        return true;
    });

    public byte[] Convert(string text, TextPdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new TextPdfOptions();
        Validate(options);
        _ = FontLoaded.Value;

        var sections = text.Split('\f').Select(section => ReadLines(section, options.TabSize)).ToList();
        // A terminal form feed finishes the last page rather than creating another one.
        if (sections.Count > 1 && sections[^1].Count == 0)
            sections.RemoveAt(sections.Count - 1);

        using var processor = new PdfDocumentProcessor();
        processor.CreateEmptyDocument();
        using var measuringGraphics = processor.CreateGraphicsWorldSystem(72, 72);
        var requestedFont = new DXFont(FontName, options.FontSize);
        var format = new PdfStringFormat
        {
            FormatFlags = PdfStringFormatFlags.NoWrap | PdfStringFormatFlags.MeasureTrailingSpaces
        };

        var width = options.PageWidth - 2 * options.Margin;
        var widest = sections.SelectMany(lines => lines)
            .Where(line => line.Length > 0)
            .Select(line => measuringGraphics.MeasureString(line, requestedFont, format).Width)
            .DefaultIfEmpty(0).Max();
        var scale = widest > width ? width / widest : 1;
        var font = new DXFont(FontName, options.FontSize * scale);
        using var brush = new DXSolidBrush(Color.Black);
        var textHeight = measuringGraphics.MeasureString("Mg", font, format).Height;
        var spacing = Math.Max(options.LineSpacing * scale, textHeight);
        var availableHeight = options.PageHeight - 2 * options.Margin;
        if (textHeight > availableHeight)
            throw new ArgumentException("The page must have room for at least one line of text.", nameof(options));
        var linesPerPage = (int)Math.Min(int.MaxValue, Math.Floor((availableHeight - textHeight) / spacing) + 1);

        foreach (var lines in sections)
        {
            // Even an empty section represents a page (including blank pages between form feeds).
            for (var start = 0; start < Math.Max(1, lines.Count); start += linesPerPage)
            {
                using var graphics = processor.CreateGraphicsWorldSystem(72, 72);
                var count = Math.Min(linesPerPage, lines.Count - start);
                for (var i = 0; i < count; i++)
                {
                    if (lines[start + i].Length > 0)
                        graphics.DrawString(lines[start + i], font, brush,
                            new PointF(options.Margin, options.Margin + i * spacing), format);
                }
                processor.RenderNewPage(new PdfRectangle(0, 0, options.PageWidth, options.PageHeight), graphics);
            }
        }

        using var output = new MemoryStream();
        processor.SaveDocument(output);
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
