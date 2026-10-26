using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ORP.Importer;

public interface ISaaMessageParser
{
    SaaMessageDocument Parse(SaaSourceFileSnapshot file);
}

public sealed class SaaMessageParser : ISaaMessageParser
{
    private readonly ILogger<SaaMessageParser> logger;

    public SaaMessageParser(ILogger<SaaMessageParser>? logger = null)
    {
        this.logger = logger ?? NullLogger<SaaMessageParser>.Instance;
    }

    private static readonly string[] RequiredKeys =
        { "Branch", "MessageType", "Reference#", "DIRECTION", "SENDER", "RECEIVER", "Host", "Time" };

    private const string PrintLinePattern = @"\A(?<date>(?:[0-9]{4}/[0-9]{2}/[0-9]{2}|[0-9]{2}/[0-9]{2}/[0-9]{2})-[0-9]{2}:[0-9]{2}:[0-9]{2})[ \t]+\S+[ \t]+1[ \t]*\z";

    /// <summary>Validates a UTF-8 SAA print file and reads its header, body and source metadata.</summary>
    public SaaMessageDocument ParseFile(string path) =>
        Parse(new SaaSourceFileReader().Read(path, Path.GetDirectoryName(Path.GetFullPath(path))!));

    public SaaMessageDocument Parse(SaaSourceFileSnapshot file)
    {
        var filePath = file.FilePath;
        ImporterLog.ParsingStarted(logger, filePath);
        try
        {
            var document = ParseFileContents(file);
            ImporterLog.ParsingCompleted(logger, filePath, document.ContentHash);
            return document;
        }
        catch (SaaMessageFormatException exception)
        {
            ImporterLog.ParsingFailed(logger, exception, filePath, exception.LineNumber, exception.Reason);
            throw;
        }
    }

