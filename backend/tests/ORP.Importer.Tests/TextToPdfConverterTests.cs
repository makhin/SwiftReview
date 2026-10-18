using ORP.Infrastructure.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class TextToPdfConverterTests
{
    private readonly TextToPdfConverter converter = new();

    [Fact]
    public void PreservesColumnsLeadingSpacesAndBlankLines()
    {
        using var pdf = PdfDocument.Open(converter.Convert("A   X\n  B Y\n\nC   Z"));
        var letters = pdf.GetPage(1).Letters;
        var a = Find(letters, "A");
        var b = Find(letters, "B");
        var c = Find(letters, "C");
        var x = Find(letters, "X");
        var cell = (x.StartBaseLine.X - a.StartBaseLine.X) / 4;
        Close(a.StartBaseLine.X + 2 * cell, b.StartBaseLine.X);
        Close(a.StartBaseLine.X, c.StartBaseLine.X);
        Close(x.StartBaseLine.X, Find(letters, "Y").StartBaseLine.X);
        Close(x.StartBaseLine.X, Find(letters, "Z").StartBaseLine.X);
        Close(12, a.StartBaseLine.Y - b.StartBaseLine.Y);
        Close(24, b.StartBaseLine.Y - c.StartBaseLine.Y);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void PreservesNewlinesAndExpandsTabs(string newline)
    {
        using var pdf = PdfDocument.Open(converter.Convert($"A\tX{newline}ABCDE\tY"));
        var letters = pdf.GetPage(1).Letters;
        Close(Find(letters, "X").StartBaseLine.X, Find(letters, "Y").StartBaseLine.X);
        Close(12, Find(letters, "X").StartBaseLine.Y - Find(letters, "Y").StartBaseLine.Y);
    }

    [Fact]
    public void EmbedsUnicodeFontAndPreservesPlainText()
    {
        const string text = "Привет, мир! Zażółć <b>text</b> └──────┘";
        using var pdf = PdfDocument.Open(converter.Convert(text));
        Assert.Equal(text, pdf.GetPage(1).Text);
        Assert.All(pdf.GetPage(1).Letters, letter => Assert.Contains("DejaVuSansMono", letter.FontName));
    }

    [Fact]
    public void ShrinksWholeDocumentWithoutWrappingOrClipping()
    {
        using var pdf = PdfDocument.Open(converter.Convert("L" + new string(' ', 130) + "R\nS"));
        var page = pdf.GetPage(1);
        var left = Find(page.Letters, "L");
        var right = Find(page.Letters, "R");
        Close(left.StartBaseLine.Y, right.StartBaseLine.Y);
        Close(left.FontSize, Find(page.Letters, "S").FontSize);
        Assert.True(left.FontSize < 10);
        Assert.True(right.BoundingBox.Right <= page.Width - 36 + 0.1);
    }

    [Fact]
    public void PaginatesWithoutLosingOrDuplicatingText()
    {
        var rows = Enumerable.Range(1, 150).Select(i => $"ROW{i:D3}").ToArray();
        using var pdf = PdfDocument.Open(converter.Convert(string.Join("\n", rows)));
        Assert.Equal(3, pdf.NumberOfPages);
        Assert.Equal(string.Concat(rows), string.Concat(pdf.GetPages().Select(page => page.Text)));
        Assert.All(pdf.GetPages(), page => Assert.All(page.Letters, letter =>
        {
            Assert.InRange(letter.BoundingBox.Bottom, 36, page.Height - 36);
            Assert.InRange(letter.BoundingBox.Top, 36, page.Height - 36);
        }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyBodyProducesOneBlankPage(string? body)
    {
        using var pdf = PdfDocument.Open(converter.Convert(body));
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Empty(pdf.GetPage(1).Text);
    }

    [Fact]
    public void FormFeedsPreserveExplicitBlankPages()
    {
        using var pdf = PdfDocument.Open(converter.Convert("FIRST\f\fLAST\f"));
        Assert.Equal(3, pdf.NumberOfPages);
        Assert.Equal("FIRST", pdf.GetPage(1).Text);
        Assert.Empty(pdf.GetPage(2).Text);
        Assert.Equal("LAST", pdf.GetPage(3).Text);
    }

    private static Letter Find(IReadOnlyList<Letter> letters, string value) =>
        Assert.Single(letters, letter => letter.Value == value);

    private static void Close(double expected, double actual) =>
        Assert.InRange(actual, expected - 0.1, expected + 0.1);
}
