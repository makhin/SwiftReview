using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ORP.Importer;

public interface ISaaMessageDepartmentResolver
{
    SaaDepartmentResolution? Resolve(int branchId, string messageType, SaaMessageDirection direction);
}

public enum SaaRoutingStatus { Matched, NoDepartmentRule }

public sealed class SaaProcessedMessage
{
    internal SaaProcessedMessage(SaaMessageDocument document, int branchId, int? departmentId, SaaMessageDirection direction)
    {
        Document = document;
        BranchId = branchId;
        DepartmentId = departmentId;
        Direction = direction;
    }

    public SaaMessageDocument Document { get; }
    public string MessageType => Document.Header.MessageType;
    public int BranchId { get; }
    public int? DepartmentId { get; }
    public SaaMessageDirection Direction { get; }
    public SaaRoutingStatus Status => DepartmentId.HasValue ? SaaRoutingStatus.Matched : SaaRoutingStatus.NoDepartmentRule;
}

/// <summary>Routes an already parsed document without file or database access.</summary>
public sealed class SaaMessageProcessor
{
    private readonly ILogger<SaaMessageProcessor> logger;
    private readonly ISaaMessageDepartmentResolver departmentResolver;
    private readonly ISaaMessageBranchResolver branchResolver;
    private readonly SaaMessageDirection? defaultDirection;

    public SaaMessageProcessor(ISaaMessageDepartmentResolver departmentResolver, ISaaMessageBranchResolver branchResolver,
        ILogger<SaaMessageProcessor>? logger = null, SaaMessageDirection? defaultDirection = null)
    {
        this.departmentResolver = departmentResolver ?? throw new ArgumentNullException(nameof(departmentResolver));
        this.branchResolver = branchResolver ?? throw new ArgumentNullException(nameof(branchResolver));
        if (defaultDirection.HasValue && !Enum.IsDefined(typeof(SaaMessageDirection), defaultDirection.Value))
            throw new ArgumentOutOfRangeException(nameof(defaultDirection));
        this.defaultDirection = defaultDirection;
        this.logger = logger ?? NullLogger<SaaMessageProcessor>.Instance;
    }

    public SaaProcessedMessage Process(SaaMessageDocument document)
    {
        var branchId = ResolveBranch(document);
        var direction = document.Header.Direction ?? defaultDirection;
        if (document.Header.Direction is null && direction.HasValue)
            ImporterLog.DirectionDefaulted(logger, document.FilePath, direction.Value);
        try
        {
            if (direction is null)
                throw new FormatException("Direction is required to resolve DepartmentId.");
            var resolution = departmentResolver.Resolve(branchId, document.Header.MessageType, direction.Value);
            if (resolution is null)
            {
                ImporterLog.DepartmentRuleNotMatched(logger, document.FilePath, branchId,
                    document.Header.MessageType, direction.Value);
                return new SaaProcessedMessage(document, branchId, null, direction.Value);
            }
            if (resolution.DepartmentId <= 0)
                throw new FormatException("Department rules returned an invalid DepartmentId.");
            ImporterLog.DepartmentResolved(logger, document.FilePath, resolution.DepartmentId,
                string.Join(",", resolution.RuleIds), branchId, document.Header.MessageType, direction.Value);
            return new SaaProcessedMessage(document, branchId, resolution.DepartmentId, direction.Value);
        }
        catch (FormatException exception)
        {
            ImporterLog.DepartmentResolutionFailed(logger, document.FilePath, exception.Message);
            throw new SaaMessageFormatException(document.FilePath, 1, exception.Message, exception);
        }
    }

    private int ResolveBranch(SaaMessageDocument document)
    {
        try
        {
            var branchId = branchResolver.ResolveBranchId(document.Header.Branch);
            if (branchId is null || branchId <= 0)
                throw new FormatException("Header field 'Branch' has no matching positive BranchId in the branch directory.");
            ImporterLog.BranchResolved(logger, document.FilePath, branchId.Value);
            return branchId.Value;
        }
        catch (FormatException exception)
        {
            ImporterLog.BranchResolutionFailed(logger, document.FilePath, document.Header.Branch, exception.Message);
            throw new SaaMessageFormatException(document.FilePath, 1, exception.Message, exception);
        }
    }
}
