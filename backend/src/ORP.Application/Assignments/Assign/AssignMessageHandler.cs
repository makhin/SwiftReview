using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.Assign;

public sealed class AssignMessageValidator : AbstractValidator<AssignMessageRequest>
{
    public AssignMessageValidator() { RuleFor(x => x.AssignedTo).GreaterThan(0); }
}

public sealed class AssignMessageHandler(IORPStore store, IUserAccessService accessService,
    IValidator<AssignMessageRequest> validator, ICurrentUser user, ICorrelationContext correlation,
    AssignmentCoordinator assignments)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => await HandleCoreAsync(messageId, request, false, cancellationToken);

    public async Task ReassignAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => await HandleCoreAsync(messageId, request, true, cancellationToken);

    private async Task HandleCoreAsync(long messageId, AssignMessageRequest request, bool reassign,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        if (reassign && message.CurrentAssigneeId is null)
            throw new DomainRuleViolationException("An unassigned message must be assigned before it can be reassigned.");
        if (!reassign && message.CurrentAssigneeId is not null)
            throw new DomainRuleViolationException("An assigned message must be reassigned instead of assigned.");
        var reviewLevel = ReviewAssignmentRules.AssignmentLevelForState(message.State)
            ?? throw new DomainRuleViolationException($"Assignment is not allowed while message is in state '{message.State}'.");
        var source = await store.FindMessageSourceAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("SWIFT message was not found.");
        var target = await accessService.GetByIdAsync(request.AssignedTo, cancellationToken)
            ?? throw new ResourceNotFoundException("Assignee was not found.");
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        if (!ReviewAssignmentRules.IsEligible(target, source, reviewLevel,
                ReviewAssignmentRules.ApprovedReviewerIds(reviews), user.UserId, message.CurrentAssigneeId))
            throw new ValidationException("The assignee is not eligible to review the message in its current workflow state.");
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            cancellationToken, user.IsGlobalAdministrator);
        await store.SaveChangesAsync(cancellationToken);
    }
}
