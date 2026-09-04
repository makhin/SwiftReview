using System.Text.Json;

namespace OpenApiTsContracts.Generation;

internal static class TypeScriptNames
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "any", "as", "async", "await", "bigint", "boolean", "break", "case", "catch",
        "class", "const", "constructor", "continue", "debugger", "declare", "default",
        "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for",
        "from", "function", "get", "if", "implements", "import", "in", "infer", "instanceof",
        "interface", "is", "keyof", "let", "module", "namespace", "never", "new", "null",
        "number", "object", "of", "package", "private", "protected", "public", "readonly",
        "require", "return", "set", "static", "string", "super", "switch", "symbol", "this",
        "throw", "true", "try", "type", "typeof", "undefined", "unique", "unknown", "var",
        "void", "while", "with", "yield"
    };

    public static bool IsSafeIdentifier(string value) =>
        value.Length > 0 &&
        (IsAsciiLetter(value[0]) || value[0] is '_' or '$') &&
        value.Skip(1).All(character =>
            IsAsciiLetter(character) || char.IsAsciiDigit(character) || character is '_' or '$') &&
        !ReservedWords.Contains(value);

    public static string PropertyName(string value) =>
        IsSafeIdentifier(value) ? value : JsonSerializer.Serialize(value);

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
