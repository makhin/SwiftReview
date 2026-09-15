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
    private static readonly IReadOnlyList<MessageStateReferenceDto> States = Enum.GetValues<MessageState>()
        .Select(state => state switch
        {
            MessageState.New => new MessageStateReferenceDto(state.ToString(), "New",
                "Waiting for first review assignment", 1, MessageStagePhase.Waiting, null),
            MessageState.Assigned => new MessageStateReferenceDto(state.ToString(), "Assigned",
                "Waiting for first review assignment", 1, MessageStagePhase.Assigned, "Assigned for first review"),
            MessageState.FirstReviewInProgress => new MessageStateReferenceDto(state.ToString(), "First review in progress",
                "First review in progress", 1, MessageStagePhase.Reviewing, null),
            MessageState.WaitingForSecondReview => new MessageStateReferenceDto(state.ToString(), "Waiting for second review",
                "Waiting for second review assignment", 2, MessageStagePhase.Waiting, "Assigned for second review"),
            MessageState.SecondReviewInProgress => new MessageStateReferenceDto(state.ToString(), "Second review in progress",
                "Second review in progress", 2, MessageStagePhase.Reviewing, null),
            MessageState.WaitingForThirdReview => new MessageStateReferenceDto(state.ToString(), "Waiting for third review",
                "Waiting for third review assignment", 3, MessageStagePhase.Waiting, "Assigned for third review"),
            MessageState.ThirdReviewInProgress => new MessageStateReferenceDto(state.ToString(), "Third review in progress",
                "Third review in progress", 3, MessageStagePhase.Reviewing, null),
            MessageState.Completed => new MessageStateReferenceDto(state.ToString(), "Completed",
                "All required reviews completed", null, MessageStagePhase.Completed, null),
            MessageState.Rejected => new MessageStateReferenceDto(state.ToString(), "Rejected",
                "Review rejected", null, MessageStagePhase.Rejected, null),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Missing message stage metadata.")
        }).ToArray();

    public async Task<IReadOnlyList<MessageStateReferenceDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return States;
    }

}
