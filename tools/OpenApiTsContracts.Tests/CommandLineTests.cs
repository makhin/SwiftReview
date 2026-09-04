using OpenApiTsContracts.Cli;

namespace OpenApiTsContracts.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void ParsesAllOptions()
    {
        var success = CommandLineOptions.TryParse(
            [
                "--input", "openapi.json",
                "--output", "contracts.generated.ts",
                "--namespace", "Contracts.",
                "--verbose",
                "--check"
            ],
            out var options,
            out var error);

        Assert.True(success, error);
        Assert.Equal("openapi.json", options.InputPath);
        Assert.Equal("contracts.generated.ts", options.OutputPath);
        Assert.Equal("Contracts.", options.NamespacePrefix);
        Assert.True(options.Verbose);
        Assert.True(options.Check);
    }

    [Theory]
    [InlineData()]
    [InlineData("--input", "input.json")]
    [InlineData("--output", "output.ts")]
    [InlineData("--unknown")]
    [InlineData("--input")]
    [InlineData("--check", "--check")]
    public void RejectsInvalidCommandLines(params string[] args)
    {
        Assert.False(CommandLineOptions.TryParse(args, out _, out var error));
        Assert.NotEmpty(error);
    }
}
