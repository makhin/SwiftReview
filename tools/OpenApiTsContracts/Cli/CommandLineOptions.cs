namespace OpenApiTsContracts.Cli;

public sealed record CommandLineOptions(
    string InputPath,
    string OutputPath,
    string? NamespacePrefix,
    bool Verbose,
    bool Check)
{
    public const string Usage =
        "Usage: OpenApiTsContracts --input <openapi.json> --output <contracts.generated.ts> " +
        "[--namespace <schema-prefix>] [--verbose] [--check]";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out CommandLineOptions options,
        out string error)
    {
        string? input = null;
        string? output = null;
        string? namespacePrefix = null;
        var verbose = false;
        var check = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--input":
                    if (!TryReadValue(args, ref index, argument, ref input, out error))
                    {
                        options = null!;
                        return false;
                    }

                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, argument, ref output, out error))
                    {
                        options = null!;
                        return false;
                    }

                    break;
                case "--namespace":
                    if (!TryReadValue(args, ref index, argument, ref namespacePrefix, out error))
                    {
                        options = null!;
                        return false;
                    }

                    break;
                case "--verbose":
                    if (verbose)
                    {
                        options = null!;
                        error = "Option '--verbose' was specified more than once.";
                        return false;
                    }

                    verbose = true;
                    break;
                case "--check":
                    if (check)
                    {
                        options = null!;
                        error = "Option '--check' was specified more than once.";
                        return false;
                    }

                    check = true;
                    break;
                default:
                    options = null!;
                    error = $"Unknown option: {argument}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            options = null!;
            error = "Required option '--input' is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            options = null!;
            error = "Required option '--output' is missing.";
            return false;
        }

        options = new(input, output, namespacePrefix, verbose, check);
        error = string.Empty;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        ref string? target,
        out string error)
    {
        if (target is not null)
        {
            error = $"Option '{option}' was specified more than once.";
            return false;
        }

        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"Option '{option}' requires a value.";
            return false;
        }

        target = args[++index];
        if (string.IsNullOrWhiteSpace(target))
        {
            error = $"Option '{option}' requires a non-empty value.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
