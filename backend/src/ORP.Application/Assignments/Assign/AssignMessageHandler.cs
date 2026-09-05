using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Automatic;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

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
        if (!target.Permissions.Contains(Permissions.MessageView) ||
            !target.CanAccess(source.BranchId, source.DepartmentId) ||
            RequiredReviewPermission(message.State) is { } permission && !target.Permissions.Contains(permission))
            throw new ValidationException("The assignee cannot access or process the message in its current workflow state.");
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? RequiredReviewPermission(MessageState state) => state switch
    {
        MessageState.New or MessageState.Assigned or MessageState.FirstReviewInProgress => Permissions.ReviewLevel1,
        MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress => Permissions.ReviewLevel2,
        MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress => Permissions.ReviewLevel3,
        _ => null
    };
}
