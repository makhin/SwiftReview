using System.Globalization;
using System.Text.Json;

namespace OpenApiTsContracts.OpenApi;

public static class OpenApiSchemaParser
{
    private static readonly string[] UnsupportedKeywords =
    [
        "oneOf",
        "anyOf",
        "allOf",
        "not",
        "discriminator",
        "const",
        "prefixItems",
        "contains",
        "patternProperties",
        "unevaluatedProperties",
        "if",
        "then",
        "else",
        "dependentSchemas"
    ];

    public static OpenApiSchema Parse(JsonElement element, string schemaName, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new UnsupportedSchemaException("boolean or non-object schema", schemaName, path);
        }

        RejectUnsupportedKeywords(element, schemaName, path);
        var nullable = ReadNullable(element, schemaName, path);

        if (element.TryGetProperty("$ref", out var referenceElement))
        {
            return ParseReference(element, referenceElement, nullable, schemaName, path);
        }

        var (typeNames, typeNullable) = ReadTypes(element, schemaName, path);
        nullable |= typeNullable;

        if (typeNames is null)
        {
            if (element.TryGetProperty("enum", out _))
            {
                throw Invalid(schemaName, path, "schemas with 'enum' must declare a type");
            }

            if (HasAnyProperty(element, "properties", "required", "items", "additionalProperties"))
            {
                throw Invalid(schemaName, path, "schema structure requires an explicit type");
            }

            return new(OpenApiSchemaKind.Any, nullable, path, schemaName);
        }

        var kinds = typeNames.Select(typeName => ParseKind(typeName, schemaName, path)).ToArray();
        if (kinds.Length > 1)
        {
            if (element.TryGetProperty("enum", out _))
            {
                throw new UnsupportedSchemaException("enum on a type union", schemaName, $"{path}.enum");
            }

            if (kinds.Any(kind => kind is OpenApiSchemaKind.Object or OpenApiSchemaKind.Array) ||
                HasAnyProperty(element, "properties", "required", "items", "additionalProperties"))
            {
                throw new UnsupportedSchemaException("non-primitive type union", schemaName, $"{path}.type");
            }

            var variants = kinds
                .Select(kind => new OpenApiSchema(kind, false, path, schemaName))
                .ToArray();
            return new(
                OpenApiSchemaKind.Union,
                nullable,
                path,
                schemaName,
                Variants: variants);
        }

        var kind = kinds[0];
        var enumValues = ParseEnum(element, kind, nullable, schemaName, path);
        if (enumValues is not null && kind is OpenApiSchemaKind.Object or OpenApiSchemaKind.Array)
        {
            throw new UnsupportedSchemaException("enum on a non-primitive schema", schemaName, $"{path}.enum");
        }

