using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Reviews;

internal static class ReviewHandlerHelper
{
    public static async Task<WorkflowDefinition> LoadWorkflowAsync(IORPStore store, int workflowId,
        CancellationToken cancellationToken) =>
        await store.FindWorkflowAsync(workflowId, cancellationToken)
            ?? throw new ResourceNotFoundException("Workflow was not found.");

    public static Review RequireActiveReview(IEnumerable<Review> reviews, long reviewId, int level) =>
        reviews.SingleOrDefault(x => x.Id == reviewId && x.Level == level && x.Status == ReviewStatus.InProgress)
            ?? throw new DomainRuleViolationException("This review attempt is no longer active. Close this window and refresh the message before reviewing again.");

    public static void AddEvent(IORPStore store, long messageId, AuditEventType type, int userId,
        MessageState oldState, MessageState newState, Review review,
        DateTimeOffset now, string correlationId, string? comment = null) =>
        store.AddAudit(AuditEventFactory.Create(messageId, type, userId, now, oldState, newState,
            new AuditEventDetailsDto(ReviewLevel: review.Level, Comment: comment), correlationId, review));
}
