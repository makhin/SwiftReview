using System.Text.Json;
using FluentValidation;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Outbox;

namespace SwiftReview.Application.Messages.Import;

public sealed class ImportMessageValidator : AbstractValidator<ImportMessageRequest>
{
    public ImportMessageValidator()
    {
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MessageType).NotEmpty().MaximumLength(20);
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.Sender).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Receiver).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RawContent).NotEmpty();
    }
}

public sealed class ImportMessageHandler(ISwiftReviewStore store, IWorkflowResolver workflows,
    IValidator<ImportMessageRequest> validator, IClock clock, ICorrelationContext correlation)
{
    public async Task<(Message Message, bool Created)> HandleAsync(ImportMessageRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var existing = await store.FindMessageByExternalIdAsync(request.ExternalId, cancellationToken);
        if (existing is not null) return (existing, false);
        var workflow = await workflows.ResolveAsync(request.MessageType, request.DepartmentId, request.BranchId, cancellationToken);

        var message = new Message(request.ExternalId, request.MessageType, request.BranchId, request.DepartmentId,
            workflow.Id, request.ReceivedAt, request.Sender, request.Receiver, request.Account,
            request.Currency, request.Amount, request.Reference);
        store.AddMessage(message);
        store.AddRawData(new MessageRawData(message, request.RawContent));
        store.AddAudit(new AuditEvent(message, "MessageImported", null, clock.UtcNow, null, message.State.ToString(),
            JsonSerializer.Serialize(new { request.ExternalId, request.MessageType }), correlation.CorrelationId));
        store.AddOutbox(new OutboxMessage("MessageImported",
            JsonSerializer.Serialize(new { request.ExternalId, request.BranchId, request.DepartmentId }), clock.UtcNow, correlation.CorrelationId));
        try
        {
            await store.SaveChangesAsync(cancellationToken);
            return (message, true);
        }
        catch (DuplicateExternalIdException)
        {
            var winner = await store.FindMessageByExternalIdAsync(request.ExternalId, cancellationToken);
            if (winner is null) throw;
            return (winner, false);
        }
    }
}
