using FluentValidation;
using ORP.Domain.Common;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Auditing;

namespace ORP.Application.Messages.ChangeWorkflow;

public sealed class ChangeMessageWorkflowValidator : AbstractValidator<ChangeMessageWorkflowRequest>
{
    public ChangeMessageWorkflowValidator() { RuleFor(x => x.WorkflowDefinitionId).GreaterThan(0); }
}

public sealed class ChangeMessageWorkflowHandler(IORPStore store, IValidator<ChangeMessageWorkflowRequest> validator,
    IWorkflowResolver routing, ICurrentUser user, IClock clock, ICorrelationContext correlation,
    ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetCandidatesAsync(long messageId, CancellationToken ct)
    {
        var access = await authorization.RequireAsync(messageId, Permissions.WorkflowManage, ct);
        if (access.Source.Direction is null)
            throw new DomainRuleViolationException("Message direction is required to change workflow.");
        var resolved = await routing.ResolveAsync(access.Source.MessageType, access.Source.Direction.Value,
            access.Source.DepartmentIds, access.Source.BranchId, ct);
        var workflow = resolved.Workflow;
        if (access.Message.State is not (ORP.Domain.Messages.MessageState.New or ORP.Domain.Messages.MessageState.Assigned) ||
            access.Reviews.Any(review => review.Status is not (ORP.Domain.Reviews.ReviewStatus.Cancelled or ORP.Domain.Reviews.ReviewStatus.Undone)))
            return [];
        if (workflow.ConfirmationMode == ORP.Domain.Workflows.ConfirmationMode.None &&
            access.Message.CurrentAssigneeId is not null)
            return [];
        return [new WorkflowSummaryDto(workflow.Id, workflow.Code, workflow.Name, workflow.IsActive,
            workflow.Steps.Select(step => new WorkflowStepDto(step.Order, step.ReviewLevel, step.Required)).ToArray())
        {
            ConfirmationMode = workflow.ConfirmationMode, ConfirmationActorMode = workflow.ConfirmationActorMode,
            AllowReviewerReuse = workflow.AllowReviewerReuse, AssignmentRetentionMode = workflow.AssignmentRetentionMode,
            RejectAllowed = workflow.RejectAllowed, UndoApprovalMode = workflow.UndoApprovalMode,
            UndoActorMode = workflow.UndoActorMode, UndoActiveReviewMode = workflow.UndoActiveReviewMode
        }];
    }

    public Task HandleAsync(long messageId, ChangeMessageWorkflowRequest request, CancellationToken ct)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.WorkflowManage, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var workflow = await store.FindWorkflowAsync(request.WorkflowDefinitionId, ct) ?? throw new ResourceNotFoundException("Workflow was not found.");
            if (access.Source.Direction is null)
                throw new DomainRuleViolationException("Message direction is required to change workflow.");
            var resolved = await routing.ResolveAsync(access.Source.MessageType, access.Source.Direction.Value,
                access.Source.DepartmentIds, access.Source.BranchId, ct);
            if (workflow.Id != resolved.Workflow.Id)
                throw new DomainRuleViolationException("The selected workflow does not match the message routing rules.");
            var reviews = access.Reviews;
            var previous = message.WorkflowDefinitionId;
            var previousState = message.State;
            var previousProcessing = message.ProcessingDepartmentIds.ToArray();
            message.ChangeWorkflow(workflow, reviews);
            message.ApplyRouting(workflow, resolved.ProcessingDepartmentIds);
            if (previous == workflow.Id && previousProcessing.SequenceEqual(message.ProcessingDepartmentIds)) return;
            store.AddAudit(AuditEventFactory.Create(messageId, AuditEventType.MessageWorkflowChanged, user.UserId,
                clock.UtcNow, previousState, message.State,
                new AuditEventDetailsDto(WorkflowDefinitionId: workflow.Id, PreviousWorkflowDefinitionId: previous), correlation.CorrelationId));
            await store.SaveChangesAsync(ct);
        }, ct);
}
