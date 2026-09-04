using System.Text.Json;

namespace OpenApiTsContracts.OpenApi;

public static class OpenApiDocumentReader
{
    public static async Task<OpenApiContractDocument> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(json);
    }

    public static OpenApiContractDocument Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseDocument(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new OpenApiDocumentException(
                $"Invalid OpenAPI JSON: {exception.Message}",
                exception);
        }
    }

    private static OpenApiContractDocument ParseDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new OpenApiDocumentException("Invalid OpenAPI document: root must be an object.");
        }

        if (!root.TryGetProperty("openapi", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.String)
        {
            throw new OpenApiDocumentException(
                "Invalid OpenAPI document: string property 'openapi' is required.");
        }

        var version = versionElement.GetString()!;
        if (!Version.TryParse(version, out var parsedVersion) ||
            parsedVersion.Major != 3 ||
            parsedVersion.Minor is not (0 or 1))
        {
            throw new OpenApiDocumentException(
                $"Unsupported OpenAPI version '{version}'. Expected 3.0.x or 3.1.x.");
        }

        var schemasElement = GetRequiredObject(root, "components", "components");
        schemasElement = GetRequiredObject(schemasElement, "schemas", "components.schemas");

        var schemas = new Dictionary<string, OpenApiSchema>(StringComparer.Ordinal);
        foreach (var schemaProperty in schemasElement.EnumerateObject())
        {
            var path = $"components.schemas.{schemaProperty.Name}";
            if (!schemas.TryAdd(
                    schemaProperty.Name,
                    OpenApiSchemaParser.Parse(schemaProperty.Value, schemaProperty.Name, path)))
            {
                throw new OpenApiDocumentException(
                    $"Invalid OpenAPI document: duplicate schema name '{schemaProperty.Name}'.");
            }
        }

        ValidateReferences(schemas);
        return new OpenApiContractDocument(version, schemas);
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            throw new OpenApiDocumentException(
                $"Invalid OpenAPI document: '{path}' must be an object.");
        }

        return value;
    }

    private static void ValidateReferences(IReadOnlyDictionary<string, OpenApiSchema> schemas)
    {
        foreach (var schema in schemas.Values)
        {
            ValidateReferences(schema, schemas);
        }
    }

    private static void ValidateReferences(
        OpenApiSchema schema,
        IReadOnlyDictionary<string, OpenApiSchema> schemas)
    {
        if (schema.Kind == OpenApiSchemaKind.Reference)
        {
            if (!schemas.TryGetValue(schema.ReferenceName!, out var referenceTarget))
            {
                throw new OpenApiDocumentException(
                    $"Invalid local $ref '#/components/schemas/{schema.ReferenceName}'. " +
                    $"Target schema was not found. Path: {schema.Path}.$ref");
            }

            if (schema.RequiresNonNullableReferenceTarget &&
                AcceptsNull(referenceTarget, schemas, new HashSet<string>(StringComparer.Ordinal)))
            {
                throw new UnsupportedSchemaException(
                    "oneOf with a reference target that accepts null",
                    schema.SchemaName,
                    $"{schema.Path}.$ref");
            }
        }

        foreach (var property in schema.ObjectProperties)
        {
            ValidateReferences(property.Schema, schemas);
        }

        if (schema.Items is not null)
        {
            ValidateReferences(schema.Items, schemas);
        }

        if (schema.AdditionalProperties is not null)
        {
            ValidateReferences(schema.AdditionalProperties, schemas);
        }

        foreach (var variant in schema.UnionVariants)
        {
            ValidateReferences(variant, schemas);
        }
    }

    private static bool AcceptsNull(
        OpenApiSchema schema,
        IReadOnlyDictionary<string, OpenApiSchema> schemas,
        HashSet<string> visitedReferences)
    {
        if (schema.IsNullable || schema.Kind == OpenApiSchemaKind.Any)
        {
            return true;
        }

        if (schema.Kind == OpenApiSchemaKind.Reference &&
            schemas.TryGetValue(schema.ReferenceName!, out var referenceTarget) &&
            visitedReferences.Add(schema.ReferenceName!))
        {
            return AcceptsNull(referenceTarget, schemas, visitedReferences);
        }

        return schema.Kind == OpenApiSchemaKind.Union &&
               schema.UnionVariants.Any(variant =>
                   AcceptsNull(variant, schemas, visitedReferences));
    }
}
