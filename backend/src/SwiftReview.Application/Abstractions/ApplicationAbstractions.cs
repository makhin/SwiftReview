using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;

namespace SwiftReview.Application.Abstractions;

public interface IClock { DateTimeOffset UtcNow { get; } }
public interface ICorrelationContext { string CorrelationId { get; } }
public interface ICurrentUser { int UserId { get; } string UserName { get; } }

public interface ISwiftReviewStore
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
    Task<IReadOnlyList<AuditEventDto>> AuditAsync(long messageId, UserAccess access, CancellationToken cancellationToken);
}

public interface IUserAccessService
{
    Task<UserAccess?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
    Task<UserAccess?> GetByIdAsync(int userId, CancellationToken cancellationToken);
}

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
    public bool CanAccess(int branchId, int departmentId) => BranchIds.Contains(branchId) && DepartmentIds.Contains(departmentId);
}

public sealed class ResourceNotFoundException(string message) : Exception(message);
