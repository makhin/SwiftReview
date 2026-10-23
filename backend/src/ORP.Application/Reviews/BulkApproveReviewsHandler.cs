using FluentValidation;
using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Authorization;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Reviews;

public sealed class BulkApproveReviewsValidator : AbstractValidator<BulkApproveReviewsRequest>
{
    public BulkApproveReviewsValidator()
    {
        RuleFor(x => x.MessageIds).NotNull().Must(ids => ids is { Count: >= 1 and <= 100 })
            .WithMessage("Provide between 1 and 100 message IDs.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Message IDs must be unique.");
        RuleForEach(x => x.MessageIds).GreaterThan(0);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public sealed class BulkApproveReviewsHandler(IValidator<BulkApproveReviewsRequest> validator,
    ICurrentUser user, ICorrelationContext correlation, MessageAuthorizationService authorization,
    IORPStore store, AssignmentCoordinator assignments, ConfirmReviewHandler confirm,
    ITransactionExecutor transactions, ILogger<BulkApproveReviewsHandler> logger)
{
    public async Task<BulkApproveReviewsResponse> HandleAsync(BulkApproveReviewsRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var results = new List<BulkApproveMessageResult>(request.MessageIds.Count);
        foreach (var id in request.MessageIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                results.Add(await transactions.ExecuteAsync(token => ApproveAsync(id, request.Comment, token), ct));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                var (status, code, detail) = exception switch
                {
                    UnauthorizedAccessException => (403, "forbidden", "The current user is not allowed to perform this action."),
                    ResourceNotFoundException => (404, "not_found", exception.Message),
                    DomainRuleViolationException => (409, "domain_conflict", exception.Message),
                    ConcurrentUpdateException => (409, "concurrent_update", exception.Message),
                    TransactionCommitOutcomeUnknownException => (500, "commit_outcome_unknown",
                        "The commit outcome is unknown. Reload the message before retrying."),
                    _ => (500, "unexpected_error", "An unexpected error occurred. Reload the message before retrying.")
                };
                if (status == 500)
                    logger.LogError(exception, "Bulk approval failed for message {MessageId}, user {UserId}, correlation {CorrelationId}",
                        id, user.UserId, correlation.CorrelationId);
                else
                    logger.LogWarning("Bulk approval rejected for message {MessageId}, user {UserId}, code {Code}, correlation {CorrelationId}",
                        id, user.UserId, code, correlation.CorrelationId);
                results.Add(new(id, status) { Error = new(code, detail) });
            }
        }
        return new(results);
    }

    private async Task<BulkApproveMessageResult> ApproveAsync(long id, string? comment, CancellationToken ct)
    {
        var access = await authorization.RequireAsync(id, Permissions.ReviewBulkApprove, ct);
        access = await authorization.RequireAsync(access, Permissions.MessageAssign, ct);
        var message = access.Message;
        var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, message.WorkflowDefinitionId, ct);
        if (!workflow.IsActive || workflow.ConfirmationMode != ConfirmationMode.Immediate)
            throw new DomainRuleViolationException("Bulk approval requires an active immediate-confirmation workflow.");
        if (message.State.AssignableReviewLevel() is not { } currentLevel ||
            access.Reviews.Any(r => r.Status == ReviewStatus.InProgress))
            throw new DomainRuleViolationException("The message has no confirmation available for bulk approval.");
        var levels = workflow.RequiredLevels().Where(level => level >= currentLevel).ToArray();
        if (levels.Length == 0 || levels[0] != currentLevel)
            throw new DomainRuleViolationException("The current level is not required by this workflow.");
        var activeAssignments = await store.GetActiveAssignmentsAsync(id, ct);
        if ((message.CurrentAssigneeId is int assignee && assignee != user.UserId) ||
            activeAssignments.Any(a => a.EndedAt is null && a.Level is int level && levels.Contains(level) && a.AssignedTo != user.UserId))
            throw new DomainRuleViolationException("The message or a remaining level is assigned to another reviewer.");
        foreach (var level in levels)
            access = await authorization.RequireAsync(access, ReviewAssignmentRules.PermissionForLevel(level), ct);

        foreach (var level in levels)
        {
            if (message.CurrentAssigneeId is null)
            {
                await assignments.AssignAsync(message, user.UserId, user.UserId, correlation.CorrelationId, ct,
                    level, activeAssignments);
                await store.SaveChangesAsync(ct);
            }
            await confirm.ApplyAsync(access, workflow, activeAssignments, new(level, comment), ct, allowReviewerReuse: true);
        }
        return new(id, 200) { ApprovedLevels = levels };
    }
}
