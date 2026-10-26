using System.Text;
using System.Text.Json;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageDocumentTests
{
    private const string PrintLine = "2026/10/14-04:35:30          PRMXSwiftPrt-2614-002614              1";
    private const string Separator = "____________________________________________________________________";
    private const string Section = "     ---------------------  Instance Type and Transmission --------------";
    private const string Content = "       Copy received from APPLI\n       Text: café, Пример\n\n";
    private static readonly string[] Lines = { SaaMessageParserTests.Header, PrintLine, Separator, "", Section, Content };
    private readonly SaaMessageParser parser = new();

    [Theory]
    [MemberData(nameof(SaaMessageParserTests.SampleHeaders), MemberType = typeof(SaaMessageParserTests))]
    public void AcceptsAllSampleEnvelopes(string sourceFile, string json)
    {
        using var fixture = JsonDocument.Parse(json);
        var row = fixture.RootElement;
        var text = string.Join("\n", row.GetProperty("Line").GetString(), row.GetProperty("PrintLine").GetString(), Separator, "", Section, Content);
        WithFile(Encoding.UTF8.GetBytes(text), path =>
        {
            if (row.GetProperty("Direction").ValueKind == JsonValueKind.Null)
            {
                var error = Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path));
                Assert.Equal(1, error.LineNumber);
                Assert.Contains("'DIRECTION' must not be empty", error.Reason);
                return;
            }
            var result = parser.ParseFile(path);
            Assert.EndsWith(".prt", sourceFile);
            Assert.Equal(row.GetProperty("Branch").GetString(), result.Header.Branch);
            Assert.Equal(row.GetProperty("ReferenceId").GetString(), result.Header.ReferenceId);
            Assert.Equal(row.GetProperty("Time").GetDateTime(), result.Header.Time);
            Assert.Equal("\n" + Section + "\n" + Content, result.Body);
        });
    }

    [Theory]
    [InlineData(false, "\n")]
    [InlineData(true, "\n")]
    [InlineData(false, "\r\n")]
    [InlineData(true, "\r\n")]
    [InlineData(false, "\r")]
    public void PreservesBodyAndReturnsSourceMetadata(bool bom, string newline)
    {
        var body = newline + Section + newline + Content.Replace("\n", newline);
        var text = SaaMessageParserTests.Header + newline + PrintLine + newline + Separator + newline + body;
        var encoding = new UTF8Encoding(bom);
        WithFile(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(), path =>
        {
            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, path);
            var result = parser.ParseFile(relativePath);
            Assert.Equal("PARIS", result.Header.Branch);
            Assert.Equal(body, result.Body);
            Assert.Equal(Path.GetFullPath(path), result.FilePath);
            Assert.Equal(Path.GetFileName(path), result.FileName);
            Assert.Matches("^[0-9a-f]{64}$", result.ContentHash);
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });
    }

    [Fact]
    public void PreservesMixedLineEndingsAndMissingFinalNewline()
    {
        var body = "\r\n" + Section + "\nMessage\r\nNext\rLast";
        var text = SaaMessageParserTests.Header + "\n" + PrintLine + "\r\n" + Separator + "\r" + body;
        WithFile(Encoding.UTF8.GetBytes(text), path => Assert.Equal(body, parser.ParseFile(path).Body));
    }

    [Fact]
    public void HashMatchesKnownSha256OfEntireFile()
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", Lines));
        WithFile(bytes, path => Assert.Equal("e0a5310594114f6bf752f32691c3d7c0f3aad24aabfd27c9b5f504f9422d9ef1", parser.ParseFile(path).ContentHash));
    }

    [Fact]
    public void HashDependsOnBytesIncludingHeaderBomAndLineEndingsButNotPath()
    {
        var text = string.Join("\n", Lines);
        var bytes = Encoding.UTF8.GetBytes(text);
        WithFile(bytes, firstPath => WithFile(bytes, secondPath =>
        {
            var original = parser.ParseFile(firstPath);
            Assert.Equal(original.ContentHash, parser.ParseFile(secondPath).ContentHash);
            foreach (var changed in new[]
            {
                Encoding.UTF8.GetBytes(text.Replace("Branch:PARIS", "Branch:LONDON")),
                Encoding.UTF8.GetBytes(text + "Additional content"),
                Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n")),
                new UTF8Encoding(true).GetPreamble().Concat(bytes).ToArray()
            })
            {
                File.WriteAllBytes(secondPath, changed);
                Assert.NotEqual(original.ContentHash, parser.ParseFile(secondPath).ContentHash);
            }
        }));
    }

    [Theory]
    [InlineData(0, "Invalid header", 1)]
    [InlineData(1, "", 2)]
    [InlineData(1, "2026/02/30-04:35:30 Printer 1", 2)]
    [InlineData(1, "30/02/26-04:35:30 Printer 1", 2)]
    [InlineData(1, "2026/10/14-25:35:30 Printer 1", 2)]
    [InlineData(1, "2026/10/14-04:35:30 1", 2)]
    [InlineData(1, "2026/10/14-04:35:30 Printer 2", 2)]
    [InlineData(1, "2026/10/14-04:35:30 Printer 1 extra", 2)]
    [InlineData(2, "________x________", 3)]
    [InlineData(2, "", 3)]
    [InlineData(3, "Message in the wrong place", 4)]
    [InlineData(4, "--- Another Section ---", 5)]
    [InlineData(4, "", 5)]
    [InlineData(5, " \t\n", 6)]
    public void ReportsStructuralErrorsWithFileAndLine(int index, string replacement, int expectedLine)
    {
        var lines = (string[])Lines.Clone();
        lines[index] = replacement;
        WithFile(Encoding.UTF8.GetBytes(string.Join("\n", lines)), path =>
        {
            var exception = Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path));
            Assert.IsAssignableFrom<FormatException>(exception);
            Assert.Equal(expectedLine, exception.LineNumber);
            Assert.Equal(path, exception.FilePath);
            Assert.Contains($"line {expectedLine}", exception.Message);
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    public void RejectsTruncatedFiles(int lineCount, int expectedLine)
    {
        var text = string.Join("\n", Lines.Take(lineCount)) + (lineCount == 0 ? "" : "\n");
        WithFile(Encoding.UTF8.GetBytes(text), path =>
            Assert.Equal(expectedLine, Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path)).LineNumber));
    }

    [Fact]
    public void AllowsVariablePrintIdentifierAndSpacing()
    {
        var lines = (string[])Lines.Clone();
        lines[1] = "14/10/26-02:00:22\tCustomPrinter-1\t1  ";
        lines[2] = "  ______  ";
        lines[3] = " \t";
        lines[4] = "\t--- Instance Type and Transmission -----  ";
        WithFile(Encoding.UTF8.GetBytes(string.Join("\n", lines)), path =>
            Assert.StartsWith(" \t\n", parser.ParseFile(path).Body));
    }

    [Fact]
    public void RejectsInvalidUtf8InsteadOfSilentlyReplacingBodyCharacters()
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", Lines)).Concat(new byte[] { 0xff }).ToArray();
        WithFile(bytes, path =>
        {
            var exception = Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path));
            Assert.Null(exception.LineNumber);
            Assert.Contains("UTF-8", exception.Message);
            Assert.IsType<DecoderFallbackException>(exception.InnerException);
        });
    }

    private static void WithFile(byte[] bytes, Action<string> action)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, bytes);
            action(path);
        }
        finally { File.Delete(path); }
    }
}
