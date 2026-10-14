using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ORP.Importer;

/// <summary>Application-specific rules may inspect both the header and the body.</summary>
public interface ISaaMessageDepartmentResolver
{
    int? ResolveDepartmentId(SaaMessageDocument document);
}

public sealed class SaaProcessedMessage
{
    internal SaaProcessedMessage(SaaMessageDocument document, int? departmentId)
    {
        Document = document;
        DepartmentId = departmentId;
    }

    public SaaMessageDocument Document { get; }
    public int? DepartmentId { get; }
}

/// <summary>Runs optional department rules only after successful file validation.</summary>
public sealed class SaaMessageProcessor
{
    private readonly ILogger<SaaMessageProcessor> logger;
    private readonly SaaMessageParser parser;
    private readonly ISaaMessageDepartmentResolver? departmentResolver;

    public SaaMessageProcessor(SaaMessageParser parser, ISaaMessageDepartmentResolver? departmentResolver = null,
        ILogger<SaaMessageProcessor>? logger = null)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        this.departmentResolver = departmentResolver;
        this.logger = logger ?? NullLogger<SaaMessageProcessor>.Instance;
    }

    public SaaProcessedMessage ParseFile(string path)
    {
        var document = parser.ParseFile(path);
        var departmentId = departmentResolver?.ResolveDepartmentId(document);
        if (departmentResolver is not null && (departmentId is null || departmentId <= 0))
        {
            ImporterLog.DepartmentResolutionFailed(logger, document.FilePath);
            throw new SaaMessageFormatException(document.FilePath, null,
                "Department rules did not resolve a positive DepartmentId.");
        }
        return new SaaProcessedMessage(document, departmentId);
    }
}
