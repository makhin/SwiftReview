using System.Text.Json;
using ORP.Domain.Auditing;
using ORP.Domain.Workflows;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Infrastructure.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Messages;
using ORP.Importer;

namespace ORP.Infrastructure.Persistence;

public enum SwiftMessageImportResult
{
    Imported,
    AlreadyImported,
    Conflict
}

public sealed class SwiftMessageImporter(string connectionString, ILogger<SwiftMessageImporter>? logger = null,
    IClock? clock = null)
{
    private readonly IClock clock = clock ?? new SystemClock();
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<SwiftMessageImporter> logger = logger ?? NullLogger<SwiftMessageImporter>.Instance;

    private readonly DbContextOptions<ORPDbContext> options = new DbContextOptionsBuilder<ORPDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp"))
        .Options;

    public async Task<SaaMessageBranchResolver> LoadBranchResolverAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new ORPDbContext(options);
        var branches = await db.Branches.AsNoTracking()
            .Select(branch => new { branch.Name, branch.Id })
            .ToListAsync(cancellationToken);
        return new SaaMessageBranchResolver(branches.Select(branch =>
            new KeyValuePair<string, int>(branch.Name, branch.Id)));
    }

    public Task<SwiftMessageImportResult> SaveAsync(SaaProcessedMessage processed, string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateRouting(processed);
        return SaveCoreAsync(processed, rootDirectory, cancellationToken);
    }

    private async Task<SwiftMessageImportResult> SaveCoreAsync(SaaProcessedMessage processed, string rootDirectory,
        CancellationToken cancellationToken, bool retryOnDuplicate = true)
    {
        var document = processed.Document;
        var record = CreateRecord(document, rootDirectory);
        var path = record.FilePath;
        await using var db = new ORPDbContext(options);
        var existingHash = await db.SwiftMessages.AsNoTracking()
            .Where(message => message.FilePath == path)
            .Select(message => message.ContentHash)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingHash is not null)
            return LogResult(path, document.ContentHash, CompareHash(existingHash, document.ContentHash));

        if (!await db.Branches.AnyAsync(branch => branch.Id == processed.BranchId, cancellationToken))
            throw new FormatException("Resolved BranchId does not exist in the branch directory.");
        var departmentName = await db.Departments.Where(department => department.Id == processed.DepartmentId)
            .Select(department => department.Name).SingleOrDefaultAsync(cancellationToken);
        if (departmentName is null)
            throw new FormatException("Resolved DepartmentId does not exist in the department directory.");
        var candidates = await db.WorkflowDefinitions.AsNoTracking().Include(candidate => candidate.Steps)
            .Where(candidate => candidate.IsActive && candidate.DepartmentId == processed.DepartmentId &&
                (candidate.BranchId == null || candidate.BranchId == processed.BranchId))
            .ToListAsync(cancellationToken);
        var workflow = SelectWorkflow(candidates, processed, departmentName);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.SwiftMessages.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            var receivedAt = clock.UtcNow;
            var message = new Message(record.MessageId, workflow.Id, processed.BranchId!.Value,
                processed.DepartmentId!.Value, receivedAt);
            db.Messages.Add(message);
            var details = JsonSerializer.Serialize(new AuditEventDetailsDto(WorkflowDefinitionId: workflow.Id,
                BranchId: message.BranchId, DepartmentId: message.DepartmentId), AuditJsonOptions);
            db.AuditEvents.Add(new AuditEvent(message, AuditEventType.MessageRegistered, null, receivedAt,
                null, MessageState.New, details, Guid.NewGuid().ToString("N")));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InfrastructureLog.SwiftMessageRegistered(logger, path, message.Id, message.BranchId!.Value,
                message.DepartmentId!.Value, message.WorkflowDefinitionId);
            return LogResult(path, document.ContentHash, SwiftMessageImportResult.Imported);
        }
        catch (DbUpdateException exception) when (retryOnDuplicate &&
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A competing importer may have committed the same path after our lookup.
            await transaction.RollbackAsync(cancellationToken);
            return await SaveCoreAsync(processed, rootDirectory, cancellationToken, retryOnDuplicate: false);
        }
    }

    internal static void ValidateRouting(SaaProcessedMessage processed)
    {
        ArgumentNullException.ThrowIfNull(processed);
        if (!processed.IsMessageTypeValidated)
            throw new FormatException("MessageType must be checked against allowed types before registration.");
        if (processed.BranchId is null or <= 0)
            throw new FormatException("A resolved positive BranchId is required before registration.");
        if (processed.DepartmentId is null or <= 0)
            throw new FormatException("A resolved positive DepartmentId is required before registration.");
        if (processed.Document.Header.Direction is null)
            throw new FormatException("Message direction is required to select a workflow before registration.");
    }

    internal static WorkflowDefinition SelectWorkflow(IEnumerable<WorkflowDefinition> candidates, SaaProcessedMessage processed,
        string departmentName)
    {
        var direction = processed.Document.Header.Direction switch
        {
            SaaMessageDirection.Incoming => MessageDirection.Incoming,
            SaaMessageDirection.Outgoing => MessageDirection.Outgoing,
            _ => throw new FormatException("Message direction is required to select a workflow.")
        };
        var matches = candidates.Where(candidate => candidate.IsActive && candidate.Direction == direction &&
            candidate.DepartmentId == processed.DepartmentId &&
            (candidate.BranchId == null || candidate.BranchId == processed.BranchId) &&
            string.Equals(candidate.MessageType, processed.MessageType, StringComparison.OrdinalIgnoreCase)).ToArray();
        var scoped = matches.Where(candidate => candidate.BranchId == processed.BranchId).ToArray();
        var preferred = scoped.Length > 0 ? scoped : matches;
        if (preferred.Length != 1)
            throw new FormatException(preferred.Length == 0
                ? "No active workflow matches the message type, direction, branch and department."
                : "Multiple active workflows match the message routing fields.");
        try { _ = preferred[0].RequiredLevels(); }
        catch (ORP.Domain.Common.DomainRuleViolationException exception)
        {
            throw new FormatException("The matching workflow has invalid review steps.", exception);
        }
        if (preferred[0].Steps.Any(step => step.ReviewLevel > 2) &&
            (departmentName.Equals("CS", StringComparison.OrdinalIgnoreCase) ||
             departmentName.Equals("TFO", StringComparison.OrdinalIgnoreCase)))
            throw new FormatException("CS and TFO messages cannot use a workflow with review level 3.");
        return preferred[0];
    }

    private SwiftMessageImportResult LogResult(string path, string hash, SwiftMessageImportResult result)
    {
        InfrastructureLog.SwiftMessageImported(logger,
            result == SwiftMessageImportResult.Conflict ? LogLevel.Warning : LogLevel.Information,
            path, hash, result);
        return result;
    }

    internal static SwiftMessageRecord CreateRecord(SaaMessageDocument document, string rootDirectory)
    {
        var path = Path.GetRelativePath(Path.GetFullPath(rootDirectory), document.FilePath)
            .Replace('\\', '/');
        if (path == ".." || path.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(path))
            throw new ArgumentException("The file must be inside the SAA messages directory.", nameof(document));

        EnsureLength(path, 450, "FilePath");
        EnsureLength(document.FileName, 255, "FileName");
        EnsureLength(document.Header.Branch, 100, "Branch");
        EnsureLength(document.Header.MessageType, 100, "MessageType");
        EnsureLength(document.Header.ReferenceId, 255, "ReferenceId");
        EnsureLength(document.Header.Sender, 100, "Sender");
        EnsureLength(document.Header.Receiver, 100, "Receiver");
        EnsureLength(document.Header.Host, 255, "Host");

        return new SwiftMessageRecord
        {
            Branch = document.Header.Branch,
            MessageType = document.Header.MessageType,
            ReferenceId = document.Header.ReferenceId,
            Direction = document.Header.Direction switch
            {
                SaaMessageDirection.Incoming => MessageDirection.Incoming,
                SaaMessageDirection.Outgoing => MessageDirection.Outgoing,
                _ => null
            },
            Sender = document.Header.Sender,
            Receiver = document.Header.Receiver,
            Host = document.Header.Host,
            Time = document.Header.Time,
            Body = document.Body,
            FileName = document.FileName,
            FilePath = path,
            ContentHash = document.ContentHash
        };
    }

    private static SwiftMessageImportResult CompareHash(string saved, string current) =>
        string.Equals(saved, current, StringComparison.OrdinalIgnoreCase)
            ? SwiftMessageImportResult.AlreadyImported
            : SwiftMessageImportResult.Conflict;

    private static void EnsureLength(string? value, int maximum, string field)
    {
        if (value?.Length > maximum)
            throw new FormatException($"{field} is longer than {maximum} characters.");
    }
}
