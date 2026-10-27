using ORP.Domain.Emails;

namespace ORP.Application.Emails;

public sealed record EmailAddress(string Address, string? DisplayName = null);
public sealed record CreateEmailRequest
{
    public IReadOnlyList<EmailAddress> To { get; init; } = [];
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];
    public IReadOnlyList<EmailAddress> Bcc { get; init; } = [];
    public IReadOnlyList<EmailAddress> ReplyTo { get; init; } = [];
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public EmailBodyFormat BodyFormat { get; init; }
    public EmailImportance Importance { get; init; } = EmailImportance.Normal;
    public bool IncludeMessagePdf { get; init; }
}
public sealed record EmailUpload(string FileName, string ContentType, byte[] Content);
public sealed record CreatedEmail(long Id, long MessageId, EmailStatus Status, DateTimeOffset CreatedAt, int AttachmentCount);
public interface IOutgoingEmailService
{
    Task<CreatedEmail> CreateAsync(long messageId, CreateEmailRequest request, IReadOnlyList<EmailUpload> uploads, CancellationToken ct);
}
public sealed class EmailNotConfiguredException() : Exception("A valid email sender must be configured.");
public sealed class EmailSizeLimitException() : Exception("Email attachments exceed the allowed size or count.");

public static class EmailLimits
{
    public const int FileBytes = 10 * 1024 * 1024;
    public const int TotalBytes = 20 * 1024 * 1024;
    public const int RequestBytes = 25 * 1024 * 1024;
    public static void CheckAttachments(int count, IEnumerable<long> sizes)
    {
        var values = sizes.ToArray();
        if (count > 10 || values.Any(x => x > FileBytes) || values.Sum() > TotalBytes)
            throw new EmailSizeLimitException();
    }
}
