namespace OpenApiTsContracts.OpenApi;

public sealed class OpenApiDocumentException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class UnsupportedSchemaException : Exception
{
    public UnsupportedSchemaException(string construct, string schemaName, string path)
        : base(
            $"Unsupported OpenAPI construct: {construct}{Environment.NewLine}" +
            $"Schema: {schemaName}{Environment.NewLine}" +
            $"Path: {path}")
    {
    }
}
