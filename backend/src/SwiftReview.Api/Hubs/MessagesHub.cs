using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SwiftReview.Application.Abstractions;

namespace SwiftReview.Api.Hubs;

[Authorize]
public sealed class MessagesHub(IMessageQueries queries, IUserAccessService accessService) : Hub
{
    public async Task JoinBranch(int branchId)
    {
        var access = await CurrentAccess();
        if (!access.BranchIds.Contains(branchId)) throw new HubException("Branch access denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
    }
    public async Task JoinDepartment(int departmentId)
    {
        var access = await CurrentAccess();
        if (!access.DepartmentIds.Contains(departmentId)) throw new HubException("Department access denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"department:{departmentId}");
    }
    public async Task JoinMessage(long messageId)
    {
        var access = await CurrentAccess();
        if (await queries.GetAsync(messageId, access, Context.ConnectionAborted) is null) throw new HubException("Message access denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"message:{messageId}");
    }
    private async Task<UserAccess> CurrentAccess()
    {
        var name = Context.User?.Identity?.Name ?? throw new HubException("Not authenticated.");
        return await accessService.GetByUserNameAsync(name, Context.ConnectionAborted) ?? throw new HubException("Unknown user.");
    }
}

public sealed record MessageChangedNotification(string Type, long MessageId, int BranchId,
    int DepartmentId, string EventId);
