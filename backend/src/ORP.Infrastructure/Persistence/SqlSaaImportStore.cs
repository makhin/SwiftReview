using System.Text.Json;
using ORP.Domain.Auditing;
using ORP.Domain.Workflows;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Messages;
using ORP.Importer;

namespace ORP.Infrastructure.Persistence;

/// <summary>Persists file dispositions and atomically registers routed messages.</summary>
public sealed class SqlSaaImportStore(string connectionString, ILogger<SqlSaaImportStore>? logger = null,
    IClock? clock = null) : ISaaImportStore
{
    private readonly IClock clock = clock ?? new SystemClock();
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<SqlSaaImportStore> logger = logger ?? NullLogger<SqlSaaImportStore>.Instance;
    private readonly DbContextOptions<ORPDbContext> options = new DbContextOptionsBuilder<ORPDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp"))
        .Options;

    public async Task<SaaFileImportResult?> FindAsync(string relativePath, string contentHash, bool retryIgnored,
        CancellationToken cancellationToken = default)
    {
        await using var db = new ORPDbContext(options);
        var imported = await FindImportedAsync(db, relativePath, contentHash, cancellationToken);
        if (imported.HasValue) return LogResult(relativePath, contentHash, imported.Value);
        if (!retryIgnored && await db.IgnoredImportFiles.AsNoTracking().AnyAsync(
                file => file.FilePath == relativePath && file.ContentHash == contentHash, cancellationToken))
            return LogResult(relativePath, contentHash, SaaFileImportResult.AlreadyIgnored);
        return null;
    }

    public async Task<SaaFileImportResult> IgnoreAsync(SaaSourceFileSnapshot file,
        CancellationToken cancellationToken = default)
    {
        await using var db = new ORPDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePathLockAsync(db, file.RelativePath, cancellationToken);
        var imported = await FindImportedAsync(db, file.RelativePath, file.ContentHash, cancellationToken);
        if (imported.HasValue) return LogResult(file.RelativePath, file.ContentHash, imported.Value);
        var ignored = await db.IgnoredImportFiles.SingleOrDefaultAsync(
            row => row.FilePath == file.RelativePath, cancellationToken);
        if (ignored is not null && ignored.ContentHash == file.ContentHash)
            return LogResult(file.RelativePath, file.ContentHash, SaaFileImportResult.AlreadyIgnored);
        if (ignored is null)
        {
            ignored = new IgnoredImportFile { FilePath = file.RelativePath };
            db.IgnoredImportFiles.Add(ignored);
        }
        ignored.ContentHash = file.ContentHash;
        ignored.IgnoredAtUtc = clock.UtcNow;
        ignored.Reason = "NoDepartmentRule";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LogResult(file.RelativePath, file.ContentHash, SaaFileImportResult.Ignored);
    }

    // SQL CHECKSUM uses the same collation as the path index. Equal paths share a lock;
    // checksum collisions only serialize unrelated files and cannot merge their records.
    private static Task AcquirePathLockAsync(ORPDbContext db, string path, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
DECLARE @resource nvarchar(255) = N'orp-saa-file:' + CONVERT(nvarchar(20), CHECKSUM({path} COLLATE Latin1_General_100_CI_AS));
EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode=N'Exclusive',
    @LockOwner=N'Transaction', @LockTimeout=15000;
IF @result < 0 THROW 51000, 'Cannot acquire SAA import file lock.', 1;", cancellationToken);

    private static async Task<SaaFileImportResult?> FindImportedAsync(ORPDbContext db, string path, string hash,
        CancellationToken cancellationToken)
    {
        var existing = await db.SwiftMessages.AsNoTracking().Where(row => row.FilePath == path)
            .Select(row => row.ContentHash).SingleOrDefaultAsync(cancellationToken);
        return existing is null ? null : CompareHash(existing, hash);
    }

    public Task<SaaFileImportResult> RegisterAsync(SaaSourceFileSnapshot file, SaaProcessedMessage processed,
        CancellationToken cancellationToken = default)
    {
        ValidateRouting(processed);
        if (processed.Document.FilePath != file.FilePath || processed.Document.ContentHash != file.ContentHash)
            throw new ArgumentException("The routed document must belong to the source snapshot.", nameof(processed));
        return RegisterCoreAsync(file, processed, cancellationToken);
    }

    private async Task<SaaFileImportResult> RegisterCoreAsync(SaaSourceFileSnapshot file, SaaProcessedMessage processed,
        CancellationToken cancellationToken)
    {
        var record = CreateRecord(processed.Document, file.RootDirectory, processed.Direction);
        await using var db = new ORPDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePathLockAsync(db, file.RelativePath, cancellationToken);
        var imported = await FindImportedAsync(db, file.RelativePath, file.ContentHash, cancellationToken);
        if (imported.HasValue) return LogResult(file.RelativePath, file.ContentHash, imported.Value);
        if (!await db.Branches.AnyAsync(branch => branch.Id == processed.BranchId, cancellationToken))
            throw new FormatException("Resolved BranchId does not exist in the branch directory.");
        var departmentCount = await db.Departments.CountAsync(department => processed.DepartmentIds.Contains(department.Id), cancellationToken);
        if (departmentCount != processed.DepartmentIds.Count)
            throw new FormatException("A resolved department does not exist in the department directory.");
        var routing = await new WorkflowResolver(db).ResolveAsync(processed.MessageType,
            (MessageDirection)processed.Direction, processed.DepartmentIds, processed.BranchId, cancellationToken);
        var workflow = routing.Workflow;
        db.SwiftMessages.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        var receivedAt = clock.UtcNow;
        var message = new Message(record.MessageId, workflow.Id, processed.BranchId,
            processed.DepartmentIds, receivedAt);
        message.ApplyRouting(workflow, routing.ProcessingDepartmentIds);
        db.Messages.Add(message);
        var details = JsonSerializer.Serialize(new AuditEventDetailsDto(WorkflowDefinitionId: workflow.Id,
            BranchId: message.BranchId, DepartmentIds: message.DepartmentIds), AuditJsonOptions);
        db.AuditEvents.Add(new AuditEvent(message, AuditEventType.MessageRegistered, null, receivedAt,
            null, message.State, details, Guid.NewGuid().ToString("N")));
        await db.IgnoredImportFiles.Where(row => row.FilePath == file.RelativePath).ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        InfrastructureLog.SwiftMessageRegistered(logger, file.RelativePath, message.Id, message.BranchId!.Value,
            string.Join(",", message.DepartmentIds), message.WorkflowDefinitionId);
        return LogResult(file.RelativePath, file.ContentHash, SaaFileImportResult.Imported);
    }

    internal static void ValidateRouting(SaaProcessedMessage processed)
    {
        ArgumentNullException.ThrowIfNull(processed);
        if (processed.BranchId <= 0)
            throw new FormatException("A resolved positive BranchId is required before registration.");
        if (processed.DepartmentIds.Count == 0 || processed.DepartmentIds.Any(id => id <= 0) ||
            processed.DepartmentIds.Distinct().Count() != processed.DepartmentIds.Count)
            throw new FormatException("Unique positive department IDs are required before registration.");
    }

    private SaaFileImportResult LogResult(string path, string hash, SaaFileImportResult result)
    {
        InfrastructureLog.SwiftMessageImported(logger,
            result == SaaFileImportResult.Conflict ? LogLevel.Warning : LogLevel.Information,
            path, hash, result);
        return result;
    }

    internal static SwiftMessageRecord CreateRecord(SaaMessageDocument document, string rootDirectory,
        SaaMessageDirection? defaultDirection = null)
    {
        var path = SaaSourceFileReader.GetRelativePath(document.FilePath, rootDirectory);

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
            Direction = (document.Header.Direction ?? defaultDirection) switch
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

    private static SaaFileImportResult CompareHash(string saved, string current) =>
        string.Equals(saved, current, StringComparison.OrdinalIgnoreCase)
            ? SaaFileImportResult.AlreadyImported
            : SaaFileImportResult.Conflict;

    private static void EnsureLength(string? value, int maximum, string field)
    {
        if (value?.Length > maximum)
            throw new FormatException($"{field} is longer than {maximum} characters.");
    }
}
