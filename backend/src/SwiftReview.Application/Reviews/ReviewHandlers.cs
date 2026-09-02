using System.Text.Json;
using FluentValidation;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Outbox;
using SwiftReview.Domain.Reviews;

namespace SwiftReview.Application.Reviews;

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
    public RejectReviewValidator() { RuleFor(x => x.Level).InclusiveBetween(1, 3); RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000); }
}
public sealed class UndoReviewValidator : AbstractValidator<UndoReviewRequest>
{
    public UndoReviewValidator() { RuleFor(x => x.ReviewId).GreaterThan(0); }
}

public sealed class StartReviewHandler(ISwiftReviewStore store, IValidator<StartReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task<long> HandleAsync(long messageId, StartReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var (message, workflow, reviews) = await LoadAsync(store, messageId, cancellationToken);
        var oldState = message.State;
        var review = message.StartReview(request.Level, user.UserId, workflow, reviews, clock.UtcNow);
        store.AddReview(review);
        AddEvent(store, messageId, "ReviewStarted", user.UserId, oldState.ToString(), message.State.ToString(), request.Level, clock.UtcNow, correlation.CorrelationId);
        await store.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    internal static async Task<(Domain.Messages.Message Message, Domain.Workflows.WorkflowDefinition Workflow, List<Review> Reviews)> LoadAsync(
        ISwiftReviewStore store, long messageId, CancellationToken cancellationToken)
    {
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, cancellationToken) ?? throw new ResourceNotFoundException("Workflow was not found.");
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        return (message, workflow, reviews);
    }

    internal static void AddEvent(ISwiftReviewStore store, long messageId, string type, int userId, string oldState,
        string newState, int level, DateTimeOffset now, string correlationId)
    {
        AddAudit(store, messageId, type, userId, oldState, newState, level, now, correlationId);
        store.AddOutbox(new OutboxMessage(newState == "Completed" ? "MessageCompleted" : "MessageStatusChanged",
            JsonSerializer.Serialize(new { messageId, state = newState }), now, correlationId));
    }

    internal static void AddAudit(ISwiftReviewStore store, long messageId, string type, int userId, string oldState,
        string newState, int level, DateTimeOffset now, string correlationId) =>
        store.AddAudit(new AuditEvent(messageId, type, userId, now, oldState, newState,
            JsonSerializer.Serialize(new { level }), correlationId));
}

public sealed class ApproveReviewHandler(ISwiftReviewStore store, IValidator<ApproveReviewRequest> validator,
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
        message.Approve(review, workflow, reviews, request.Comment, clock.UtcNow);
        var now = clock.UtcNow;
        StartReviewHandler.AddEvent(store, messageId, "ReviewApproved", user.UserId, oldState.ToString(),
            message.State.ToString(), request.Level, now, correlation.CorrelationId);
        if (message.State == Domain.Messages.MessageState.Completed)
            StartReviewHandler.AddAudit(store, messageId, "MessageCompleted", user.UserId, oldState.ToString(),
                message.State.ToString(), request.Level, now, correlation.CorrelationId);
        await store.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RejectReviewHandler(ISwiftReviewStore store, IValidator<RejectReviewRequest> validator,
    ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, RejectReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var (message, _, reviews) = await StartReviewHandler.LoadAsync(store, messageId, cancellationToken);
        var review = reviews.SingleOrDefault(x => x.Level == request.Level && x.Status == ReviewStatus.InProgress)
            ?? throw new ResourceNotFoundException("Active review was not found.");
        var oldState = message.State;
        message.Reject(review, request.Comment!, clock.UtcNow);
        StartReviewHandler.AddEvent(store, messageId, "ReviewRejected", user.UserId, oldState.ToString(), message.State.ToString(), request.Level, clock.UtcNow, correlation.CorrelationId);
        await store.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UndoReviewHandler(ISwiftReviewStore store, IValidator<UndoReviewRequest> validator,
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
        message.UndoLastApproval(review, workflow, reviews, user.UserId, clock.UtcNow);
        StartReviewHandler.AddEvent(store, messageId, "ConfirmationUndone", user.UserId, oldState.ToString(), message.State.ToString(), review.Level, clock.UtcNow, correlation.CorrelationId);
        await store.SaveChangesAsync(cancellationToken);
    }
}
