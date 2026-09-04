using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts.Generation;

internal sealed class SchemaNameMap
{
    private readonly IReadOnlyDictionary<string, string> names;

    public SchemaNameMap(OpenApiContractDocument document, string? namespacePrefix)
    {
        var selected = document.Schemas.Keys
            .Where(name => namespacePrefix is null || name.StartsWith(namespacePrefix, StringComparison.Ordinal))
            .Select(name => new
            {
                Source = name,
                Output = namespacePrefix is null ? name : name[namespacePrefix.Length..]
            })
            .ToArray();

        if (selected.Length == 0 && namespacePrefix is not null)
        {
            throw new GenerationException(
                $"No schemas match namespace prefix '{namespacePrefix}'.");
        }

        foreach (var item in selected)
        {
            if (!TypeScriptNames.IsSafeIdentifier(item.Output))
            {
                throw new GenerationException(
                    $"Schema name '{item.Source}' cannot be emitted as a safe TypeScript identifier" +
                    (namespacePrefix is null ? "." : $" after removing prefix '{namespacePrefix}'."));
            }
        }

        var collision = selected
            .GroupBy(item => item.Output, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
        {
            throw new GenerationException(
                $"Schemas {string.Join(", ", collision.Select(item => $"'{item.Source}'"))} " +
                $"map to the same TypeScript name '{collision.Key}'.");
        }

        names = selected.ToDictionary(item => item.Source, item => item.Output, StringComparer.Ordinal);
    }

    public IEnumerable<(string SourceName, string OutputName)> Entries =>
        names.Select(pair => (pair.Key, pair.Value));

    public string ResolveReference(string sourceName)
    {
        if (!names.TryGetValue(sourceName, out var outputName))
        {
            throw new GenerationException(
                $"Schema reference '{sourceName}' is outside the schemas selected by --namespace.");
        }

        return outputName;
    }
}
