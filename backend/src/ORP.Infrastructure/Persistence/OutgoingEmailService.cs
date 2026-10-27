using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Application.Emails;
using ORP.Domain.Auditing;
using ORP.Domain.Emails;
using ORP.Domain.Identity;
using ORP.Infrastructure.Documents;

namespace ORP.Infrastructure.Persistence;

public sealed class OutgoingEmailService(ORPDbContext db, MessageAuthorizationService authorization,
    ITransactionExecutor transactions, IValidator<CreateEmailRequest> validator, IConfiguration configuration,
    TextToPdfConverter converter, ICurrentUser current, IClock clock, ICorrelationContext correlation) : IOutgoingEmailService
{
    public Task<CreatedEmail> CreateAsync(long messageId, CreateEmailRequest request,
        IReadOnlyList<EmailUpload> uploads, CancellationToken ct) => transactions.ExecuteAsync(async token =>
    {
        await authorization.RequireAsync(messageId, Permissions.MessageForward, token);
        await validator.ValidateAndThrowAsync(request, token);
        var from = configuration["Email:FromAddress"];
        var fromName = configuration["Email:FromDisplayName"];
        if (!CreateEmailValidator.ValidAddress(from) || !CreateEmailValidator.ValidHeader(fromName, 200))
            throw new EmailNotConfiguredException();
        EmailLimits.CheckAttachments(uploads.Count, uploads.Select(x => (long)x.Content.Length));
        var email = new OutgoingEmail
        {
            MessageId = messageId, CreatedByUserId = current.UserId, CreatedAt = clock.UtcNow,
            FromAddress = from!.Trim(), FromDisplayName = fromName?.Trim(), Subject = request.Subject,
            Body = request.Body, BodyFormat = request.BodyFormat, Importance = request.Importance
        };
        AddRecipients(email, request.To, EmailRecipientKind.To);
        AddRecipients(email, request.Cc, EmailRecipientKind.Cc);
        AddRecipients(email, request.Bcc, EmailRecipientKind.Bcc);
        AddRecipients(email, request.ReplyTo, EmailRecipientKind.ReplyTo);
        foreach (var upload in uploads)
        {
            var name = upload.FileName.Replace('\\', '/').Split('/').Last();
            var type = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType;
            if (string.IsNullOrWhiteSpace(name) || !CreateEmailValidator.ValidHeader(name, 255) ||
                !CreateEmailValidator.ValidHeader(type, 255) || !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(type, out _))
                throw new ValidationException("Provide a valid attachment filename and content type.");
            email.Attachments.Add(new EmailAttachment
            {
                FileName = name, ContentType = type, Content = upload.Content, Size = upload.Content.Length,
                Source = EmailAttachmentSource.Upload
            });
        }
        if (request.IncludeMessagePdf)
        {
            var body = await db.SwiftMessages.Where(x => x.MessageId == messageId).Select(x => x.Body).SingleAsync(token);
            var pdf = converter.Convert(body);
            email.Attachments.Add(new EmailAttachment
            {
                FileName = $"message-{messageId}.pdf", ContentType = "application/pdf", Content = pdf,
                Size = pdf.Length, Source = EmailAttachmentSource.MessagePdf
            });
        }
        EmailLimits.CheckAttachments(email.Attachments.Count, email.Attachments.Select(x => x.Size));
        db.OutgoingEmails.Add(email);
        db.AuditEvents.Add(new AuditEvent(messageId, AuditEventType.EmailCreated, current.UserId, email.CreatedAt,
            null, null, "{}", correlation.CorrelationId));
        await db.SaveChangesAsync(token);
        return new CreatedEmail(email.Id, messageId, email.Status, email.CreatedAt, email.Attachments.Count);
    }, ct);

    private static void AddRecipients(OutgoingEmail email, IEnumerable<EmailAddress> addresses, EmailRecipientKind kind)
    {
        foreach (var address in addresses)
            email.Recipients.Add(new EmailRecipient { Kind = kind, Address = address.Address.Trim(), DisplayName = address.DisplayName?.Trim() });
    }
}
