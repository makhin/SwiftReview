using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

namespace ORP.Application.ReferenceData;

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
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return await queries.GetUsersAsync(access, ct);
    }
}

public sealed class GetBranchesHandler(IReferenceDataQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<ReferenceItemDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return await queries.GetBranchesAsync(access, ct);
    }
}

public sealed class GetDepartmentsHandler(IReferenceDataQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<ReferenceItemDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return await queries.GetDepartmentsAsync(access, ct);
    }
}

public sealed class GetMessageTypesHandler(IReferenceDataQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<string>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return await queries.GetMessageTypesAsync(access, ct);
    }
}

public sealed class GetMessageStatesHandler(IUserAccessService users, ICurrentUser current)
{
    private static readonly IReadOnlyList<MessageStateReferenceDto> States =
    [
        State(MessageState.New, "New"),
        State(MessageState.Assigned, "Assigned"),
        State(MessageState.FirstReviewInProgress, "First review in progress"),
        State(MessageState.WaitingForSecondReview, "Waiting for second review"),
        State(MessageState.SecondReviewInProgress, "Second review in progress"),
        State(MessageState.WaitingForThirdReview, "Waiting for third review"),
        State(MessageState.ThirdReviewInProgress, "Third review in progress"),
        State(MessageState.Completed, "Completed"),
        State(MessageState.Rejected, "Rejected")
    ];

    public async Task<IReadOnlyList<MessageStateReferenceDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return States;
    }

    private static MessageStateReferenceDto State(MessageState state, string label) => new(state.ToString(), label);
}
