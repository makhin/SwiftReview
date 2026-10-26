using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageReferenceTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ExtractsPrintedReferencesAndMapsThemWithoutChangingBody(string newline)
    {
        using var file = new ImportTestFile();
        var body = string.Join(newline,
            "--- Message Text ---",
            "        20: Transaction Reference Number", "            8801IC000673",
            "        21: Related Reference", "            1234",
            "        79: Narrative", "            20:OTHER", "            21:OTHER",
            "--- Message Trailer ---");
        file.Write(body: body);
        var document = new SaaMessageParser().ParseFile(file.Path);
        var record = SqlSaaImportStore.CreateRecord(document, file.Root);
        Assert.Equal("8801IC000673", record.TransactionReferenceNumber);
        Assert.Equal("1234", record.RelatedReference);
        Assert.Equal("EMON261435", record.ReferenceId);
        Assert.EndsWith(body, document.Body);
        Assert.Equal(document.Body, record.Body);
    }

    [Theory]
    [InlineData("--- Message Text ---\n        20C: Reference\n            :SEME//6393445")]
    [InlineData("--- Message Text ---\n        79: Narrative\n            20:OTHER\n            21:OTHER")]
    [InlineData("--- Message Text ---\n        79: Narrative\n            20: Transaction Reference Number\n                QUOTED\n            21: Related Reference\n                QUOTED")]
    [InlineData("        20: Transaction Reference Number\n            OUTSIDE")]
    [InlineData("--- Message Text ---\n--- Message Trailer ---\n        21: Related Reference\n            OUTSIDE")]
    [InlineData("--- Message Text ---\n        20: Transaction Reference Number\n        21: Related Reference\n--- Message Trailer ---")]
    public void AbsentReferencesRemainNull(string body)
    {
        using var file = new ImportTestFile();
        file.Write(body: body);
        var record = SqlSaaImportStore.CreateRecord(new SaaMessageParser().ParseFile(file.Path), file.Root);
        Assert.Null(record.TransactionReferenceNumber);
        Assert.Null(record.RelatedReference);
    }

    [Fact]
    public void SupportsMt101LabelsAndKeepsFirstTransactionReference()
    {
        using var file = new ImportTestFile();
        file.Write(body: "--- Message Text ---\n        20: Sender's Reference\n            CMSFF55783\n        21: Transaction Reference\n            dg  test edit\n        21: Transaction Reference\n            SECOND");
        var document = new SaaMessageParser().ParseFile(file.Path);
        Assert.Equal("CMSFF55783", document.TransactionReferenceNumber);
        Assert.Equal("dg  test edit", document.RelatedReference);
    }

    [Theory]
    [InlineData("Possible duplicate information received from network")]
    [InlineData("Possible duplicate indicator set locally\n \n       Duplicate detected by interface")]
    public void AcceptsDuplicateNoticesAndPreservesThem(string notice)
    {
        using var file = new ImportTestFile();
        file.Write();
        var text = File.ReadAllText(file.Path).Replace("--- Instance Type", "       " + notice + "\n\n--- Instance Type");
        File.WriteAllText(file.Path, text);
        var document = new SaaMessageParser().ParseFile(file.Path);
        Assert.Contains(notice, document.Body);
        Assert.EndsWith("message content", document.Body);
    }
}
