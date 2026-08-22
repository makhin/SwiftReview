using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Identity;

namespace SwiftReview.Application.ReferenceData;

public sealed class GetWorkflowsHandler(IReferenceDataQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return await queries.GetWorkflowsAsync(access, ct);
    }
}

public sealed class GetUsersHandler(IReferenceDataQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<UserSummaryDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageAssign)) throw new UnauthorizedAccessException();
        return await queries.GetUsersAsync(access, ct);
    }
}
