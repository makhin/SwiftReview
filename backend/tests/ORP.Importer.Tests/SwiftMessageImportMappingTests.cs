using System.Text;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SwiftMessageImportMappingTests
{
    private const string PrintLine = "2026/10/14-04:35:30 PRMXSwiftPrt-2614-002614 1";
    private const string Separator = "____________________________________________________________________";
    private const string Section = "--- Instance Type and Transmission ---";

    [Fact]
    public void MapsParsedDocumentAndRelativePathWithoutChangingBody()
    {
        WithFile("PR", SaaMessageParserTests.Header, (root, file) =>
        {
            var parsed = new SaaMessageParser().ParseFile(file);
            var record = SwiftMessageImporter.CreateRecord(parsed, root);

            Assert.Equal("PR/sample.prt", record.FilePath);
            Assert.Equal("sample.prt", record.FileName);
            Assert.Equal("PARIS", record.Branch);
            Assert.Equal("MXpain.001.001.09", record.MessageType);
            Assert.Equal("EMON261435", record.ReferenceId);
            Assert.Equal(MessageDirection.Incoming, record.Direction);
            Assert.Equal("SMBCFRPPXXX", record.Sender);
            Assert.Equal("SMBCFRPPXXX", record.Receiver);
            Assert.Equal("njcvsaadv01", record.Host);
            Assert.Equal(new DateTime(2026, 10, 14, 4, 35, 39), record.Time);
            Assert.Equal(DateTimeKind.Unspecified, record.Time!.Value.Kind);
            Assert.Equal(parsed.Body, record.Body);
            Assert.Equal(parsed.ContentHash, record.ContentHash);
        });
    }

    [Fact]
    public void KeepsEmptyValuesAndOutgoingDirection()
    {
        var header = SaaMessageParserTests.Header.Replace("Reference#: EMON261435", "Reference#:")
            .Replace("DIRECTION:Incoming", "DIRECTION:Outgoing");
        WithFile("nested", header, (root, file) =>
        {
            var record = SwiftMessageImporter.CreateRecord(new SaaMessageParser().ParseFile(file), root);
            Assert.Equal("PARIS", record.Branch);
            Assert.Null(record.ReferenceId);
            Assert.Equal(MessageDirection.Outgoing, record.Direction);
            Assert.Equal("nested/sample.prt", record.FilePath);
        });
    }

    [Fact]
    public void RejectsAFileOutsideTheScanRoot()
    {
        WithFile("PR", SaaMessageParserTests.Header, (root, file) =>
        {
            var document = new SaaMessageParser().ParseFile(file);
            Assert.Throws<ArgumentException>(() => SwiftMessageImporter.CreateRecord(document, Path.Combine(root, "other")));
        });
    }

    [Fact]
    public void RejectsRelativePathLongerThanTheIndexedColumn()
    {
        var longPath = string.Join(Path.DirectorySeparatorChar,
            new string('a', 150), new string('b', 150), new string('c', 150));
        WithFile(longPath, SaaMessageParserTests.Header, (root, file) =>
        {
            var document = new SaaMessageParser().ParseFile(file);
            Assert.Contains("FilePath", Assert.Throws<FormatException>(() =>
                SwiftMessageImporter.CreateRecord(document, root)).Message);
        });
    }

    private static void WithFile(string relativeDirectory, string header, Action<string, string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "orp-import-test-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, relativeDirectory);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "sample.prt");
        try
        {
            File.WriteAllText(file, string.Join("\n", header, PrintLine, Separator, "", Section, "message content"),
                new UTF8Encoding(false));
            action(root, file);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
