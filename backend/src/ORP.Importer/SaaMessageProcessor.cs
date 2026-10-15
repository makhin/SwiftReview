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
    internal SaaProcessedMessage(SaaMessageDocument document, string messageType, int? branchId, int? departmentId,
        bool isMessageTypeValidated, SaaMessageDirection? direction)
    {
        Document = document;
        MessageType = messageType;
        BranchId = branchId;
        DepartmentId = departmentId;
        IsMessageTypeValidated = isMessageTypeValidated;
        Direction = direction;
    }

    public SaaMessageDocument Document { get; }
    public string MessageType { get; }
    public int? BranchId { get; }
    public int? DepartmentId { get; }
    public bool IsMessageTypeValidated { get; }
    public SaaMessageDirection? Direction { get; }
}

/// <summary>Resolves routing fields only after successful file validation.</summary>
public sealed class SaaMessageProcessor
{
    private readonly ILogger<SaaMessageProcessor> logger;
    private readonly SaaMessageParser parser;
    private readonly ISaaMessageDepartmentResolver? departmentResolver;
    private readonly ISaaMessageBranchResolver? branchResolver;
    private readonly ISaaMessageTypeResolver? messageTypeResolver;
    private readonly SaaMessageDirection? defaultDirection;

    public SaaMessageProcessor(SaaMessageParser parser, ISaaMessageDepartmentResolver? departmentResolver = null,
        ILogger<SaaMessageProcessor>? logger = null, ISaaMessageBranchResolver? branchResolver = null,
        ISaaMessageTypeResolver? messageTypeResolver = null, SaaMessageDirection? defaultDirection = null)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        this.departmentResolver = departmentResolver;
        this.branchResolver = branchResolver;
        this.messageTypeResolver = messageTypeResolver;
        if (defaultDirection.HasValue && !Enum.IsDefined(typeof(SaaMessageDirection), defaultDirection.Value))
            throw new ArgumentOutOfRangeException(nameof(defaultDirection));
        this.defaultDirection = defaultDirection;
        this.logger = logger ?? NullLogger<SaaMessageProcessor>.Instance;
    }

    public SaaProcessedMessage ParseFile(string path)
    {
        var document = parser.ParseFile(path);
        var messageType = ResolveMessageType(document);
        var branchId = ResolveBranch(document);
        var departmentId = departmentResolver?.ResolveDepartmentId(document);
        if (departmentResolver is not null && (departmentId is null || departmentId <= 0))
        {
            ImporterLog.DepartmentResolutionFailed(logger, document.FilePath);
            throw new SaaMessageFormatException(document.FilePath, null,
                "Department rules did not resolve a positive DepartmentId.");
        }
        var direction = document.Header.Direction ?? defaultDirection;
        if (document.Header.Direction is null && direction.HasValue)
            ImporterLog.DirectionDefaulted(logger, document.FilePath, direction.Value);
        return new SaaProcessedMessage(document, messageType, branchId, departmentId, messageTypeResolver is not null, direction);
    }

    private string ResolveMessageType(SaaMessageDocument document)
    {
        if (messageTypeResolver is null)
            return document.Header.MessageType;
        try
        {
            var messageType = messageTypeResolver.ResolveMessageType(document.Header.MessageType);
            if (messageType is null || string.IsNullOrWhiteSpace(messageType))
                throw new FormatException("Header field 'MessageType' is not in the allowed message types.");
            ImporterLog.MessageTypeResolved(logger, document.FilePath);
            return messageType;
        }
        catch (FormatException exception)
        {
            ImporterLog.MessageTypeResolutionFailed(logger, document.FilePath);
            throw new SaaMessageFormatException(document.FilePath, 1, exception.Message, exception);
        }
    }

    private int? ResolveBranch(SaaMessageDocument document)
    {
        if (branchResolver is null)
            return null;
        try
        {
            var branchId = branchResolver.ResolveBranchId(document.Header.Branch);
            if (branchId is null || branchId <= 0)
                throw new FormatException("Header field 'Branch' has no matching positive BranchId in the branch directory.");
            ImporterLog.BranchResolved(logger, document.FilePath, branchId.Value);
            return branchId;
        }
        catch (FormatException exception)
        {
            ImporterLog.BranchResolutionFailed(logger, document.FilePath);
            throw new SaaMessageFormatException(document.FilePath, 1, exception.Message, exception);
        }
    }
}
