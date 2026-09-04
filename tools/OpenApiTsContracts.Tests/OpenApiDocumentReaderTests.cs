using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Tests;

public sealed class OpenApiDocumentReaderTests
{
    [Theory]
    [InlineData("3.0.0")]
    [InlineData("3.0.4")]
    [InlineData("3.1.0")]
    [InlineData("3.1.2")]
    public void AcceptsSupportedOpenApiVersions(string version)
    {
        var document = GeneratorTestHelper.Parse(
            """{ "Value": { "type": "string", "nullable": true } }""",
            version);

        Assert.Equal(version, document.Version);
        Assert.True(document.Schemas["Value"].IsNullable);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("3.2.0")]
    [InlineData("3.1.x")]
    public void RejectsUnsupportedOrInvalidOpenApiVersions(string version)
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Parse("{}", version));

        Assert.Contains("Unsupported OpenAPI version", exception.Message);
    }

    [Fact]
    public void RejectsLocalReferencesOutsideComponentsSchemas()
    {
        var exception = Assert.Throws<UnsupportedSchemaException>(() =>
            GeneratorTestHelper.Parse(
                """{ "Value": { "$ref": "#/components/parameters/Value" } }"""));

        Assert.Contains("local $ref outside components.schemas", exception.Message);
    }
}
