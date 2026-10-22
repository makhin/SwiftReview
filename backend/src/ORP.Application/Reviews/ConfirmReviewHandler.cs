using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Authorization;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;

namespace ORP.Application.Reviews;

public sealed class ConfirmReviewValidator : AbstractValidator<ConfirmReviewRequest>
{
    public ConfirmReviewValidator()
    {
        RuleFor(x => x.Level).InclusiveBetween(1, 3);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public sealed class ConfirmReviewHandler(IORPStore store, IValidator<ConfirmReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation, AssignmentCoordinator assignments,
    ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, ConfirmReviewRequest request, CancellationToken cancellationToken) =>
        transactions.ExecuteAsync(ct => HandleInTransactionAsync(messageId, request, ct), cancellationToken);

    // The caller must own the transaction; shared with assign-and-confirm.
    internal async Task HandleInTransactionAsync(long messageId, ConfirmReviewRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var access = await authorization.RequireAsync(messageId,
            MessageAuthorizationService.ReviewPermission(request.Level), ct, ownershipRequirement: MessageActionOwnership.Assignee);
        var message = access.Message;
        var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
        if (workflow.ConfirmationMode != ConfirmationMode.Immediate)
            throw new DomainRuleViolationException("This workflow does not use immediate confirmations.");
        var now = clock.UtcNow;
        var oldState = message.State;
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var review = message.StartReview(request.Level, user.UserId, workflow, access.Reviews, now,
            isGlobalAdministrator: access.IsGlobalAdministrator);
        store.AddReview(review);
        access.Reviews.Add(review);
        ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewStarted, user.UserId,
            oldState, message.State, review, now, correlation.CorrelationId);
        var reviewingState = message.State;
        message.Approve(review, workflow, access.Reviews, user.UserId, comment, now);
        ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewApproved, user.UserId,
            reviewingState, message.State, review, now, correlation.CorrelationId, comment);
        if (message.CurrentAssigneeId is not null &&
            (workflow.AssignmentRetentionMode == AssignmentRetentionMode.PerConfirmation || message.State == MessageState.Completed))
            await assignments.UnassignAsync(message, user.UserId, correlation.CorrelationId, ct);
        if (message.State == MessageState.Completed)
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.MessageCompleted, user.UserId,
                reviewingState, message.State, review, now, correlation.CorrelationId, comment);
        await store.SaveChangesAsync(ct);
    }
}
