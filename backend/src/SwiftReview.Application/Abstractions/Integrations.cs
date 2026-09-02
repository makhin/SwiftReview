namespace SwiftReview.Application.Abstractions;

public sealed record AwhMessage(string ExternalId, string MessageType, int BranchId, int DepartmentId,
    DateTimeOffset ReceivedAt, string Sender, string Receiver, string? Account,
    string? Currency, decimal? Amount, string? Reference, string RawContent);

public interface IAwhClient
{
    Task<IReadOnlyList<AwhMessage>> GetMessagesSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);
    Task<AwhMessage?> GetMessageAsync(string externalId, CancellationToken cancellationToken);
}

public interface IDocumentStorage
{
    Task StoreConfirmationAsync(long messageId, string content, string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface INotificationSender
{
    Task SendAsync(string recipient, string message, string eventName, string idempotencyKey,
        CancellationToken cancellationToken);
}
