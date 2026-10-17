using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageParserTests
{
    internal const string Header = "Branch:PARIS|MessageType:MXpain.001.001.09|Reference#: EMON261435|DIRECTION:Incoming|SENDER:  SMBCFRPPXXX|RECEIVER:  SMBCFRPPXXX|Host:njcvsaadv01|Time:14/10/26 04:35:39 AM";
    private readonly SaaMessageParser parser = new();

    public static IEnumerable<object[]> SampleHeaders()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "headers.json"));
        using var document = JsonDocument.Parse(json);
        foreach (var row in document.RootElement.EnumerateArray())
            yield return new object[] { row.GetProperty("File").GetString()!, row.GetRawText() };
    }

    [Theory]
    [MemberData(nameof(SampleHeaders))]
    public void ParsesSampleHeader(string file, string json)
    {
        Assert.EndsWith(".prt", file);
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        var actual = parser.ParseLine(expected.GetProperty("Line").GetString()!);

        Assert.Equal(expected.GetProperty("Branch").GetString(), actual.Branch);
        Assert.Equal(expected.GetProperty("MessageType").GetString(), actual.MessageType);
        Assert.Equal(expected.GetProperty("ReferenceId").GetString(), actual.ReferenceId);
        var direction = expected.GetProperty("Direction");
        Assert.Equal(direction.ValueKind == JsonValueKind.Null ? null : (SaaMessageDirection?)direction.GetInt32(), actual.Direction);
        Assert.Equal(expected.GetProperty("Sender").GetString(), actual.Sender);
        Assert.Equal(expected.GetProperty("Receiver").GetString(), actual.Receiver);
        Assert.Equal(expected.GetProperty("Host").GetString(), actual.Host);
        Assert.Equal(expected.GetProperty("Time").GetDateTime(), actual.Time);
        Assert.Equal(DateTimeKind.Unspecified, actual.Time.Kind);
    }

    [Fact]
    public void AcceptsReorderedCaseInsensitiveKeysAndTrimsValues()
    {
        var result = parser.ParseLine(" time : 14/10/26 04:35:39 AM | host : server | reciever : RECEIVER | sender : SENDER | direction : outgoing | reference# : abc:123 | messagetype : MT081 | branch : DUBAI ");
        Assert.Equal("DUBAI", result.Branch);
        Assert.Equal("MT081", result.MessageType);
        Assert.Equal("abc:123", result.ReferenceId);
        Assert.Equal(SaaMessageDirection.Outgoing, result.Direction);
        Assert.Equal("SENDER", result.Sender);
        Assert.Equal("RECEIVER", result.Receiver);
        Assert.Equal("server", result.Host);
        Assert.Equal(new DateTime(2026, 10, 14, 4, 35, 39), result.Time);
    }

    [Fact]
    public void EmptyValuesAreNull()
    {
        var result = parser.ParseLine("Branch: PARIS |MessageType: MT199|Reference#:|DIRECTION: |SENDER:|RECEIVER:|Host:|Time:14/10/26 04:35:39 AM");
        Assert.Null(result.ReferenceId);
        Assert.Null(result.Direction);
        Assert.Null(result.Sender);
        Assert.Null(result.Receiver);
        Assert.Null(result.Host);
    }

    [Theory]
    [InlineData("Branch", "PARIS")]
    [InlineData("MessageType", "MXpain.001.001.09")]
    [InlineData("Time", "14/10/26 04:35:39 AM")]
    public void RejectsEmptyRequiredValues(string key, string value)
    {
        var line = Header.Replace(key + ":" + value, key + ":   ");
        Assert.Contains(key, Assert.Throws<FormatException>(() => parser.ParseLine(line)).Message);
    }

    [Fact]
    public void IgnoresAdditionalKeysAndAllowsRepeatedReferencesAcrossMessages()
    {
        var first = parser.ParseLine(Header + "|Extra:a:b|Extra:c");
        var second = parser.ParseLine(Header.Replace("04:35:39", "05:08:53"));
        Assert.Equal("EMON261435", first.ReferenceId);
        Assert.Equal(first.ReferenceId, second.ReferenceId);
        Assert.NotEqual(first.Time, second.Time);
    }

    [Theory]
    [InlineData("Incoming", SaaMessageDirection.Incoming)]
    [InlineData("INCOMING", SaaMessageDirection.Incoming)]
    [InlineData("Outgoing", SaaMessageDirection.Outgoing)]
    [InlineData("outgoing", SaaMessageDirection.Outgoing)]
    public void ParsesDirections(string value, SaaMessageDirection expected) =>
        Assert.Equal(expected, parser.ParseLine(Header.Replace("Incoming", value)).Direction);

    [Theory]
    [InlineData("01/01/00 12:00:00 AM", 2000, 1, 1, 0)]
    [InlineData("31/12/99 12:00:00 PM", 2099, 12, 31, 12)]
    [InlineData("29/02/00 11:00:00 PM", 2000, 2, 29, 23)]
    [InlineData("14/10/26 07:00:00 PM", 2026, 10, 14, 19)]
    public void ParsesTimeIndependentlyOfCurrentCulture(string value, int year, int month, int day, int hour)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var result = parser.ParseLine(Header.Replace("14/10/26 04:35:39 AM", value));
            Assert.Equal(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified), result.Time);
            Assert.Equal(DateTimeKind.Unspecified, result.Time.Kind);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Branch:PARIS")]
    [InlineData("broken")]
    public void RejectsEmptyOrIncompleteHeaders(string line) =>
        Assert.Throws<FormatException>(() => parser.ParseLine(line));

    [Theory]
    [InlineData("2026/10/09-18:48:40          BRMXSwiftPrt-0583-000583              1")]
    [InlineData("2026/10/12-18:48:06          BRMXSwiftPrt-0584-000584              1")]
    [InlineData("2026/10/12-18:48:22          BRMXSwiftPrt-0585-000585              1")]
    [InlineData("2026/10/13-08:04:22          MLMXSwiftPrt-0151-000151              1")]
    [InlineData("2026/10/13-07:54:30          PRMXSwiftPrt-2605-002605              1")]
    [InlineData("2026/10/13-09:08:14          PRMXSwiftPrt-2606-002606              1")]
    public void IdentifiesRawPrintReportsInsteadOfReportingOneMissingField(string printLine)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Join("\n", printLine, "________", "",
                "--- Instance Type and Transmission ---", "message content"));
            var error = Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path));
            Assert.Equal(1, error.LineNumber);
            Assert.Contains("no import metadata header", error.Reason);
            Assert.DoesNotContain("Missing header field", error.Reason);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Branch")]
    [InlineData("MessageType")]
    [InlineData("Reference#")]
    [InlineData("DIRECTION")]
    [InlineData("SENDER")]
    [InlineData("RECEIVER")]
    [InlineData("Host")]
    [InlineData("Time")]
    public void RejectsMissingAndDuplicateKnownKeys(string key)
    {
        var missing = string.Join("|", Header.Split('|').Where(x => !x.StartsWith(key + ":", StringComparison.Ordinal)));
        Assert.Contains(key, Assert.Throws<FormatException>(() => parser.ParseLine(missing)).Message);
        var duplicate = Header + "|" + key.ToLowerInvariant() + ":";
        Assert.Contains("Duplicate", Assert.Throws<FormatException>(() => parser.ParseLine(duplicate)).Message);
    }

    [Theory]
    [InlineData("|RECIEVER:another")]
    [InlineData("|broken")]
    [InlineData("|:value")]
    [InlineData("|  :value")]
    [InlineData("|")]
    public void RejectsMalformedSegmentsAndDuplicateReceiverAlias(string suffix) =>
        Assert.Throws<FormatException>(() => parser.ParseLine(Header + suffix));

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("Incoming, Outgoing")]
    [InlineData("Unknown")]
    public void RejectsInvalidDirection(string value) =>
        Assert.Contains("DIRECTION", Assert.Throws<FormatException>(() => parser.ParseLine(Header.Replace("Incoming", value))).Message);

    [Theory]
    [InlineData("29/02/26 04:35:39 AM")]
    [InlineData("14/13/26 04:35:39 AM")]
    [InlineData("14/10/26 13:35:39 PM")]
    [InlineData("14/10/26 04:35:39")]
    [InlineData("14/10/2026 04:35:39 AM")]
    [InlineData("invalid")]
    public void RejectsInvalidTime(string value) =>
        Assert.Contains("Time", Assert.Throws<FormatException>(() => parser.ParseLine(Header.Replace("14/10/26 04:35:39 AM", value))).Message);

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\n" + Header)]
    [InlineData("invalid")]
    public void RejectsInvalidFileAndReleasesIt(string content)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            Assert.Throws<SaaMessageFormatException>(() => parser.ParseFile(path));
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PreservesFileNotFoundException() =>
        Assert.Throws<FileNotFoundException>(() => parser.ParseFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".prt")));
}
