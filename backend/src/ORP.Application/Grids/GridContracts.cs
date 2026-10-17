using ORP.Application.ReferenceData;
using ORP.Domain.Messages;

namespace ORP.Application.Grids;

public sealed class MessageGridRowDto
{
    public string Version { get; init; } = string.Empty;
    public ORP.Domain.Workflows.ConfirmationMode ConfirmationMode { get; set; }
    public bool CanConfirm { get; set; }
    public int? ConfirmationLevel { get; set; }
    public required long Id { get; init; }
    public required string ExternalId { get; init; }
    public required string Sender { get; init; }
    public required string Receiver { get; init; }
    public required MessageDirection? Direction { get; init; }
    public required string MessageType { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required int[] DepartmentIds { get; init; }
    public required string DepartmentNames { get; init; }
    public required MessageState State { get; init; }
    public string StateName => MessageStates.GetName(State);
    public required DateTimeOffset ReceivedAt { get; init; }
    public required int? CurrentAssigneeId { get; init; }
    public required string? CurrentAssigneeName { get; init; }
    public required long? ActiveReviewId { get; init; }
    public required int? ActiveReviewLevel { get; init; }
    public required int? ActiveReviewerId { get; init; }
    public required string? ActiveReviewerName { get; init; }
    public required long? UndoReviewId { get; init; }
    public required int WorkflowDefinitionId { get; init; }
    public required string WorkflowDefinitionName { get; init; }
    public bool CanReview { get; set; }
    public bool RejectAllowed { get; init; }
    public bool CanAssign { get; init; }
    public bool CanViewAudit { get; init; }
    public required bool CanChangeWorkflow { get; init; }
    public required int[] RequiredReviewLevels { get; init; }
}

public static class MessageAssignmentScopes
{
    public const string Mine = "mine";
    public const string Departments = "departments";
    public const string Assignable = "assignable";
}
