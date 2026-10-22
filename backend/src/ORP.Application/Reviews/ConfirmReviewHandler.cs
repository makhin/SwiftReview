using ORP.Domain.Assignments;
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
    public async Task HandleAsync(long messageId, ConfirmReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId,
                ReviewAssignmentRules.PermissionForLevel(request.Level), ct, ownershipRequirement: MessageActionOwnership.Assignee);
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, access.Message.WorkflowDefinitionId, ct);
            await ApplyAsync(access, workflow, await store.GetActiveAssignmentsAsync(messageId, ct), request, ct);
        }, cancellationToken);
    }

    // The caller owns validation, authorization and the transaction.
    internal async Task ApplyAsync(AuthorizedMessage access, WorkflowDefinition workflow,
        List<Assignment> activeAssignments, ConfirmReviewRequest request, CancellationToken ct)
    {
        var message = access.Message;
        var messageId = message.Id;
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
        await assignments.AfterApprovalAsync(message, review.Level, workflow, user.UserId, correlation.CorrelationId, ct, activeAssignments);
        if (message.State == MessageState.Completed)
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.MessageCompleted, user.UserId,
                reviewingState, message.State, review, now, correlation.CorrelationId, comment);
        await store.SaveChangesAsync(ct);
    }
}