        return kind switch
        {
            OpenApiSchemaKind.Object => ParseObject(element, nullable, schemaName, path),
            OpenApiSchemaKind.Array => ParseArray(element, nullable, schemaName, path),
            _ => ParsePrimitive(element, kind, nullable, enumValues, schemaName, path)
        };
    }

    private static OpenApiSchemaKind ParseKind(string typeName, string schemaName, string path) =>
        typeName switch
        {
            "string" => OpenApiSchemaKind.String,
            "integer" => OpenApiSchemaKind.Integer,
            "number" => OpenApiSchemaKind.Number,
            "boolean" => OpenApiSchemaKind.Boolean,
            "object" => OpenApiSchemaKind.Object,
            "array" => OpenApiSchemaKind.Array,
            _ => throw new UnsupportedSchemaException($"schema type '{typeName}'", schemaName, $"{path}.type")
        };

    private static OpenApiSchema ParseReference(
        JsonElement schema,
        JsonElement referenceElement,
        bool nullable,
        string schemaName,
        string path)
    {
        if (referenceElement.ValueKind != JsonValueKind.String)
        {
            throw Invalid(schemaName, $"{path}.$ref", "$ref must be a string");
        }

        if (HasAnyProperty(schema, "type", "enum", "properties", "required", "items", "additionalProperties"))
        {
            throw new UnsupportedSchemaException(
                "schema combining $ref with other structural keywords",
                schemaName,
                path);
        }

        var reference = referenceElement.GetString()!;
        const string localPrefix = "#/components/schemas/";
        if (!reference.StartsWith('#'))
        {
            throw new UnsupportedSchemaException("external $ref", schemaName, $"{path}.$ref");
        }

        if (!reference.StartsWith(localPrefix, StringComparison.Ordinal))
        {
            throw new UnsupportedSchemaException(
                "local $ref outside components.schemas",
                schemaName,
                $"{path}.$ref");
        }

        var referenceName = DecodeJsonPointerToken(reference[localPrefix.Length..], schemaName, path);
        if (referenceName.Length == 0 || referenceName.Contains('/', StringComparison.Ordinal))
        {
            throw Invalid(schemaName, $"{path}.$ref", $"invalid local schema reference '{reference}'");
        }

        return new(
            OpenApiSchemaKind.Reference,
            nullable,
            path,
            schemaName,
            ReferenceName: referenceName);
    }

    private static OpenApiSchema ParsePrimitive(
        JsonElement element,
        OpenApiSchemaKind kind,
        bool nullable,
        IReadOnlyList<OpenApiLiteral>? enumValues,
        string schemaName,
        string path)
    {
        if (HasAnyProperty(element, "properties", "required", "items", "additionalProperties"))
        {
            throw Invalid(schemaName, path, "primitive schema contains object or array keywords");
        }

        return new(kind, nullable, path, schemaName, EnumValues: enumValues);
    }

    private static OpenApiSchema ParseArray(
        JsonElement element,
        bool nullable,
        string schemaName,
        string path)
    {
        if (HasAnyProperty(element, "properties", "required", "additionalProperties"))
        {
            throw Invalid(schemaName, path, "array schema contains object keywords");
        }

        var itemSchema = element.TryGetProperty("items", out var items)
            ? Parse(items, schemaName, $"{path}.items")
            : new OpenApiSchema(
                OpenApiSchemaKind.Any,
                false,
                $"{path}.items",
                schemaName);

        return new(
            OpenApiSchemaKind.Array,
            nullable,
            path,
            schemaName,
            Items: itemSchema);
    }

    private static OpenApiSchema ParseObject(
        JsonElement element,
        bool nullable,
        string schemaName,
        string path)
    {
        if (element.TryGetProperty("items", out _))
        {
            throw Invalid(schemaName, path, "object schema contains array keyword 'items'");
        }

        var required = ParseRequired(element, schemaName, path);
        var properties = ParseProperties(element, required, schemaName, path);

        foreach (var requiredName in required)
        {
            if (!properties.Any(property => property.Name == requiredName))
            {
                throw Invalid(
                    schemaName,
                    $"{path}.required",
                    $"required property '{requiredName}' is not declared in properties");
            }
        }

        if (!element.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            return new(
                OpenApiSchemaKind.Object,
                nullable,
                path,
                schemaName,
                Properties: properties);
        }

        if (additionalProperties.ValueKind == JsonValueKind.False)
        {
            return new(
                OpenApiSchemaKind.Object,
                nullable,
                path,
                schemaName,
                Properties: properties);
        }

        if (properties.Count > 0)
        {
            throw new UnsupportedSchemaException(
                "object schemas combining properties and additionalProperties",
                schemaName,
                path);
        }

        var valueSchema = additionalProperties.ValueKind switch
        {
            JsonValueKind.True => new OpenApiSchema(
                OpenApiSchemaKind.Any,
                false,
                $"{path}.additionalProperties",
                schemaName),
            JsonValueKind.Object => Parse(
                additionalProperties,
                schemaName,
                $"{path}.additionalProperties"),
            _ => throw Invalid(
                schemaName,
                $"{path}.additionalProperties",
                "additionalProperties must be a schema or boolean")
        };

        return new(
            OpenApiSchemaKind.Dictionary,
            nullable,
            path,
            schemaName,
            AdditionalProperties: valueSchema);
    }

    private static IReadOnlyList<OpenApiProperty> ParseProperties(
        JsonElement element,
        IReadOnlySet<string> required,
        string schemaName,
        string path)
    {
        if (!element.TryGetProperty("properties", out var propertiesElement))
        {
            return Array.Empty<OpenApiProperty>();
        }

        if (propertiesElement.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(schemaName, $"{path}.properties", "properties must be an object");
        }

        var properties = new List<OpenApiProperty>();
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in propertiesElement.EnumerateObject())
        {
            if (!propertyNames.Add(property.Name))
            {
                throw Invalid(
                    schemaName,
                    $"{path}.properties",
                    $"property '{property.Name}' is duplicated");
            }

            properties.Add(new(
                property.Name,
                Parse(property.Value, schemaName, $"{path}.properties.{property.Name}"),
                required.Contains(property.Name)));
        }

        return properties;
    }

    private static IReadOnlySet<string> ParseRequired(
        JsonElement element,
        string schemaName,
        string path)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (!element.TryGetProperty("required", out var requiredElement))
        {
            return required;
        }

        if (requiredElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(schemaName, $"{path}.required", "required must be an array");
        }

        foreach (var item in requiredElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw Invalid(schemaName, $"{path}.required", "required entries must be strings");
            }

            var name = item.GetString()!;
            if (!required.Add(name))
            {
                throw Invalid(
                    schemaName,
                    $"{path}.required",
                    $"required property '{name}' is duplicated");
            }
        }

        return required;
    }

    private static IReadOnlyList<OpenApiLiteral>? ParseEnum(
        JsonElement element,
        OpenApiSchemaKind kind,
        bool nullable,
        string schemaName,
        string path)
    {
        if (!element.TryGetProperty("enum", out var enumElement))
        {
            return null;
        }

        if (enumElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(schemaName, $"{path}.enum", "enum must be an array");
        }

        var values = new List<OpenApiLiteral>();
        var uniqueValues = new HashSet<(OpenApiLiteralKind Kind, string Text)>();
        foreach (var value in enumElement.EnumerateArray())
        {
            var parsedValue = ParseEnumValue(value, kind, nullable, schemaName, path);
            if (!uniqueValues.Add((parsedValue.Kind, parsedValue.TypeScriptText)))
            {
                throw Invalid(
                    schemaName,
                    $"{path}.enum",
                    $"enum value {parsedValue.TypeScriptText} is duplicated");
            }

            values.Add(parsedValue);
        }

        if (values.Count == 0)
        {
            throw Invalid(schemaName, $"{path}.enum", "enum must contain at least one value");
        }

        return values;
    }

    private static OpenApiLiteral ParseEnumValue(
        JsonElement value,
        OpenApiSchemaKind kind,
        bool nullable,
        string schemaName,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null && nullable)
        {
            return new(OpenApiLiteralKind.Null, "null");
        }

        var valid = kind switch
        {
            OpenApiSchemaKind.String => value.ValueKind == JsonValueKind.String,
            OpenApiSchemaKind.Integer => IsInteger(value),
            OpenApiSchemaKind.Number => value.ValueKind == JsonValueKind.Number,
            OpenApiSchemaKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };

        if (!valid)
        {
            var actualType = JsonTypeName(value.ValueKind);
            var expectedType = kind.ToString().ToLowerInvariant();
            throw Invalid(
                schemaName,
                $"{path}.enum",
                $"invalid enum value. Expected values of type '{expectedType}' but found '{actualType}'");
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => new(
                OpenApiLiteralKind.String,
                JsonSerializer.Serialize(value.GetString())),
            JsonValueKind.Number => new(OpenApiLiteralKind.Number, value.GetRawText()),
            JsonValueKind.True => new(OpenApiLiteralKind.Boolean, "true"),
            JsonValueKind.False => new(OpenApiLiteralKind.Boolean, "false"),
            _ => throw new InvalidOperationException("Validated enum value was not handled.")
        };
    }

    private static bool IsInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !decimal.TryParse(
                value.GetRawText(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return false;
        }

        return number == decimal.Truncate(number);
    }

    private static (IReadOnlyList<string>? TypeNames, bool Nullable) ReadTypes(
        JsonElement element,
        string schemaName,
        string path)
    {
        if (!element.TryGetProperty("type", out var typeElement))
        {
            return (null, false);
        }

        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return (new[] { typeElement.GetString()! }, false);
        }

        if (typeElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(schemaName, $"{path}.type", "type must be a string or an array");
        }

        var typeNames = new List<string>();
        var nullable = false;
        foreach (var typeItem in typeElement.EnumerateArray())
        {
            if (typeItem.ValueKind != JsonValueKind.String)
            {
                throw Invalid(schemaName, $"{path}.type", "type array entries must be strings");
            }

            var value = typeItem.GetString();
            if (value == "null")
            {
                if (nullable)
                {
                    throw Invalid(schemaName, $"{path}.type", "type array contains duplicate 'null'");
                }

                nullable = true;
            }
            else
            {
                if (typeNames.Contains(value!, StringComparer.Ordinal))
                {
                    throw Invalid(schemaName, $"{path}.type", $"type array contains duplicate '{value}'");
                }

                typeNames.Add(value!);
            }
        }

        if (typeNames.Count == 0)
        {
            throw new UnsupportedSchemaException("null-only schema", schemaName, $"{path}.type");
        }

        return (typeNames, nullable);
    }

    private static bool ReadNullable(JsonElement element, string schemaName, string path)
    {
        if (!element.TryGetProperty("nullable", out var nullableElement))
        {
            return false;
        }

        return nullableElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Invalid(schemaName, $"{path}.nullable", "nullable must be a boolean")
        };
    }

    private static void RejectUnsupportedKeywords(
        JsonElement element,
        string schemaName,
        string path)
    {
        foreach (var keyword in UnsupportedKeywords)
        {
            if (element.TryGetProperty(keyword, out _))
            {
                throw new UnsupportedSchemaException(keyword, schemaName, $"{path}.{keyword}");
            }
        }
    }

    private static bool HasAnyProperty(JsonElement element, params string[] names) =>
        names.Any(name => element.TryGetProperty(name, out _));

    private static string DecodeJsonPointerToken(string token, string schemaName, string path)
    {
        var result = new System.Text.StringBuilder(token.Length);
        for (var index = 0; index < token.Length; index++)
        {
            if (token[index] != '~')
            {
                result.Append(token[index]);
                continue;
            }

            if (index + 1 >= token.Length || token[index + 1] is not ('0' or '1'))
            {
                throw Invalid(schemaName, $"{path}.$ref", "invalid JSON Pointer escape");
            }

            result.Append(token[++index] == '0' ? '~' : '/');
        }

        return result.ToString();
    }

    private static string JsonTypeName(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static OpenApiDocumentException Invalid(
        string schemaName,
        string path,
        string message) =>
        new(
            $"Invalid OpenAPI schema '{schemaName}': {message}.{Environment.NewLine}" +
            $"Path: {path}");
}
