using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Abstractions;

public interface IClock { DateTimeOffset UtcNow { get; } }
public interface ICorrelationContext { string CorrelationId { get; } }
public interface ICurrentUser { int UserId { get; } string UserName { get; } }

public interface IORPStore
{
    Task<Message?> FindMessageAsync(long id, CancellationToken cancellationToken);
    Task<MessageSourceDto?> FindMessageSourceAsync(long id, CancellationToken cancellationToken);
    Task<WorkflowDefinition?> FindWorkflowAsync(int id, CancellationToken cancellationToken);
    Task<List<Review>> GetReviewsAsync(long messageId, CancellationToken cancellationToken);
    Task<Assignment?> GetActiveAssignmentAsync(long messageId, CancellationToken cancellationToken);
    void AddReview(Review review);
    void AddAssignment(Assignment assignment);
    void AddAudit(AuditEvent auditEvent);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record MessageSourceDto(long MessageId, string ExternalId, string MessageType, int BranchId,
    int DepartmentId, DateTimeOffset ReceivedAt, string Sender, string Receiver, string? Account,
    string? Currency, decimal? Amount, string? Reference);

public interface IMessageQueries
{
    Task<MessageDetailsDto?> GetAsync(long id, UserAccess access, CancellationToken cancellationToken);
    Task<PagedResult<MessageListItemDto>> SearchAsync(MessageSearchRequest request, UserAccess access, CancellationToken cancellationToken);
    Task<DashboardSummaryDto> DashboardAsync(UserAccess access, CancellationToken cancellationToken);
    Task<PagedResult<AuditEventDto>?> AuditAsync(long messageId, AuditTrailRequest request, UserAccess access,
        CancellationToken cancellationToken);
}

public interface IUserAccessService
{
    Task<UserAccess?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
    Task<UserAccess?> GetByIdAsync(int userId, CancellationToken cancellationToken);
}

public interface IAutomaticAssignmentQueries
{
    Task<int?> SelectAssigneeAsync(long messageId, int branchId, int departmentId, int reviewLevel,
        IReadOnlyCollection<int> excludedUserIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<UnassignedMessageCursor>> GetUnassignedMessagesAsync(UnassignedMessageCursor? after, int take,
        CancellationToken cancellationToken);
}

public sealed record UnassignedMessageCursor(DateTimeOffset ReceivedAt, long MessageId);

public sealed class ConcurrentUpdateException(string message, Exception innerException)
    : Exception(message, innerException);

public interface IWorkflowResolver
{
    Task<WorkflowDefinition> ResolveAsync(string messageType, int departmentId, int branchId,
        CancellationToken cancellationToken);
}

public interface IReferenceDataQueries
{
    Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(UserAccess access, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(UserAccess access, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceItemDto>> GetBranchesAsync(UserAccess access, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceItemDto>> GetDepartmentsAsync(UserAccess access, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetMessageTypesAsync(UserAccess access, CancellationToken cancellationToken);
}

public sealed record UserAccess(int UserId, string UserName, IReadOnlySet<string> Permissions,
    IReadOnlySet<int> BranchIds, IReadOnlySet<int> DepartmentIds)
{
    public bool HasAllDepartmentAccess => Permissions.Contains(Domain.Identity.Permissions.MessageAccessAllDepartments);
    public bool CanAccess(int branchId, int departmentId) =>
        BranchIds.Contains(branchId) && (HasAllDepartmentAccess || DepartmentIds.Contains(departmentId));
}

public sealed class ResourceNotFoundException(string message) : Exception(message);
