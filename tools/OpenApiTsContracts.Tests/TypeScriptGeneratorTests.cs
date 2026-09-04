using OpenApiTsContracts.Generation;
using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Tests;

public sealed class TypeScriptGeneratorTests
{
    [Fact]
    public void GeneratesPrimitiveRequiredAndOptionalProperties()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "PrimitiveDto": {
                "type": "object",
                "required": ["text", "count", "ratio", "enabled", "createdAt"],
                "properties": {
                  "text": { "type": "string" },
                  "count": { "type": "integer", "format": "int64" },
                  "ratio": { "type": "number", "format": "double" },
                  "enabled": { "type": "boolean" },
                  "createdAt": { "type": "string", "format": "date-time" },
                  "comment": { "type": "string" }
                }
              }
            }
            """);

        Assert.Contains("text: string;", output);
        Assert.Contains("count: number;", output);
        Assert.Contains("ratio: number;", output);
        Assert.Contains("enabled: boolean;", output);
        Assert.Contains("createdAt: string;", output);
        Assert.Contains("comment?: string;", output);
    }

    [Fact]
    public void DistinguishesRequiredOptionalAndNullableProperties()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "NullableDto": {
                "type": "object",
                "required": ["requiredNullable", "required31"],
                "properties": {
                  "requiredNullable": { "type": "string", "nullable": true },
                  "optional": { "type": "string" },
                  "optionalNullable": { "type": "string", "nullable": true },
                  "required31": { "type": ["string", "null"] }
                }
              }
            }
            """);

        Assert.Contains("requiredNullable: string | null;", output);
        Assert.Contains("optional?: string;", output);
        Assert.Contains("optionalNullable?: string | null;", output);
        Assert.Contains("required31: string | null;", output);
    }

    [Fact]
    public void GeneratesPrimitiveAndReferenceArrays()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "ArrayDto": {
                "type": "object",
                "required": ["tags", "users"],
                "properties": {
                  "tags": { "type": "array", "items": { "type": "string" } },
                  "users": {
                    "type": "array",
                    "items": { "$ref": "#/components/schemas/UserDto" }
                  }
                }
              },
              "UserDto": { "type": "object", "properties": {} }
            }
            """);

        Assert.Contains("tags: string[];", output);
        Assert.Contains("users: UserDto[];", output);
    }

    [Fact]
    public void ArrayWithoutItemsRepresentsArbitraryJsonValuesAsUnknown()
    {
        var output = GeneratorTestHelper.Generate(
            """{ "Values": { "type": "array" } }""");

        Assert.Contains("export type Values = unknown[];", output);
        Assert.DoesNotContain("any[]", output);
    }

    [Fact]
    public void GeneratesSupportedPrimitiveTypeUnions()
    {
        var output = GeneratorTestHelper.Generate(
            """{ "WireNumber": { "type": ["integer", "string", "null"] } }""");

        Assert.Contains("export type WireNumber = number | string | null;", output);
    }

    [Fact]
    public void GeneratesNestedDtoReferences()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "OrderDto": {
                "type": "object",
                "required": ["user"],
                "properties": {
                  "user": { "$ref": "#/components/schemas/UserDto" }
                }
              },
              "UserDto": { "type": "object", "properties": {} }
            }
            """);

        Assert.Contains("user: UserDto;", output);
    }

    [Theory]
    [InlineData("string", "[\"Pending\", \"Approved\"]", "\"Pending\"", "\"Approved\"")]
    [InlineData("integer", "[0, 1, 2]", "0", "2")]
    [InlineData("number", "[0.5, 1, 2.5]", "0.5", "2.5")]
    [InlineData("boolean", "[true, false]", "true", "false")]
    public void GeneratesNamedLiteralEnums(
        string type,
        string values,
        string first,
        string last)
    {
        var output = GeneratorTestHelper.Generate(
            $$"""{ "Value": { "type": "{{type}}", "enum": {{values}} } }""");

        Assert.Contains("export type Value =", output);
        Assert.Contains($"  | {first}", output);
        Assert.Contains($"  | {last};", output);
        Assert.DoesNotContain("export enum", output);
    }

    [Fact]
    public void PreservesSingleBooleanEnumLiteral()
    {
        var output = GeneratorTestHelper.Generate(
            """{ "Enabled": { "type": "boolean", "enum": [true] } }""");

        Assert.Contains("export type Enabled =\n  true;", output);
    }

    [Fact]
    public void InfersHomogeneousTypeLessEnumFromLiteralValues()
    {
        var output = GeneratorTestHelper.Generate(
            """{ "Status": { "enum": ["Pending", "Approved"] } }""");

        Assert.Contains("export type Status =\n  | \"Pending\"\n  | \"Approved\";", output);
    }

    [Fact]
    public void RejectsMixedTypeLessEnumValues()
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Generate(
                """{ "Status": { "enum": ["Pending", 1] } }"""));

        Assert.Contains("must all have the same JSON primitive type", exception.Message);
    }

    [Fact]
    public void GeneratesInlineStringAndNumericEnums()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "ReviewDto": {
                "type": "object",
                "required": ["status"],
                "properties": {
                  "status": {
                    "type": "string",
                    "enum": ["Pending", "Approved", "Rejected"]
                  },
                  "priority": { "type": "integer", "enum": [1, 2, 3] }
                }
              }
            }
            """);

        Assert.Contains("status: \"Pending\" | \"Approved\" | \"Rejected\";", output);
        Assert.Contains("priority?: 1 | 2 | 3;", output);
    }

    [Fact]
    public void ReferencesNamedEnumAndPreservesEnumNullability()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "ReviewDto": {
                "type": "object",
                "required": ["status"],
                "properties": {
                  "status": {
                    "$ref": "#/components/schemas/ReviewStatus",
                    "nullable": true
                  },
                  "optionalStatus": {
                    "$ref": "#/components/schemas/ReviewStatus",
                    "nullable": true
                  }
                }
              },
              "ReviewStatus": {
                "type": "string",
                "enum": ["Pending", "Approved"]
              }
            }
            """);

        Assert.Contains("status: ReviewStatus | null;", output);
        Assert.Contains("optionalStatus?: ReviewStatus | null;", output);
    }

    [Fact]
    public void RejectsMixedTypeEnumValues()
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Generate(
                """{ "Status": { "type": "integer", "enum": [1, "Two"] } }"""));

        Assert.Contains("Expected values of type 'integer' but found 'string'", exception.Message);
    }

    [Fact]
    public void RejectsEmptyEnum()
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Generate(
                """{ "Status": { "type": "string", "enum": [] } }"""));

        Assert.Contains("enum must contain at least one value", exception.Message);
    }

    [Fact]
    public void GeneratesDictionariesWithPrimitiveAndReferenceValues()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "StringMap": {
                "type": "object",
                "additionalProperties": { "type": "string" }
              },
              "UserMap": {
                "type": "object",
                "additionalProperties": { "$ref": "#/components/schemas/UserDto" }
              },
              "UserDto": { "type": "object", "properties": {} }
            }
            """);

        Assert.Contains("export type StringMap = Record<string, string>;", output);
        Assert.Contains("export type UserMap = Record<string, UserDto>;", output);
    }

    [Fact]
    public void GeneratesNestedAnonymousObjects()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "UserDto": {
                "type": "object",
                "properties": {
                  "address": {
                    "type": "object",
                    "required": ["city"],
                    "properties": {
                      "city": { "type": "string" },
                      "street": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);

        Assert.Contains(
            "  address?: {\n    city: string;\n    street?: string;\n  };",
            output);
    }

    [Fact]
    public void RejectsMissingLocalReferenceTarget()
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Generate(
                """{ "Order": { "$ref": "#/components/schemas/Missing" } }"""));

        Assert.Contains("Target schema was not found", exception.Message);
    }

    [Fact]
    public void RejectsExternalReference()
    {
        var exception = Assert.Throws<UnsupportedSchemaException>(() =>
            GeneratorTestHelper.Generate(
                """{ "Order": { "$ref": "other.json#/components/schemas/Order" } }"""));

        Assert.Contains("external $ref", exception.Message);
    }

    [Theory]
    [InlineData("{ \"type\": \"null\" }, { \"$ref\": \"#/components/schemas/MessageFilter\" }")]
    [InlineData("{ \"$ref\": \"#/components/schemas/MessageFilter\" }, { \"type\": \"null\" }")]
    public void GeneratesNullableLocalReferenceFromExactOneOf(string variants)
    {
        var output = GeneratorTestHelper.Generate($$"""
            {
              "MessageFilter": { "type": "object", "properties": {} },
              "Request": {
                "type": "object",
                "required": ["filter"],
                "properties": {
                  "filter": { "oneOf": [{{variants}}] }
                }
              }
            }
            """);

        Assert.Contains("filter: MessageFilter | null;", output);
    }

    [Fact]
    public void RejectsNullableReferenceOneOfWhenTargetAlreadyAcceptsNull()
    {
        var exception = Assert.Throws<UnsupportedSchemaException>(() =>
            GeneratorTestHelper.Generate("""
                {
                  "Maybe": {
                    "type": ["object", "null"],
                    "properties": {}
                  },
                  "Value": {
                    "oneOf": [
                      { "type": "null" },
                      { "$ref": "#/components/schemas/Maybe" }
                    ]
                  }
                }
                """));

        Assert.Contains("reference target that accepts null", exception.Message);
        Assert.Contains("Path: components.schemas.Value.oneOf[1].$ref", exception.Message);
    }

    [Fact]
    public void PreservesReferencePathForMissingNullableOneOfTarget()
    {
        var exception = Assert.Throws<OpenApiDocumentException>(() =>
            GeneratorTestHelper.Generate("""
                {
                  "Value": {
                    "oneOf": [
                      { "type": "null" },
                      { "$ref": "#/components/schemas/Missing" }
                    ]
                  }
                }
                """));

        Assert.Contains("Path: components.schemas.Value.oneOf[1].$ref", exception.Message);
    }

    [Theory]
    [InlineData("{ \"type\": \"null\" }, { \"type\": \"string\" }")]
    [InlineData("{ \"$ref\": \"#/components/schemas/First\" }, { \"$ref\": \"#/components/schemas/Second\" }")]
    [InlineData("{ \"type\": \"null\" }")]
    public void RejectsOtherOneOfShapes(string variants)
    {
        var exception = Assert.Throws<UnsupportedSchemaException>(() =>
            GeneratorTestHelper.Generate($$"""
                {
                  "First": { "type": "string" },
                  "Second": { "type": "string" },
                  "Value": { "oneOf": [{{variants}}] }
                }
                """));

        Assert.Contains("Unsupported OpenAPI construct: oneOf", exception.Message);
        Assert.Contains("Path: components.schemas.Value.oneOf", exception.Message);
    }

    [Theory]
    [InlineData("oneOf")]
    [InlineData("allOf")]
    [InlineData("anyOf")]
    [InlineData("not")]
    [InlineData("discriminator")]
    public void RejectsUnsupportedCompositionConstructs(string construct)
    {
        var exception = Assert.Throws<UnsupportedSchemaException>(() =>
            GeneratorTestHelper.Generate(
                $$"""{ "PaymentDto": { "{{construct}}": [] } }"""));

        Assert.Contains($"Unsupported OpenAPI construct: {construct}", exception.Message);
        Assert.Contains("Schema: PaymentDto", exception.Message);
        Assert.Contains($"Path: components.schemas.PaymentDto.{construct}", exception.Message);
    }

    [Fact]
    public void QuotesInvalidAndReservedPropertyNamesWithoutRenamingThem()
    {
        var output = GeneratorTestHelper.Generate("""
            {
              "NamesDto": {
                "type": "object",
                "required": ["some-value", "default"],
                "properties": {
                  "some-value": { "type": "string" },
                  "default": { "type": "boolean" },
                  "normal": { "type": "number" }
                }
              }
            }
            """);

        Assert.Contains("\"some-value\": string;", output);
        Assert.Contains("\"default\": boolean;", output);
        Assert.Contains("normal?: number;", output);
    }

    [Fact]
    public void RejectsInvalidSchemaNames()
    {
        var document = GeneratorTestHelper.Parse(
            """{ "some-value": { "type": "string" } }""");

        var exception = Assert.Throws<GenerationException>(() =>
            new TypeScriptGenerator().Generate(document));
        Assert.Contains("safe TypeScript identifier", exception.Message);
    }

    [Fact]
    public void NamespaceSelectsAndStripsSchemaPrefix()
    {
        var document = GeneratorTestHelper.Parse("""
            {
              "Contracts.UserDto": { "type": "object", "properties": {} },
              "Infrastructure.InternalDto": { "type": "object", "properties": {} }
            }
            """);

        var output = new TypeScriptGenerator("Contracts.").Generate(document);

        Assert.Contains("export interface UserDto", output);
        Assert.DoesNotContain("InternalDto", output);
    }

    [Fact]
    public void EmptySchemaUsesUnknownOnlyForExplicitArbitraryJson()
    {
        var output = GeneratorTestHelper.Generate("""{ "JsonValue": {} }""");

        Assert.Contains("export type JsonValue = unknown;", output);
        Assert.DoesNotContain("any", output);
    }

    [Fact]
    public void GenerationIsDeterministicAndSortsTopLevelTypes()
    {
        var document = GeneratorTestHelper.Parse("""
            {
              "Zulu": { "type": "string" },
              "Alpha": { "type": "integer" }
            }
            """);
        var generator = new TypeScriptGenerator();

        var first = generator.Generate(document);
        var second = generator.Generate(document);

        Assert.Equal(first, second);
        Assert.True(
            first.IndexOf("export type Alpha", StringComparison.Ordinal) <
            first.IndexOf("export type Zulu", StringComparison.Ordinal));
        Assert.DoesNotContain('\r', first);
    }
}
