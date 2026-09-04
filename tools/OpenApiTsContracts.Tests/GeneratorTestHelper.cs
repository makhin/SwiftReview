using OpenApiTsContracts.Generation;
using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Tests;

internal static class GeneratorTestHelper
{
    public static string Generate(string schemas, string version = "3.1.0")
    {
        var json = """
            {
              "openapi": "VERSION",
              "components": {
                "schemas": SCHEMAS
              }
            }
            """
            .Replace("VERSION", version, StringComparison.Ordinal)
            .Replace("SCHEMAS", schemas, StringComparison.Ordinal);

        return new TypeScriptGenerator().Generate(OpenApiDocumentReader.Parse(json));
    }

    public static OpenApiContractDocument Parse(string schemas, string version = "3.1.0")
    {
        var json = $$"""
            {
              "openapi": "{{version}}",
              "components": {
                "schemas": {{schemas}}
              }
            }
            """;
        return OpenApiDocumentReader.Parse(json);
    }
}
