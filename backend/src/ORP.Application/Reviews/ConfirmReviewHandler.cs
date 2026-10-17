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
        RuleFor(x => x.ExpectedVersion).Must(IsVersion).WithMessage("expectedVersion must be a base64 SQL rowversion (8 bytes).");
    }

    private static bool IsVersion(string? value)
    {
        Span<byte> bytes = stackalloc byte[8];
        return value is not null && Convert.TryFromBase64String(value, bytes, out var length) && length == 8;
    }
}

public sealed class ConfirmReviewHandler(IORPStore store, IValidator<ConfirmReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation, AssignmentCoordinator assignments,
    ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, ConfirmReviewRequest request, CancellationToken cancellationToken) =>
        transactions.ExecuteAsync(async ct =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var access = await authorization.RequireAsync(messageId,
                MessageAuthorizationService.ReviewPermission(request.Level), ct);
            var message = access.Message;
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
            if (workflow.ConfirmationMode != ConfirmationMode.Immediate)
                throw new DomainRuleViolationException("This workflow does not use immediate confirmations.");
            if (!message.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(request.ExpectedVersion)))
                throw new DomainRuleViolationException("The message version has changed. Reload before confirming.");
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
            message.Approve(review, workflow, access.Reviews, user.UserId, comment, now, access.IsGlobalAdministrator);
            ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.ReviewApproved, user.UserId,
                reviewingState, message.State, review, now, correlation.CorrelationId, comment);
            if (message.CurrentAssigneeId is not null &&
                (workflow.AssignmentRetentionMode == AssignmentRetentionMode.PerConfirmation || message.State == MessageState.Completed))
                await assignments.UnassignAsync(message, user.UserId, correlation.CorrelationId, ct);
            if (message.State == MessageState.Completed)
                ReviewHandlerHelper.AddEvent(store, messageId, AuditEventType.MessageCompleted, user.UserId,
                    reviewingState, message.State, review, now, correlation.CorrelationId, comment);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}
