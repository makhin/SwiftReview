namespace ORP.Domain.Emails;

public enum EmailBodyFormat { Text, Html }
public enum EmailImportance { Low, Normal, High }
public enum EmailRecipientKind { To, Cc, Bcc, ReplyTo }
public enum EmailAttachmentSource { Upload, MessagePdf }
public enum EmailStatus { Pending }

public sealed class OutgoingEmail
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public string FromAddress { get; set; } = null!;
    public string? FromDisplayName { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public EmailBodyFormat BodyFormat { get; set; }
    public EmailImportance Importance { get; set; } = EmailImportance.Normal;
    public ICollection<EmailRecipient> Recipients { get; set; } = [];
    public ICollection<EmailAttachment> Attachments { get; set; } = [];
}

public sealed class EmailRecipient
{
    public long Id { get; set; }
    public long OutgoingEmailId { get; set; }
    public EmailRecipientKind Kind { get; set; }
    public string Address { get; set; } = null!;
    public string? DisplayName { get; set; }
}

public sealed class EmailAttachment
{
    public long Id { get; set; }
    public long OutgoingEmailId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }
    public byte[] Content { get; set; } = [];
    public EmailAttachmentSource Source { get; set; }
}
