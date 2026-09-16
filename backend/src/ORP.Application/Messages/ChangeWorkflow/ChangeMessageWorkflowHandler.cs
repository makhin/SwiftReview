using FluentValidation;
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
    IUserAuthorizationQueries users, ICurrentUser user, IClock clock, ICorrelationContext correlation,
    ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, ChangeMessageWorkflowRequest request, CancellationToken ct)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.WorkflowManage, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var workflow = await store.FindWorkflowAsync(request.WorkflowDefinitionId, ct) ?? throw new ResourceNotFoundException("Workflow was not found.");
            if (!access.IsGlobalAdministrator && !await users.CanAccessWorkflowAsync(user.UserId, workflow.Id, ct))
                throw new UnauthorizedAccessException("The selected workflow is outside your access scope.");
            var reviews = access.Reviews;
            var previous = message.WorkflowDefinitionId;
            message.ChangeWorkflow(workflow, reviews);
            if (previous == workflow.Id) return;
            store.AddAudit(AuditEventFactory.Create(messageId, AuditEventType.MessageWorkflowChanged, user.UserId,
                clock.UtcNow, message.State, message.State,
                new AuditEventDetailsDto(WorkflowDefinitionId: workflow.Id, PreviousWorkflowDefinitionId: previous), correlation.CorrelationId));
            await store.SaveChangesAsync(ct);
        }, ct);
}
