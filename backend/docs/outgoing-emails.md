# Outgoing email persistence

`POST /api/messages/{messageId}/emails` saves an email associated with a SWIFT
message. It does **not** send email. Every successful POST creates a separate
record with status `Pending`; automatic client retries can create duplicates.

The caller must be authenticated and have `message.forward` and message visibility
in the existing processing scope. Global administrators follow the existing
authorization rules. `ViewOnly` messages cannot be used, including by administrators.
Creating an email does not change the message state, assignments or rowversion.

## Sender configuration

Set `Email:FromAddress` and optionally `Email:FromDisplayName` in API configuration
or use environment variables `Email__FromAddress` and `Email__FromDisplayName`.
The sender is copied into each saved email. An empty or invalid sender causes
this endpoint to return 503; other endpoints remain available. No SMTP settings
are needed. The checked-in sender is deliberately empty.

## Request

Use `multipart/form-data` with exactly one text field `metadata` containing JSON
and zero or more file parts named `attachments`. Other fields are rejected.
The JSON properties are:

| Property | Meaning |
| --- | --- |
| `to` | Required nonempty array of `{ "address": "user@example.com", "displayName": "Optional name" }` |
| `cc`, `bcc`, `replyTo` | Same address structure; omitted arrays default to empty |
| `subject` | Required nonblank string, at most 500 characters |
| `body` | Required nonblank string, at most 100,000 characters |
| `bodyFormat` | `Text` (default) or `Html` |
| `importance` | `Low`, `Normal` (default), or `High` |
| `includeMessagePdf` | Boolean, default `false`; attaches a PDF of the stored SWIFT body |

Addresses are trimmed, at most 254 characters, and must contain a single mailbox
without an embedded display name. Display names are at most 200 characters.
Header fields cannot contain CR/LF. Addresses must be unique across To/Cc/Bcc,
ignoring case and surrounding spaces. At most 100 recipients across To/Cc/Bcc
and 10 ReplyTo addresses are accepted. Explicit null arrays are invalid.
Unknown JSON fields and numeric enum values are rejected. Address lists must
already be expanded by the caller; distribution-list IDs are not accepted.

Files are limited to 10 attachments, 10 MiB each and 20 MiB combined, including
the generated PDF. The full HTTP request is limited to 25 MiB. Only the filename
is retained, without directory components. Missing content types default to
`application/octet-stream`. The optional PDF is named `message-{messageId}.pdf`.
Files and the generated PDF are stored as bytes in SQL Server, not regenerated
when delivery is implemented later. Inline images and receipt requests are not supported.

Example for the Development debug authentication scheme:

```bash
curl --request POST 'http://localhost:5080/api/messages/123/emails' \
  --header 'X-Debug-User: developer' \
  --form-string 'metadata={"to":[{"address":"recipient@example.com","displayName":"Recipient"}],"cc":[],"bcc":[],"replyTo":[{"address":"reply@example.com"}],"subject":"SWIFT message","body":"Please review the attached message.","bodyFormat":"Text","importance":"Normal","includeMessagePdf":true}' \
  --form 'attachments=@/path/to/document.pdf;type=application/pdf'
```

For no uploaded files, omit the `attachments` part. Use `includeMessagePdf:false`
or omit that property to exclude the generated PDF.

## Response and errors

Successful persistence returns HTTP 201:

```json
{
  "id": 42,
  "messageId": 123,
  "status": "Pending",
  "createdAt": "2026-10-27T12:00:00+00:00",
  "attachmentCount": 2
}
```

There is no retrieval endpoint or Location header in this version. `Pending`
confirms storage only, not delivery or an active background worker.

Failures use the existing ProblemDetails envelope with `code`, `correlationId`
and, for field validation, `errors`:

| Status | Cause |
| --- | --- |
| 400 | Invalid multipart, metadata, recipients, headers or file metadata |
| 401 | Missing or invalid authentication |
| 403 | Insufficient scoped permissions |
| 404 | SWIFT message not found |
| 409 | ViewOnly or a transaction conflict |
| 413 | Request, file size, combined size or attachment count limit exceeded |
| 415 | Request is not multipart/form-data |
| 503 | Missing or invalid sender configuration (`email_not_configured`) |
| 500 | Unexpected persistence/PDF error, or unknown commit outcome |

Email, recipients, attachments and `EmailCreated` audit entry commit in one
transaction. An ordinary failure rolls back all these records. An unknown commit
outcome must not be blindly retried. Audit contains the actor, time and correlation
ID, but no recipients, subject, body or attachment content. The message card shows
“Email saved for sending”.

## Migration and verification

Apply the `AddOutgoingEmails` EF migration using the existing backend migration
procedure before enabling this endpoint against an existing database. This adds
`orp.OutgoingEmails`, `orp.EmailRecipients` and `orp.EmailAttachments`; source
messages and creator users are referenced with restrictive foreign keys.

Run `./test-backend.docker.sh -m:1` from the repository root. HTTP tests live in
`ORP.Importer.SqlTests` and create/drop isolated databases. The configured
development database must not be reset for testing.
