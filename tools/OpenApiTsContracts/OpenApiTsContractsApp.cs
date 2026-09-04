using System.Text;
using OpenApiTsContracts.Cli;
using OpenApiTsContracts.Generation;
using OpenApiTsContracts.OpenApi;

namespace OpenApiTsContracts;

public static class OpenApiTsContractsApp
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        if (!CommandLineOptions.TryParse(args, out var options, out var commandLineError))
        {
            await standardError.WriteLineAsync(commandLineError);
            await standardError.WriteLineAsync(CommandLineOptions.Usage);
            return ExitCodes.InvalidCommandLine;
        }

        try
        {
            if (options.Verbose)
            {
                await standardOutput.WriteLineAsync($"Reading OpenAPI document: {options.InputPath}");
            }

            var document = await OpenApiDocumentReader.ReadAsync(options.InputPath, cancellationToken);
            var output = new TypeScriptGenerator(options.NamespacePrefix).Generate(document);

            if (options.Check)
            {
                if (!File.Exists(options.OutputPath) ||
                    !string.Equals(
                        await File.ReadAllTextAsync(options.OutputPath, cancellationToken),
                        output,
                        StringComparison.Ordinal))
                {
                    await standardError.WriteLineAsync(
                        $"Generated file is out of date: {options.OutputPath}");
                    return ExitCodes.OutOfDate;
                }

                if (options.Verbose)
                {
                    await standardOutput.WriteLineAsync("Generated contracts are current.");
                }

                return ExitCodes.Success;
            }

            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (outputDirectory is not null)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(
                options.OutputPath,
                output,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            if (options.Verbose)
            {
                await standardOutput.WriteLineAsync($"Wrote TypeScript contracts: {options.OutputPath}");
            }

            return ExitCodes.Success;
        }
        catch (UnsupportedSchemaException exception)
        {
            await standardError.WriteLineAsync(exception.Message);
            return ExitCodes.UnsupportedSchema;
        }
        catch (OpenApiDocumentException exception)
        {
            await standardError.WriteLineAsync(exception.Message);
            return ExitCodes.InvalidOpenApiDocument;
        }
        catch (Exception exception)
        {
            await standardError.WriteLineAsync($"Generation failed: {exception.Message}");
            return ExitCodes.GenerationError;
        }
    }
}

public static class ExitCodes
{
    public const int Success = 0;
    public const int InvalidCommandLine = 1;
    public const int InvalidOpenApiDocument = 2;
    public const int UnsupportedSchema = 3;
    public const int GenerationError = 4;
    public const int OutOfDate = 5;
}
