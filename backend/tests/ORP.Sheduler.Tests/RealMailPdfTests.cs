using System.Runtime.CompilerServices;
using System.Text;
using ORP.Scheduler.Documents;
using UglyToad.PdfPig;
using Xunit;

namespace ORP.Sheduler.Tests;

public sealed class RealMailPdfTests
{
    [Fact]
    public void ConvertsAllRealMailFilesAndOverwritesPdfs()
    {
        var fixtures = Path.Combine(GetSourceDirectory(), "Fixtures", "RealMail");
        var inputDirectory = Path.Combine(fixtures, "txt");
        Assert.SkipWhen(!Directory.Exists(inputDirectory), $"Real mail fixtures are missing: {inputDirectory}");
        var files = Directory.GetFiles(inputDirectory);
        Assert.SkipWhen(files.Length == 0, $"Real mail fixtures are empty: {inputDirectory}");

        var outputDirectory = Path.Combine(fixtures, "pdf");
        Directory.CreateDirectory(outputDirectory);
        var converter = new ITextTextToPdfConverter();
        foreach (var file in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            var outputPath = Path.Combine(outputDirectory, Path.ChangeExtension(Path.GetFileName(file), ".pdf"));
            File.WriteAllBytes(outputPath, converter.Convert(text));

            using var pdf = PdfDocument.Open(outputPath);
            Assert.True(pdf.NumberOfPages > 0, $"The generated PDF has no pages: {outputPath}");
        }
    }

    // Resolve fixtures in the source project, rather than the test runner's bin directory.
    private static string GetSourceDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetDirectoryName(sourcePath)!;
}
