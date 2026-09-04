using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Generation;

internal sealed class TypeScriptTypeResolver(SchemaNameMap names)
{
    public string Resolve(OpenApiSchema schema, int indentLevel = 0)
    {
        var type = schema.EnumValues is not null
            ? string.Join(" | ", schema.EnumValues.Select(value => value.TypeScriptText))
            : schema.Kind switch
            {
                OpenApiSchemaKind.Any => "unknown",
                OpenApiSchemaKind.String => "string",
                OpenApiSchemaKind.Integer or OpenApiSchemaKind.Number => "number",
                OpenApiSchemaKind.Boolean => "boolean",
                OpenApiSchemaKind.Reference => names.ResolveReference(schema.ReferenceName!),
                OpenApiSchemaKind.Array => ResolveArray(schema, indentLevel),
                OpenApiSchemaKind.Dictionary => ResolveDictionary(schema, indentLevel),
                OpenApiSchemaKind.Object => ResolveObject(schema, indentLevel),
                OpenApiSchemaKind.Union => string.Join(
                    " | ",
                    schema.UnionVariants.Select(variant => Resolve(variant, indentLevel))),
                _ => throw new GenerationException($"Unhandled schema kind '{schema.Kind}'.")
            };

        if (schema.IsNullable &&
            (schema.EnumValues is null ||
             schema.EnumValues.All(value => value.Kind != OpenApiLiteralKind.Null)))
        {
            type += " | null";
        }

        return type;
    }

    private string ResolveArray(OpenApiSchema schema, int indentLevel)
    {
        var items = schema.Items!;
        var itemType = Resolve(items, indentLevel);
        var canUseSuffix = !items.IsNullable &&
            items.EnumValues is null &&
            items.Kind is OpenApiSchemaKind.Any or
                OpenApiSchemaKind.String or
                OpenApiSchemaKind.Integer or
                OpenApiSchemaKind.Number or
                OpenApiSchemaKind.Boolean or
                OpenApiSchemaKind.Reference;

        return canUseSuffix ? $"{itemType}[]" : $"Array<{itemType}>";
    }

    private string ResolveDictionary(OpenApiSchema schema, int indentLevel) =>
        $"Record<string, {Resolve(schema.AdditionalProperties!, indentLevel)}>";

    private string ResolveObject(OpenApiSchema schema, int indentLevel)
    {
        var writer = new TypeScriptWriter();
        writer.WriteLine("{");
        foreach (var property in schema.ObjectProperties)
        {
            var indentation = new string(' ', (indentLevel + 1) * 2);
            var optional = property.IsRequired ? string.Empty : "?";
            writer.WriteLine(
                $"{indentation}{TypeScriptNames.PropertyName(property.Name)}{optional}: " +
                $"{Resolve(property.Schema, indentLevel + 1)};");
        }

        writer.Write(new string(' ', indentLevel * 2));
        writer.Write("}");
        return writer.ToString();
    }
}
