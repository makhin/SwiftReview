namespace OpenApiTsContracts.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public async Task ReturnsInvalidCommandLineExitCodeAndWritesError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await OpenApiTsContractsApp.RunAsync(
            [],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("--input", error.ToString());
    }

    [Fact]
    public async Task ReturnsInvalidDocumentAndUnsupportedSchemaExitCodes()
    {
        using var directory = new TemporaryDirectory();
        var invalidPath = directory.Write("invalid.json", "not json");
        var unsupportedPath = directory.Write(
            "unsupported.json",
            DocumentWithSchemas("""{ "Dto": { "oneOf": [] } }"""));

        var invalidExitCode = await Run(invalidPath, directory.PathFor("invalid.ts"));
        var unsupportedExitCode = await Run(
            unsupportedPath,
            directory.PathFor("unsupported.ts"));

        Assert.Equal(ExitCodes.InvalidOpenApiDocument, invalidExitCode);
        Assert.Equal(ExitCodes.UnsupportedSchema, unsupportedExitCode);
    }

    [Fact]
    public async Task WritesUtf8WithoutBomAndCheckDetectsStaleOutputWithoutChangingIt()
    {
        using var directory = new TemporaryDirectory();
        var input = directory.Write(
            "openapi.json",
            DocumentWithSchemas("""{ "UserDto": { "type": "string" } }"""));
        var output = directory.PathFor("generated/contracts.generated.ts");

        Assert.Equal(ExitCodes.Success, await Run(input, output));
        Assert.False(File.ReadAllBytes(output).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal(ExitCodes.Success, await Run(input, output, "--check"));

        await File.WriteAllTextAsync(output, "stale", TestContext.Current.CancellationToken);
        Assert.Equal(ExitCodes.OutOfDate, await Run(input, output, "--check"));
        Assert.Equal(
            "stale",
            await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingInputReturnsGenerationErrorExitCode()
    {
        using var directory = new TemporaryDirectory();

        var exitCode = await Run(
            directory.PathFor("missing.json"),
            directory.PathFor("output.ts"));

        Assert.Equal(ExitCodes.GenerationError, exitCode);
    }

    private static async Task<int> Run(string input, string output, params string[] extraArguments)
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        var arguments = new List<string> { "--input", input, "--output", output };
        arguments.AddRange(extraArguments);
        return await OpenApiTsContractsApp.RunAsync(
            arguments.ToArray(),
            standardOutput,
            standardError,
            TestContext.Current.CancellationToken);
    }

    private static string DocumentWithSchemas(string schemas) => $$"""
        {
          "openapi": "3.1.0",
          "components": {
            "schemas": {{schemas}}
          }
        }
        """;

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"OpenApiTsContracts.Tests-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(path);

        public string PathFor(string relativePath) => System.IO.Path.Combine(path, relativePath);

        public string Write(string relativePath, string contents)
        {
            var filePath = PathFor(relativePath);
            File.WriteAllText(filePath, contents);
            return filePath;
        }

        public void Dispose() => Directory.Delete(path, recursive: true);
    }
}
