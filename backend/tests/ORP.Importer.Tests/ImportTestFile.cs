namespace ORP.Importer.Tests;

internal sealed class ImportTestFile : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "orp-import-" + Guid.NewGuid().ToString("N"));
    public string Path => System.IO.Path.Combine(Root, "sample.prt");

    public ImportTestFile() => Directory.CreateDirectory(Root);

    public void Write(string type = "MT199", string direction = "Incoming", string branch = "PARIS", string body = "message content") =>
        File.WriteAllText(Path, string.Join("\n", SaaMessageParserTests.Header.Replace("MXpain.001.001.09", type)
            .Replace("DIRECTION:Incoming", "DIRECTION:" + direction).Replace("Branch:PARIS", "Branch:" + branch),
            "2026/10/15-04:35:30 PRINT 1", "________", "", "--- Instance Type and Transmission ---", body));

    public static SaaDepartmentRule Rule(string type) => new()
    {
        Id = 1, DepartmentId = 7, Direction = SaaMessageDirection.Incoming,
        MatchKind = SaaDepartmentMatchKind.Exact, MessageTypePattern = type, IsActive = true
    };

    public void Dispose() => Directory.Delete(Root, true);
}
