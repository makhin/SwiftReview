using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Auditing;
using ORP.Domain.Reviews;

namespace ORP.Application.Reviews;

public sealed class StartReviewValidator : AbstractValidator<StartReviewRequest>
{
    public StartReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); }
}
public sealed class ApproveReviewValidator : AbstractValidator<ApproveReviewRequest>
{
    public ApproveReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.Comment).MaximumLength(2000); }
}
public sealed class RejectReviewValidator : AbstractValidator<RejectReviewRequest>
{
    public RejectReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.Comment).MaximumLength(2000); }
}
public sealed class UndoReviewValidator : AbstractValidator<UndoReviewRequest>
{
    public UndoReviewValidator() { RuleFor(x => x.ReviewId).GreaterThan(0); }
}

public sealed class StartReviewHandler(IORPStore store, IValidator<StartReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task<long> HandleAsync(long messageId, StartReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var (message, workflow, reviews) = await LoadAsync(store, messageId, cancellationToken);
        var oldState = message.State;
        var now = clock.UtcNow;
        var review = message.StartReview(request.Level, user.UserId, workflow, reviews, now);
        store.AddReview(review);
        AddEvent(store, messageId, AuditEventType.ReviewStarted, user.UserId, oldState, message.State,
            review, now, correlation.CorrelationId);
        await store.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    internal static async Task<(Domain.Messages.Message Message, Domain.Workflows.WorkflowDefinition Workflow, List<Review> Reviews)> LoadAsync(
        IORPStore store, long messageId, CancellationToken cancellationToken)
    {
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, cancellationToken) ?? throw new ResourceNotFoundException("Workflow was not found.");
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        return (message, workflow, reviews);
    }

    internal static void AddEvent(IORPStore store, long messageId, AuditEventType type, int userId,
        Domain.Messages.MessageState oldState, Domain.Messages.MessageState newState, Review review,
        DateTimeOffset now, string correlationId, string? comment = null) =>
        store.AddAudit(AuditEventFactory.Create(messageId, type, userId, now, oldState, newState,
            new AuditEventDetailsDto(ReviewLevel: review.Level, Comment: comment), correlationId, review));
}

public sealed class ApproveReviewHandler(IORPStore store, IValidator<ApproveReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, ApproveReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var (message, workflow, reviews) = await StartReviewHandler.LoadAsync(store, messageId, cancellationToken);
        var review = reviews.SingleOrDefault(x => x.Level == request.Level && x.Status == ReviewStatus.InProgress)
            ?? throw new ResourceNotFoundException("Active review was not found.");
        if (review.ReviewerId != user.UserId) throw new Domain.Common.DomainRuleViolationException("Only the reviewer who started the review can approve it.");
        var oldState = message.State;
        var now = clock.UtcNow;
        message.Approve(review, workflow, reviews, request.Comment, now);
        StartReviewHandler.AddEvent(store, messageId, AuditEventType.ReviewApproved, user.UserId, oldState,
            message.State, review, now, correlation.CorrelationId, request.Comment);
        if (message.State == Domain.Messages.MessageState.Completed)
            StartReviewHandler.AddEvent(store, messageId, AuditEventType.MessageCompleted, user.UserId, oldState,
                message.State, review, now, correlation.CorrelationId, request.Comment);
        await store.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RejectReviewHandler(IORPStore store, IValidator<RejectReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, RejectReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var (message, _, reviews) = await StartReviewHandler.LoadAsync(store, messageId, cancellationToken);
        var review = reviews.SingleOrDefault(x => x.Level == request.Level && x.Status == ReviewStatus.InProgress)
            ?? throw new ResourceNotFoundException("Active review was not found.");
        var oldState = message.State;
        var now = clock.UtcNow;
        message.Reject(review, request.Comment, now);
        StartReviewHandler.AddEvent(store, messageId, AuditEventType.ReviewRejected, user.UserId, oldState,
            message.State, review, now, correlation.CorrelationId, request.Comment);
        await store.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UndoReviewHandler(IORPStore store, IValidator<UndoReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, UndoReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, cancellationToken)
            ?? throw new ResourceNotFoundException("Workflow was not found.");
        var review = reviews.SingleOrDefault(x => x.Id == request.ReviewId) ?? throw new ResourceNotFoundException("Review was not found.");
        var oldState = message.State;
        var now = clock.UtcNow;
        message.UndoLastApproval(review, workflow, reviews, user.UserId, now);
        StartReviewHandler.AddEvent(store, messageId, AuditEventType.ConfirmationUndone, user.UserId, oldState,
            message.State, review, now, correlation.CorrelationId, review.Comment);
        await store.SaveChangesAsync(cancellationToken);
    }
}
