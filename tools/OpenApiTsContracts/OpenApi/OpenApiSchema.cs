namespace OpenApiTsContracts.OpenApi;

public sealed record OpenApiContractDocument(
    string Version,
    IReadOnlyDictionary<string, OpenApiSchema> Schemas);

public enum OpenApiSchemaKind
{
    Any,
    String,
    Integer,
    Number,
    Boolean,
    Object,
    Array,
    Reference,
    Dictionary,
    Union
}

public enum OpenApiLiteralKind
{
    String,
    Number,
    Boolean,
    Null
}

public sealed record OpenApiLiteral(OpenApiLiteralKind Kind, string TypeScriptText);

public sealed record OpenApiProperty(string Name, OpenApiSchema Schema, bool IsRequired);

public sealed record OpenApiSchema(
    OpenApiSchemaKind Kind,
    bool IsNullable,
    string Path,
    string SchemaName,
    string? ReferenceName = null,
    IReadOnlyList<OpenApiProperty>? Properties = null,
    OpenApiSchema? Items = null,
    OpenApiSchema? AdditionalProperties = null,
    IReadOnlyList<OpenApiLiteral>? EnumValues = null,
    IReadOnlyList<OpenApiSchema>? Variants = null,
    bool RequiresNonNullableReferenceTarget = false)
{
    public IReadOnlyList<OpenApiProperty> ObjectProperties { get; } =
        Properties ?? Array.Empty<OpenApiProperty>();

    public IReadOnlyList<OpenApiSchema> UnionVariants { get; } =
        Variants ?? Array.Empty<OpenApiSchema>();
}
