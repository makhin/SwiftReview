using System.Text;
using ORP.Scheduler.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xunit;

namespace ORP.Sheduler.Tests;

public sealed class TextToPdfConverterTests
{
    private readonly ITextToPdfConverter _converter = new ITextTextToPdfConverter();

    [Theory]
    [InlineData("columns.txt", 1, "ACCOUNT", "BETA")]
    [InlineData("tabs-unicode.txt", 1, "Zażółć", "└──────┘")]
    [InlineData("pages.txt", 2, "FIRST PAGE", "SECOND PAGE")]
    [InlineData("wide.txt", 1, "LEFT", "RIGHT")]
    public void ConvertsTextFilesToReadablePdf(string fileName, int pages, string first, string last)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName), Encoding.UTF8);
        var bytes = _converter.Convert(text);

        // Inspect the actual PDF with an independent reader, not a mock of the renderer.
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(pages, pdf.NumberOfPages);
        var extracted = string.Join("\n", pdf.GetPages().Select(page => page.Text));
        Assert.Contains(first, extracted);
        Assert.Contains(last, extracted);
        Assert.All(pdf.GetPages(), page => Assert.All(page.Letters, letter =>
        {
            Assert.InRange(letter.BoundingBox.Left, 0, page.Width);
            Assert.InRange(letter.BoundingBox.Right, 0, page.Width);
            Assert.InRange(letter.BoundingBox.Bottom, 0, page.Height);
            Assert.InRange(letter.BoundingBox.Top, 0, page.Height);
        }));
    }

    [Fact]
    public void PreservesLeadingSpacesColumnsAndBlankLines()
    {
        using var pdf = PdfDocument.Open(_converter.Convert("A   X\n  B Y\n\nC   Z"));
        var letters = pdf.GetPage(1).Letters;
        var a = Find(letters, "A");
        var b = Find(letters, "B");
        var c = Find(letters, "C");
        var x = Find(letters, "X");
        var y = Find(letters, "Y");
        var z = Find(letters, "Z");
        var cellWidth = (x.StartBaseLine.X - a.StartBaseLine.X) / 4;

        AssertClose(a.StartBaseLine.X + 2 * cellWidth, b.StartBaseLine.X);
        AssertClose(a.StartBaseLine.X, c.StartBaseLine.X);
        AssertClose(x.StartBaseLine.X, y.StartBaseLine.X);
        AssertClose(x.StartBaseLine.X, z.StartBaseLine.X);
        AssertClose(14, a.StartBaseLine.Y - b.StartBaseLine.Y);
        AssertClose(28, b.StartBaseLine.Y - c.StartBaseLine.Y);
        Assert.Contains("DejaVuSansMono", a.FontName);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void HandlesNewlinesAndTabStops(string newline)
    {
        using var pdf = PdfDocument.Open(_converter.Convert($"A\tX{newline}ABCDE\tY{newline}"));
        var letters = pdf.GetPage(1).Letters;
        AssertClose(Find(letters, "X").StartBaseLine.X, Find(letters, "Y").StartBaseLine.X);
        AssertClose(14, Find(letters, "X").StartBaseLine.Y - Find(letters, "Y").StartBaseLine.Y);
        Assert.Equal(1, pdf.NumberOfPages);
    }

    [Fact]
    public void FitsWideLinesWithoutWrappingOrClipping()
    {
        var options = new TextPdfOptions();
        using var pdf = PdfDocument.Open(_converter.Convert("L" + new string(' ', 130) + "R\nS", options));
        var page = pdf.GetPage(1);
        var left = Find(page.Letters, "L");
        var right = Find(page.Letters, "R");
        var shortLine = Find(page.Letters, "S");
        AssertClose(left.StartBaseLine.Y, right.StartBaseLine.Y);
        AssertClose(left.FontSize, shortLine.FontSize);
        Assert.True(left.FontSize < options.FontSize);
        Assert.True(right.BoundingBox.Right <= options.PageWidth - options.Margin + 0.1,
            $"Right edge {right.BoundingBox.Right} exceeds margin {options.PageWidth - options.Margin}; font size {left.FontSize}.");
    }

    [Fact]
    public void PaginatesWithoutLosingOrDuplicatingLines()
    {
        var text = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"ROW{i:D3}"));
        using var pdf = PdfDocument.Open(_converter.Convert(text));
        Assert.Equal(3, pdf.NumberOfPages);
        var extracted = string.Concat(pdf.GetPages().Select(page => page.Text));
        foreach (var i in Enumerable.Range(1, 150))
            Assert.Equal(1, extracted.Split(new[] { $"ROW{i:D3}" }, StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("A\f", 1)]
    [InlineData("A\f\fB", 3)]
    [InlineData("\fB", 2)]
    public void PreservesExplicitBlankPagesAndAvoidsExtraTrailingPage(string text, int pages)
    {
        using var pdf = PdfDocument.Open(_converter.Convert(text));
        Assert.Equal(pages, pdf.NumberOfPages);
    }

    [Fact]
    public void SupportsLandscapePageSettings()
    {
        using var pdf = PdfDocument.Open(_converter.Convert("Landscape", new TextPdfOptions
        {
            PageWidth = 841.89f,
            PageHeight = 595.28f
        }));
        AssertClose(841.89, pdf.GetPage(1).Width);
        AssertClose(595.28, pdf.GetPage(1).Height);
    }

    [Fact]
    public void RejectsNullTextAndInvalidLayout()
    {
        Assert.Throws<ArgumentNullException>(() => _converter.Convert(null!));
        foreach (var options in new[]
        {
            new TextPdfOptions { FontSize = 0 },
            new TextPdfOptions { PageWidth = float.NaN },
            new TextPdfOptions { PageHeight = float.PositiveInfinity },
            new TextPdfOptions { Margin = 300 },
            new TextPdfOptions { Margin = -1 },
            new TextPdfOptions { LineSpacing = 1 },
            new TextPdfOptions { TabSize = 0 }
        })
            Assert.Throws<ArgumentOutOfRangeException>(() => _converter.Convert("text", options));
        Assert.Throws<ArgumentException>(() => _converter.Convert("text", new TextPdfOptions { PageHeight = 73 }));
    }

    [Fact]
    public void ReusesConverterWithoutSharingDocumentState()
    {
        using var first = PdfDocument.Open(_converter.Convert("FIRST DOCUMENT"));
        using var second = PdfDocument.Open(_converter.Convert("SECOND DOCUMENT"));

        Assert.Equal(1, first.NumberOfPages);
        Assert.Equal(1, second.NumberOfPages);
        Assert.Equal("FIRST DOCUMENT", first.GetPage(1).Text);
        Assert.Equal("SECOND DOCUMENT", second.GetPage(1).Text);
    }

    [Fact]
    public void LibraryTargetsNetFramework472WithoutBackendDependencies()
    {
        var assembly = typeof(ITextTextToPdfConverter).Assembly;
        var framework = Assert.Single(assembly.GetCustomAttributes(
            typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false));
        Assert.Equal(".NETFramework,Version=v4.7.2",
            ((System.Runtime.Versioning.TargetFrameworkAttribute)framework).FrameworkName);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            reference.Name != null && reference.Name.StartsWith("ORP.", StringComparison.Ordinal));
    }

    private static Letter Find(IReadOnlyList<Letter> letters, string value) => Assert.Single(letters, l => l.Value == value);
    private static void AssertClose(double expected, double actual) => Assert.InRange(actual, expected - 0.1, expected + 0.1);
}
