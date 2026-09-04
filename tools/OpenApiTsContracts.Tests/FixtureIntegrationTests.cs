using OpenApiTsContracts.Generation;
using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Tests;

public sealed class FixtureIntegrationTests
{
    [Fact]
    public async Task RealisticStoreDocumentMatchesGeneratedTypeScriptFixture()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var document = await OpenApiDocumentReader.ReadAsync(
            Path.Combine(fixtureDirectory, "store.openapi.json"),
            TestContext.Current.CancellationToken);
        var expected = await File.ReadAllTextAsync(
            Path.Combine(fixtureDirectory, "store.contracts.generated.ts"),
            TestContext.Current.CancellationToken);

        var actual = new TypeScriptGenerator().Generate(document);

        Assert.Equal(expected.Replace("\r\n", "\n", StringComparison.Ordinal), actual);
    }
}
