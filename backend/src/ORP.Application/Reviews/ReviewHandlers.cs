using FluentValidation;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Reviews;

namespace ORP.Application.Reviews;

public sealed class StartReviewValidator : AbstractValidator<StartReviewRequest>
{
    public StartReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); }
}
public sealed class ApproveReviewValidator : AbstractValidator<ApproveReviewRequest>
{
    public ApproveReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.ReviewId).GreaterThan(0); RuleFor(x => x.Comment).MaximumLength(2000); }
}
public sealed class RejectReviewValidator : AbstractValidator<RejectReviewRequest>
{
    public RejectReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.ReviewId).GreaterThan(0); RuleFor(x => x.Comment).MaximumLength(2000); }
}
public sealed class CancelReviewValidator : AbstractValidator<CancelReviewRequest>
{
    public CancelReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.ReviewId).GreaterThan(0); }
}
public sealed class UndoReviewValidator : AbstractValidator<UndoReviewRequest>
{
    public UndoReviewValidator() { RuleFor(x => x.ReviewId).GreaterThan(0); RuleFor(x => x.Comment).MaximumLength(2000); }
}

public sealed class StartReviewHandler(IORPStore store, IValidator<StartReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation, AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task<long> HandleAsync(long messageId, StartReviewRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, MessageAuthorizationService.ReviewPermission(request.Level),
                ct, request.Level, MessageActionOwnership.Assignee);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var reviews = access.Reviews;
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
            var active = reviews.SingleOrDefault(r => r.Level == request.Level && r.Status == ReviewStatus.InProgress);
            if (active is not null && (access.IsGlobalAdministrator ||
                (active.ReviewerId == user.UserId && message.CurrentAssigneeId == user.UserId)))
                return active.Id;
            if (access.IsGlobalAdministrator && message.CurrentAssigneeId != user.UserId &&
                message.State is Domain.Messages.MessageState.Assigned or Domain.Messages.MessageState.WaitingForSecondReview or Domain.Messages.MessageState.WaitingForThirdReview)
                await assignments.AssignAsync(message, user.UserId, user.UserId, correlation.CorrelationId,
                    ct, allowSelfAssignment: true);
            var oldState = message.State;
            var now = clock.UtcNow;
            var review = message.StartReview(request.Level, user.UserId, workflow, reviews, now, isGlobalAdministrator: access.IsGlobalAdministrator);
            store.AddReview(review);
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewStarted, user.UserId, oldState, message.State,
                review, now, correlation.CorrelationId);
            await store.SaveChangesAsync(ct);
            return review.Id;
        }, cancellationToken);
}

public sealed class ApproveReviewHandler(IORPStore store, IValidator<ApproveReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, ApproveReviewRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, MessageAuthorizationService.ReviewPermission(request.Level),
                ct, request.Level, MessageActionOwnership.ActiveReviewer);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var reviews = access.Reviews;
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
            var review = ReviewHandlerHelper.RequireActiveReview(reviews, request.ReviewId, request.Level);
            var oldState = message.State;
            var now = clock.UtcNow;
            message.Approve(review, workflow, reviews, user.UserId, request.Comment, now, access.IsGlobalAdministrator);
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewApproved, user.UserId, oldState,
                message.State, review, now, correlation.CorrelationId, request.Comment);
            await assignments.UnassignAsync(message, user.UserId, correlation.CorrelationId, ct);
            if (message.State == Domain.Messages.MessageState.Completed)
                ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.MessageCompleted, user.UserId, oldState,
                    message.State, review, now, correlation.CorrelationId, request.Comment);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}

public sealed class RejectReviewHandler(IORPStore store, IValidator<RejectReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, RejectReviewRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, MessageAuthorizationService.ReviewPermission(request.Level),
                ct, request.Level, MessageActionOwnership.ActiveReviewer);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var reviews = access.Reviews;
            var review = ReviewHandlerHelper.RequireActiveReview(reviews, request.ReviewId, request.Level);
            var oldState = message.State;
            var now = clock.UtcNow;
            message.Reject(review, user.UserId, request.Comment, now, access.IsGlobalAdministrator);
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewRejected, user.UserId, oldState,
                message.State, review, now, correlation.CorrelationId, request.Comment);
            await assignments.UnassignAsync(message, user.UserId, correlation.CorrelationId, ct);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}

public sealed class CancelReviewHandler(IORPStore store, IValidator<CancelReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, CancelReviewRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, MessageAuthorizationService.ReviewPermission(request.Level),
                ct, request.Level, MessageActionOwnership.ActiveReviewer);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var reviews = access.Reviews;
            var review = ReviewHandlerHelper.RequireActiveReview(reviews, request.ReviewId, request.Level);
            var oldState = message.State;
            var now = clock.UtcNow;
            message.CancelReview(review, user.UserId, now, access.IsGlobalAdministrator);
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewCancelled, user.UserId, oldState,
                message.State, review, now, correlation.CorrelationId);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}

public sealed class UndoReviewHandler(IORPStore store, IValidator<UndoReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, UndoReviewRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.ReviewUndo, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            var reviews = access.Reviews;
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
            var review = reviews.SingleOrDefault(x => x.Id == request.ReviewId) ?? throw new ResourceNotFoundException("Review was not found.");
            var oldState = message.State;
            var now = clock.UtcNow;
            message.UndoLastApproval(review, workflow, reviews, user.UserId, now, access.IsGlobalAdministrator);
            var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ConfirmationUndone, user.UserId, oldState,
                message.State, review, now, correlation.CorrelationId, comment);
            if (message.CurrentAssigneeId is not null)
                await assignments.UnassignAsync(message, user.UserId, correlation.CorrelationId, ct);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}
