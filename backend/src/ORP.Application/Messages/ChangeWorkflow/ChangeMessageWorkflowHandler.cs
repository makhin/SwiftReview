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
            if (access.Source.Direction is null)
                throw new DomainRuleViolationException("Message direction is required to change workflow.");
            if (workflow.Direction != access.Source.Direction)
                throw new DomainRuleViolationException("The selected workflow direction must match the message direction.");
            if (!workflow.DepartmentIds.SequenceEqual(access.Source.DepartmentIds.Order()))
                throw new DomainRuleViolationException("The selected workflow must cover exactly the message departments.");
            if (workflow.Steps.Any(step => step.ReviewLevel > 2))
            {
                var names = new List<string?>();
                foreach (var id in access.Source.DepartmentIds)
                    names.Add(await store.FindDepartmentNameAsync(id, ct));
                if (names.Any(name => name is null)) throw new ResourceNotFoundException("Message department was not found.");
                if (names.Any(name => name!.Equals("CS", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TFO", StringComparison.OrdinalIgnoreCase)))
                    throw new DomainRuleViolationException("CS and TFO messages cannot use a workflow with review level 3.");
            }
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
