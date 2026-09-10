using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Auditing;

namespace ORP.Application.Messages.ChangeWorkflow;

public sealed class ChangeMessageWorkflowValidator : AbstractValidator<ChangeMessageWorkflowRequest>
{
    public ChangeMessageWorkflowValidator() { RuleFor(x => x.WorkflowDefinitionId).GreaterThan(0); }
}

public sealed class ChangeMessageWorkflowHandler(IORPStore store, IValidator<ChangeMessageWorkflowRequest> validator,
    IUserAccessService users, IReferenceDataQueries references, ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, ChangeMessageWorkflowRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var message = await store.FindMessageAsync(messageId, ct) ?? throw new ResourceNotFoundException("Message was not found.");
        var workflow = await store.FindWorkflowAsync(request.WorkflowDefinitionId, ct) ?? throw new ResourceNotFoundException("Workflow was not found.");
        var access = await users.GetByIdAsync(user.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.IsGlobalAdministrator && !(await references.GetWorkflowsAsync(access, ct)).Any(w => w.Id == workflow.Id))
            throw new UnauthorizedAccessException("The selected workflow is outside your access scope.");
        var reviews = await store.GetReviewsAsync(messageId, ct);
        var previous = message.WorkflowDefinitionId;
        message.ChangeWorkflow(workflow, reviews);
        if (previous == workflow.Id) return;
        store.AddAudit(AuditEventFactory.Create(messageId, AuditEventType.MessageWorkflowChanged, user.UserId,
            clock.UtcNow, message.State, message.State,
            new AuditEventDetailsDto(WorkflowDefinitionId: workflow.Id, PreviousWorkflowDefinitionId: previous), correlation.CorrelationId));
        await store.SaveChangesAsync(ct);
    }
}
