using System.Globalization;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Messages.Get;

public sealed record MessageViewDto(string Id, string Reference, string MessageType, string Sender,
    string Receiver, MessageDirection? Direction, string Department, string Status, string Branch,
    DateTimeOffset ReceivedAt, IReadOnlyList<MessageApprovalDto> Approvals,
    IReadOnlyList<MessageAuditDto> AuditLog, string? RawText);
public sealed record MessageApprovalDto(int Level, string Label, string Status, string? Assignee,
    string? Approver, string? Role, string? Comment, DateTimeOffset? At);
public sealed record MessageAuditDto(string Id, DateTimeOffset At, string User, string? Role,
    string Type, string Action, string? Detail);
public sealed record MessageViewUser(string Name, string? Roles);
public sealed record MessageViewData(IReadOnlyList<int> Levels, IReadOnlyList<Review> Reviews,
    IReadOnlyList<AuditEventDto> Audit, IReadOnlyDictionary<int, MessageViewUser> Users);

public interface IMessageViewQueries
{
    Task<MessageViewDto?> GetAsync(long id, UserAccess access, CancellationToken ct);
}

public sealed class GetMessageViewHandler(IMessageViewQueries views,
    IUserAccessService users, ICurrentUser current)
{
    public async Task<MessageViewDto> HandleAsync(long id, CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.AuditView)) throw new UnauthorizedAccessException();
        return await views.GetAsync(id, access, ct)
            ?? throw new ResourceNotFoundException("Message was not found or is outside the user's access scope.");
    }
}

public static class MessageViewAdapter
{
    public static MessageViewDto Map(MessageDetailsDto message, MessageViewData data)
    {
        MessageViewUser? User(int? id) => id is { } value ? data.Users.GetValueOrDefault(value) : null;
        var currentLevel = message.State is MessageState.Completed or MessageState.Rejected or MessageState.ViewOnly
            ? 0 : data.Levels.FirstOrDefault(level => !data.Reviews.Any(r => r.Level == level && r.Status == ReviewStatus.Approved));
        var approvals = data.Levels.Select(level =>
        {
            var decision = data.Reviews.Where(r => r.Level == level &&
                    (r.Status == ReviewStatus.Approved || r.Status == ReviewStatus.Rejected))
                .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).FirstOrDefault();
            var active = data.Reviews.SingleOrDefault(r => r.Level == level && r.Status == ReviewStatus.InProgress);
            var assignee = User(message.Assignments.SingleOrDefault(a => a.Level == level)?.AssignedTo
                ?? (level == currentLevel ? active?.ReviewerId ?? message.CurrentAssigneeId : null));
            var approver = User(decision?.ReviewerId);
            return new MessageApprovalDto(level, level switch
            {
                1 => "1st Approval", 2 => "2nd Approval", 3 => "3rd Approval",
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            }, decision?.Status == ReviewStatus.Approved ? "approved" : decision?.Status == ReviewStatus.Rejected
                ? "rejected" : level == currentLevel ? "pending" : "waiting",
                assignee?.Name, approver?.Name, (approver ?? assignee)?.Roles, decision?.Comment, decision?.CompletedAt);
        }).ToArray();
        var audit = data.Audit.OrderBy(a => a.Timestamp).ThenBy(a => a.Id).Select(a =>
        {
            var (type, action) = a.EventType switch
            {
                AuditEventType.MessageRegistered => ("create", "Message received"),
                AuditEventType.MessageAssigned => ("assign", "Message assigned"),
                AuditEventType.MessageReassigned => ("reassign", "Message reassigned"),
                AuditEventType.MessageUnassigned => ("unassign", "Message unassigned"),
                AuditEventType.ReviewStarted => ("start", "Review started"),
                AuditEventType.ReviewApproved => ("approve", "Review approved"),
                AuditEventType.MessageCompleted => ("complete", "Message completed"),
                AuditEventType.ReviewRejected => ("reject", "Review rejected"),
                AuditEventType.ConfirmationUndone => ("undo", "Confirmation undone"),
                AuditEventType.ReviewCancelled => ("cancel", "Review cancelled"),
                AuditEventType.MessageWorkflowChanged => ("workflowChange", "Message workflow changed"),
                AuditEventType.MessagePdfGenerated => ("pdfGenerate", "Message PDF generated"),
                _ => throw new ArgumentOutOfRangeException(nameof(a.EventType))
            };
            var details = new List<string>();
            if (a.Details.ReviewLevel is { } level) details.Add($"Level {level}");
            if (a.Details.PreviousAssigneeId is { } previousAssignee)
            {
                var name = User(previousAssignee)?.Name ?? previousAssignee.ToString(CultureInfo.InvariantCulture);
                details.Add(a.EventType == AuditEventType.MessageUnassigned
                    ? $"Unassigned from {name}" : $"Previously assigned to {name}");
            }
            if (a.Details.AssigneeId is { } assigned)
                details.Add($"Assigned to {User(assigned)?.Name ?? assigned.ToString(CultureInfo.InvariantCulture)}");
            if (a.Details.WorkflowDefinitionId is { } workflow)
                details.Add(a.Details.PreviousWorkflowDefinitionId is { } previous
                    ? $"Workflow changed from {previous} to {workflow}" : $"Workflow {workflow}");
            if (!string.IsNullOrEmpty(a.Details.Comment)) details.Add(a.Details.Comment);
            return new MessageAuditDto(a.Id.ToString(CultureInfo.InvariantCulture), a.Timestamp,
                a.Actor?.DisplayName ?? "System", a.Actor is null ? "System" : User(a.Actor.UserId)?.Roles,
                type, action, details.Count == 0 ? null : string.Join("; ", details));
        }).ToArray();
        return new(message.Id.ToString(CultureInfo.InvariantCulture), message.ExternalId, message.MessageType,
            message.Sender, message.Receiver, message.Direction, message.DepartmentNames, message.StateName,
            message.BranchName, message.ReceivedAt, approvals, audit, message.Body);
    }
}
