using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http.Features;
using ORP.Application.Emails;

namespace ORP.Api.Endpoints;

public static class EmailEndpoints
{
    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    // Describes the wire format; files are read explicitly so authentication precedes parsing.
    public sealed record EmailMultipartRequest(string Metadata, IFormFile[]? Attachments);

    public static void MapEmailEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/messages/{messageId:long}/emails", CreateEmail)
            .WithTags("Email").WithName("CreateMessageEmail")
            .WithSummary("Save an outgoing email and attachments for a SWIFT message.")
            .WithDescription("Requires message.forward; ViewOnly is forbidden. No email is sent. " +
                "multipart/form-data: metadata is a JSON string; attachments is a repeated file part. " +
                "Metadata: to (required), cc, bcc, replyTo arrays of {address, displayName?}; subject and body required; " +
                "bodyFormat Text (default) or Html; importance Low, Normal (default), High; includeMessagePdf false (default). " +
                "From comes from Email:FromAddress/FromDisplayName. Limits: 100 To/Cc/Bcc, 10 ReplyTo, subject 500 chars, body 100000 chars; " +
                "10 attachments including PDF, 10 MiB each, 20 MiB combined, HTTP request 25 MiB. " +
                "201 confirms persistence with Pending status, not delivery. Each successful POST creates a new email. " +
                "Example metadata: {\"to\":[{\"address\":\"recipient@example.com\"}],\"subject\":\"SWIFT message\",\"body\":\"Please review.\",\"includeMessagePdf\":true}")
            .Accepts<EmailMultipartRequest>("multipart/form-data")
            .Produces<CreatedEmail>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(413).ProducesProblem(415).ProducesProblem(503);
    }

    private static async Task<IResult> CreateEmail(long messageId, HttpRequest request,
        IOutgoingEmailService service, CancellationToken ct)
    {
        if (request.ContentLength > EmailLimits.RequestBytes) throw new EmailSizeLimitException();
        var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = EmailLimits.RequestBytes;
        if (request.ContentType is null || !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new BadHttpRequestException("Use multipart/form-data.", StatusCodes.Status415UnsupportedMediaType);
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(new FormOptions
            {
                MultipartBodyLengthLimit = EmailLimits.FileBytes,
                ValueLengthLimit = 1024 * 1024
            }, ct);
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase)) throw new EmailSizeLimitException();
            throw new ValidationException("Malformed multipart form.");
        }
        catch (IOException exception)
        {
            ct.ThrowIfCancellationRequested();
            // MultipartReader reports a missing closing boundary as IOException.
            // Other IO failures (for example temporary-file storage) remain server errors.
            if (!exception.Message.StartsWith("Unexpected end of Stream", StringComparison.Ordinal)) throw;
            throw new ValidationException("Incomplete multipart form.");
        }
        if (form["metadata"].Count != 1 || form.Keys.Any(x => x != "metadata") || form.Files.Any(x => x.Name != "attachments"))
            throw new ValidationException("Provide one metadata field and optional attachments file parts.");
        CreateEmailRequest metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<CreateEmailRequest>(form["metadata"][0]!, MetadataJson)
                ?? throw new ValidationException("Metadata must be a JSON object.");
        }
        catch (JsonException) { throw new ValidationException("Metadata must be valid email JSON with supported field types and enum names."); }
        EmailLimits.CheckAttachments(form.Files.Count + (metadata.IncludeMessagePdf ? 1 : 0), form.Files.Select(x => x.Length));
        var uploads = new List<EmailUpload>();
        foreach (var file in form.Files)
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            uploads.Add(new(file.FileName, file.ContentType, stream.ToArray()));
        }
        var result = await service.CreateAsync(messageId, metadata, uploads, ct);
        return Results.Json(result, statusCode: StatusCodes.Status201Created);
    }
}
