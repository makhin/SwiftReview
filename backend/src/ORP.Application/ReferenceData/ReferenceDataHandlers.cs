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
        .Select(state =>
        {
            var words = System.Text.RegularExpressions.Regex.Replace(state.ToString(), "(?<!^)([A-Z])", " $1");
            return new MessageStateReferenceDto(state.ToString(), words[..1] + words[1..].ToLowerInvariant());
        }).ToArray();

    public async Task<IReadOnlyList<MessageStateReferenceDto>> HandleAsync(CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.MessageView)) throw new UnauthorizedAccessException();
        return States;
    }

}