    private SaaMessageDocument ParseFileContents(SaaSourceFileSnapshot file)
    {
        var filePath = file.FilePath;
        var bytes = file.Bytes;
        var bomLength = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes, bomLength, bytes.Length - bomLength);
        }
        catch (DecoderFallbackException exception)
        {
            throw new SaaMessageFormatException(filePath, null, "Expected valid UTF-8 text.", exception);
        }

        var offset = 0;
        SaaMessageHeader header;
        try
        {
            header = ParseLine(ReadLine(text, ref offset) ?? "");
        }
        catch (FormatException exception)
        {
            throw new SaaMessageFormatException(filePath, 1, exception.Message, exception);
        }

        var printLine = ReadLine(text, ref offset);
        // The print identifier varies by branch; the first page must be page 1.
        var match = Regex.Match(printLine ?? "", PrintLinePattern);
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.Calendar.TwoDigitYearMax = 2099;
        if (!match.Success || !DateTime.TryParseExact(match.Groups["date"].Value,
                new[] { "yyyy/MM/dd-HH:mm:ss", "dd/MM/yy-HH:mm:ss" }, culture, DateTimeStyles.None, out _))
            throw new SaaMessageFormatException(filePath, 2, "Expected a print timestamp, print identifier and page number 1.");

        var separator = ReadLine(text, ref offset);
        if (separator is null || !Regex.IsMatch(separator.Trim(), @"\A_{3,}\z"))
            throw new SaaMessageFormatException(filePath, 3, "Expected an underscore separator.");

        var bodyOffset = offset;
        var blankLine = ReadLine(text, ref offset);
        if (blankLine is null || !string.IsNullOrWhiteSpace(blankLine))
            throw new SaaMessageFormatException(filePath, 4, "Expected a blank line before the message section.");

        var sectionLine = 5;
        var section = ReadLine(text, ref offset);
        while (section is not null && IsDuplicateNotice(section.Trim()))
        {
            section = ReadLine(text, ref offset);
            sectionLine++;
            while (section is not null && string.IsNullOrWhiteSpace(section))
            {
                section = ReadLine(text, ref offset);
                sectionLine++;
            }
        }
        if (section is null || !Regex.IsMatch(section, @"\A[ \t]*-{3,}[ \t]+Instance Type and Transmission[ \t]+-{3,}[ \t]*\z"))
            throw new SaaMessageFormatException(filePath, sectionLine, "Expected the Instance Type and Transmission section.");
        if (string.IsNullOrWhiteSpace(text.Substring(offset)))
            throw new SaaMessageFormatException(filePath, sectionLine + 1, "Expected message content after the section heading.");

        var body = text.Substring(bodyOffset);
        return new SaaMessageDocument(header, body, filePath, file.ContentHash,
            ReadReference(body, "20"), ReadReference(body, "21"));
    }

    private static bool IsDuplicateNotice(string line) =>
        line == "Possible duplicate information received from network" ||
        line == "Possible duplicate indicator set locally" ||
        line == "Duplicate detected by interface";

    // Only printed fields in Message Text count; references quoted in Narrative do not.
    // For repeating transaction sequences, retain the first occurrence in the message.
    private static string? ReadReference(string body, string tag)
    {
        var offset = 0;
        var inMessageText = false;
        int? fieldColumn = null;
        string? line;
        while ((line = ReadLine(body, ref offset)) is not null)
        {
            if (Regex.IsMatch(line, @"\A[ \t]*-{3,}[ \t]+Message Text[ \t]+-{3,}[ \t]*\z"))
            {
                inMessageText = true;
                continue;
            }
            if (!inMessageText) continue;
            if (Regex.IsMatch(line, @"\A[ \t]*-{3,}[ \t]+Message Trailer[ \t]+-{3,}")) break;
            if (fieldColumn is null && Regex.IsMatch(line, @"\A[ \t]*[0-9]{2,3}[A-Z]?:[ \t]+\S"))
                fieldColumn = line.IndexOf(':');
            if (line.IndexOf(':') != fieldColumn) continue;
            var label = tag == "20" ? @"(?:Transaction Reference Number|Sender's Reference)"
                : @"(?:Related Reference|Transaction Reference)";
            var field = Regex.Match(line, @"\A(?<indent>[ \t]*)" + tag + @":[ \t]+" + label + @"[ \t]*\z");
            if (!field.Success) continue;
            var value = ReadLine(body, ref offset);
            if (value is null || string.IsNullOrWhiteSpace(value)) return null;
            var indent = value.Length - value.TrimStart(' ', '\t').Length;
            return indent > field.Groups["indent"].Length ? value.Trim() : null;
        }
        return null;
    }

    private static string? ReadLine(string text, ref int offset)
    {
        if (offset >= text.Length)
            return null;
        var start = offset;
        while (offset < text.Length && text[offset] != '\r' && text[offset] != '\n')
            offset++;
        var line = text.Substring(start, offset - start);
        if (offset < text.Length && text[offset++] == '\r' && offset < text.Length && text[offset] == '\n')
            offset++;
        return line;
    }

    public SaaMessageHeader ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new FormatException("The message header is empty.");
        if (Regex.IsMatch(line, PrintLinePattern))
            throw new FormatException("SAA print report has no import metadata header. Expected 'Branch:...|MessageType:...|...' before the print timestamp.");

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in line.Split('|'))
        {
            var separator = segment.IndexOf(':');
            if (separator < 0 || string.IsNullOrWhiteSpace(segment.Substring(0, separator)))
                throw new FormatException("A header segment must have a non-empty key followed by ':'.");

            var key = segment.Substring(0, separator).Trim();
            if (string.Equals(key, "RECIEVER", StringComparison.OrdinalIgnoreCase))
                key = "RECEIVER";
            if (!RequiredKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                continue;
            if (fields.ContainsKey(key))
                throw new FormatException($"Duplicate header field '{key}'.");

            var value = segment.Substring(separator + 1).Trim();
            fields.Add(key, value.Length == 0 ? null : value);
        }

        foreach (var key in RequiredKeys)
            if (!fields.ContainsKey(key))
                throw new FormatException($"Missing header field '{key}'.");

        return new SaaMessageHeader
        {
            Branch = RequireValue(fields["Branch"], "Branch"),
            MessageType = RequireValue(fields["MessageType"], "MessageType"),
            ReferenceId = fields["Reference#"],
            Direction = ParseDirection(fields["DIRECTION"]),
            Sender = fields["SENDER"],
            Receiver = fields["RECEIVER"],
            Host = fields["Host"],
            Time = ParseTime(RequireValue(fields["Time"], "Time"))
        };
    }

    private static SaaMessageDirection ParseDirection(string? value)
    {
        if (value is null)
            throw new FormatException("Header field 'DIRECTION' must not be empty. Expected Incoming or Outgoing.");
        if (string.Equals(value, "Incoming", StringComparison.OrdinalIgnoreCase))
            return SaaMessageDirection.Incoming;
        if (string.Equals(value, "Outgoing", StringComparison.OrdinalIgnoreCase))
            return SaaMessageDirection.Outgoing;
        throw new FormatException("Invalid header field 'DIRECTION': expected Incoming or Outgoing.");
    }

    private static string RequireValue(string? value, string field) =>
        value ?? throw new FormatException($"Header field '{field}' must not be empty.");

    private static DateTime ParseTime(string value)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.Calendar.TwoDigitYearMax = 2099;
        if (!DateTime.TryParseExact(value, "dd/MM/yy hh:mm:ss tt", culture, DateTimeStyles.None, out var time))
            throw new FormatException("Invalid header field 'Time': expected dd/MM/yy hh:mm:ss tt.");
        return DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
    }
}
