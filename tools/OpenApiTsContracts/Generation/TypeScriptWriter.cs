using System.Text;

namespace OpenApiTsContracts.Generation;

public sealed class TypeScriptWriter
{
    private readonly StringBuilder builder = new();

    public void Write(string value) => builder.Append(value);

    public void WriteLine(string value = "") => builder.Append(value).Append('\n');

    public override string ToString() => builder.ToString();
}
