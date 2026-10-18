using System.Text;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;

namespace ORP.Infrastructure.Documents;

public sealed class TextToPdfConverter
{
    private const float Margin = 36;
    private static readonly Lazy<byte[]> FontData = new(() =>
    {
        using var stream = typeof(TextToPdfConverter).Assembly.GetManifestResourceStream(
            "ORP.Infrastructure.Documents.Fonts.DejaVuSansMono.ttf")!;
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    });

    public byte[] Convert(string? body)
    {
        var sections = (body ?? "").Split('\f').Select(ReadLines).ToList();
        // A terminal form feed finishes the page without adding another blank one.
        if (sections.Count > 1 && sections[^1].Count == 0)
            sections.RemoveAt(sections.Count - 1);

        using var output = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfWriter(output)))
        {
            var font = PdfFontFactory.CreateFont(FontData.Value, PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
            var pageSize = PageSize.A4;
            var widest = sections.SelectMany(lines => lines)
                .Select(line => font.GetWidth(line, 10)).DefaultIfEmpty(0).Max();
            var scale = widest > pageSize.GetWidth() - 2 * Margin
                ? (pageSize.GetWidth() - 2 * Margin) / widest : 1;
            var fontSize = 10 * scale;
            var spacing = 12 * scale;
            var descent = sections.SelectMany(lines => lines)
                .Select(line => font.GetDescent(line, fontSize)).DefaultIfEmpty(0).Min();
            var linesPerPage = (int)Math.Min(int.MaxValue,
                Math.Floor((pageSize.GetHeight() - 2 * Margin - fontSize + descent) / spacing) + 1);

            foreach (var lines in sections)
            {
                for (var start = 0; start < Math.Max(1, lines.Count); start += linesPerPage)
                {
                    var canvas = new PdfCanvas(pdf.AddNewPage(pageSize));
                    canvas.BeginText().SetFontAndSize(font, fontSize);
                    for (var i = 0; i < Math.Min(linesPerPage, lines.Count - start); i++)
                    {
                        canvas.SetTextMatrix(Margin, pageSize.GetHeight() - Margin - fontSize - i * spacing);
                        canvas.ShowText(lines[start + i]);
                    }
                    canvas.EndText();
                }
            }
        }
        return output.ToArray();
    }

    private static List<string> ReadLines(string text)
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
                    var spaces = 8 - column % 8;
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
}
