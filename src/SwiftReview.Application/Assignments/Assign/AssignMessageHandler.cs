using System.Text.Json;
using FluentValidation;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Outbox;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;

namespace SwiftReview.Application.Assignments.Assign;

public sealed class AssignMessageValidator : AbstractValidator<AssignMessageRequest>
{
    public AssignMessageValidator() { RuleFor(x => x.AssignedTo).GreaterThan(0); RuleFor(x => x.RowVersion).NotEmpty(); }
}

public sealed class AssignMessageHandler(ISwiftReviewStore store, IUserAccessService accessService,
    IValidator<AssignMessageRequest> validator, ICurrentUser user, IClock clock, ICorrelationContext correlation)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        store.SetExpectedRowVersion(message, Convert.FromBase64String(request.RowVersion));
        var target = await accessService.GetByIdAsync(request.AssignedTo, cancellationToken)
            ?? throw new ResourceNotFoundException("Assignee was not found.");
        if (!target.Permissions.Contains(Permissions.MessageView) ||
            !target.CanAccess(message.BranchId, message.OwningDepartmentId) ||
            RequiredReviewPermission(message.State) is { } permission && !target.Permissions.Contains(permission))
            throw new ValidationException("The assignee cannot access or process the message in its current workflow state.");
        var oldState = message.State;
        var previous = await store.GetActiveAssignmentAsync(messageId, cancellationToken);
        previous?.End(clock.UtcNow);
        var assignment = new Assignment(messageId, user.UserId, request.AssignedTo, clock.UtcNow);
        message.Assign(request.AssignedTo);
        store.AddAssignment(assignment);
        var eventType = previous is null ? "MessageAssigned" : "MessageReassigned";
        store.AddAudit(new AuditEvent(messageId, eventType, user.UserId, clock.UtcNow, oldState.ToString(), message.State.ToString(),
            JsonSerializer.Serialize(new { request.AssignedTo }), correlation.CorrelationId));
        store.AddOutbox(new OutboxMessage("MessageAssigned", JsonSerializer.Serialize(new { messageId, request.AssignedTo, message.BranchId, departmentId = message.OwningDepartmentId }), clock.UtcNow, correlation.CorrelationId));
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
