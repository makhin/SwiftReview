using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Assignments.Automatic;
using ORP.Domain.Identity;

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
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        var source = await store.FindMessageSourceAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("SWIFT message was not found.");
        var target = await accessService.GetByIdAsync(request.AssignedTo, cancellationToken)
            ?? throw new ResourceNotFoundException("Assignee was not found.");
        var reviewLevel = ReviewAssignmentRules.ReviewLevelForState(message.State);
        if (!target.Permissions.Contains(Permissions.MessageView) ||
            !target.CanAccess(source.BranchId, source.DepartmentId) ||
            reviewLevel is { } level && !target.Permissions.Contains(ReviewAssignmentRules.PermissionForLevel(level)))
            throw new ValidationException("The assignee cannot access or process the message in its current workflow state.");
        if (reviewLevel > 1)
        {
            var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
            if (ReviewAssignmentRules.ApprovedReviewerIds(reviews).Contains(request.AssignedTo))
                throw new ValidationException("The assignee cannot review more than one level of the same message.");
        }
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }
}
