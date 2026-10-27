using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ORP.Domain.Auditing;
using ORP.Domain.Emails;
using ORP.Domain.Identity;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using UglyToad.PdfPig;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class OutgoingEmailHttpSqlTests
{
    private const string Metadata = """
        {"to":[{"address":" user@example.com ","displayName":"Recipient"}],
         "cc":[{"address":"cc@example.com"}],"bcc":[{"address":"hidden@example.com"}],
         "replyTo":[{"address":"reply@example.com"}],"subject":"Subject","body":"<p>Hello</p>",
         "bodyFormat":"Html","importance":"High","includeMessagePdf":true}
        """;

    [Fact]
    public async Task PersistsEmailFilesPdfAndPrivateAuditWithoutChangingMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-scoped");
        await using var db = new ORPDbContext(fixture.Options);
        var before = await db.Messages.AsNoTracking().SingleAsync(ct);
        using var form = Form(Metadata, [1, 2, 3]);
        using var response = await client.PostAsync($"/api/messages/{id}/emails", form, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Pending", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("attachmentCount").GetInt32());
        var email = await db.OutgoingEmails.Include(x => x.Recipients).Include(x => x.Attachments).SingleAsync(ct);
        Assert.Equal(result.GetProperty("id").GetInt64(), email.Id);
        Assert.Equal(id, email.MessageId);
        Assert.Equal(await db.Users.Where(x => x.UserName == "email-scoped").Select(x => x.Id).SingleAsync(ct), email.CreatedByUserId);
        Assert.Equal("sender@example.com", email.FromAddress);
        Assert.Equal("ORP", email.FromDisplayName);
        Assert.Equal("Subject", email.Subject);
        Assert.Equal("<p>Hello</p>", email.Body);
        Assert.Equal(EmailBodyFormat.Html, email.BodyFormat);
        Assert.Equal(EmailImportance.High, email.Importance);
        Assert.Equal(4, email.Recipients.Count);
        Assert.Equal("user@example.com", email.Recipients.Single(x => x.Kind == EmailRecipientKind.To).Address);
        Assert.Equal("Recipient", email.Recipients.Single(x => x.Kind == EmailRecipientKind.To).DisplayName);
        Assert.Equal("hidden@example.com", email.Recipients.Single(x => x.Kind == EmailRecipientKind.Bcc).Address);
        var uploaded = email.Attachments.Single(x => x.Source == EmailAttachmentSource.Upload);
        Assert.Equal("file.bin", uploaded.FileName);
        Assert.Equal("application/octet-stream", uploaded.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, uploaded.Content);
        Assert.Equal(3, uploaded.Size);
        var attachment = email.Attachments.Single(x => x.Source == EmailAttachmentSource.MessagePdf);
        Assert.Equal($"message-{id}.pdf", attachment.FileName);
        using var pdf = PdfDocument.Open(attachment.Content);
        var body = await db.SwiftMessages.Select(x => x.Body).SingleAsync(ct);
        Assert.Equal(body.Replace("\r", "").Replace("\n", ""), string.Concat(pdf.GetPages().Select(x => x.Text)));
        var audit = await db.AuditEvents.SingleAsync(x => x.EventType == AuditEventType.EmailCreated, ct);
        Assert.Equal("{}", audit.DetailsJson);
        var after = await db.Messages.AsNoTracking().SingleAsync(ct);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.CurrentAssigneeId, after.CurrentAssigneeId);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        using var view = await client.GetAsync($"/api/messages/{id}/view", ct);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Contains("Email saved for sending", await view.Content.ReadAsStringAsync(ct));
        Assert.DoesNotContain("hidden@example.com", await view.Content.ReadAsStringAsync(ct));
        using var secondForm = Form("""{"to":[{"address":"user@example.com"}],"subject":"Again","body":"Text"}""");
        using var second = await client.PostAsync($"/api/messages/{id}/emails", secondForm, ct);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondEmail = await db.OutgoingEmails.Include(x => x.Attachments).SingleAsync(x => x.Id != email.Id, ct);
        Assert.Empty(secondEmail.Attachments);
        Assert.Equal(EmailBodyFormat.Text, secondEmail.BodyFormat);
        Assert.Equal(EmailImportance.Normal, secondEmail.Importance);
        using var openApi = await client.GetAsync("/openapi/v1.json", ct);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Contains("CreateMessageEmail", await openApi.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task DeniesAnonymousWrongScopeMissingMessageAndViewOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        async Task Check(long messageId, HttpStatusCode expected)
        {
            using var form = Form(Metadata);
            using var response = await client.PostAsync($"/api/messages/{messageId}/emails", form, ct);
            Assert.Equal(expected, response.StatusCode);
        }
        await Check(id, HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-outsider");
        await Check(id, HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        await Check(long.MaxValue, HttpStatusCode.NotFound);
        await using var db = new ORPDbContext(fixture.Options);
        var workflow = new WorkflowDefinition("EMAIL_VIEW_ONLY", "View only");
        workflow.ConfigureConfirmation(ConfirmationMode.None, false, AssignmentRetentionMode.PerConfirmation, false);
        db.Add(workflow);
        await db.SaveChangesAsync(ct);
        var message = await db.Messages.Include(x => x.Departments).SingleAsync(ct);
        message.ApplyRouting(workflow, []);
        await db.SaveChangesAsync(ct);
        await Check(id, HttpStatusCode.Conflict);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.EmailCreated, ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("sender@example.com\r\nInjected")]
    public async Task MissingOrInvalidSenderReturns503WithoutWrites(string from)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString, from);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        using var form = Form(Metadata);
        using var response = await client.PostAsync($"/api/messages/{id}/emails", form, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("email_not_configured", await response.Content.ReadAsStringAsync(ct));
        using var message = await client.GetAsync($"/api/messages/{id}", ct);
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        await using var db = new ORPDbContext(fixture.Options);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
    }

    [Fact]
    public async Task RejectsInvalidMetadataAndOversizedAttachmentsWithoutWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        foreach (var metadata in new[] { "{", "null", "{}", Metadata.Replace("Html", "Unknown"), Metadata.Replace("cc@example.com", "user@example.com") })
        {
            using var form = Form(metadata);
            using var response = await client.PostAsync($"/api/messages/{id}/emails", form, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var oversized = Form(Metadata, new byte[10 * 1024 * 1024 + 1]);
        using var tooLarge = await client.PostAsync($"/api/messages/{id}/emails", oversized, ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        using var many = Form(Metadata);
        for (var i = 0; i < 10; i++) many.Add(new ByteArrayContent([1]), "attachments", $"file{i}.bin");
        using var tooMany = await client.PostAsync($"/api/messages/{id}/emails", many, ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooMany.StatusCode);
        await using var db = new ORPDbContext(fixture.Options);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
        Assert.Empty(await db.Set<EmailAttachment>().ToListAsync(ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.EmailCreated, ct));
    }

    [Fact]
    public async Task FailureAfterSaveRollsBackEmailRecipientsAttachmentsAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString, failSave: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        using var form = Form(Metadata, [1, 2, 3]);
        using var response = await client.PostAsync($"/api/messages/{id}/emails", form, ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var db = new ORPDbContext(fixture.Options);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
        Assert.Empty(await db.Set<EmailRecipient>().ToListAsync(ct));
        Assert.Empty(await db.Set<EmailAttachment>().ToListAsync(ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.EmailCreated, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteMultipartReturns400WithoutWrites(bool truncatedFile)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        var body = "--regression\r\nContent-Disposition: form-data; name=\"metadata\"\r\n\r\n" + Metadata;
        if (truncatedFile)
            body += "\r\n--regression\r\nContent-Disposition: form-data; name=\"attachments\"; filename=\"test.txt\"\r\nContent-Type: text/plain\r\n\r\npartial file";
        // The HTTP body is complete, but the multipart closing delimiter is absent.
        using var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body));
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=regression");
        using var response = await client.PostAsync($"/api/messages/{id}/emails", content, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
        await using var db = new ORPDbContext(fixture.Options);
        Assert.False(await db.OutgoingEmails.AnyAsync(ct));
        Assert.False(await db.Set<EmailRecipient>().AnyAsync(ct));
        Assert.False(await db.Set<EmailAttachment>().AnyAsync(ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.EmailCreated, ct));
    }

    private static MultipartFormDataContent Form(string metadata, byte[]? file = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(metadata), "metadata");
        if (file is not null) form.Add(new ByteArrayContent(file), "attachments", "C:\\uploads\\file.bin");
        return form;
    }

    private static async Task<long> Seed(SqlImportDatabase fixture)
    {
        var ct = TestContext.Current.CancellationToken;
        var file = fixture.File("Email PDF body", "email.prt");
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        await using var db = new ORPDbContext(fixture.Options);
        var user = new User("email-scoped", "Email scoped user");
        var role = new Role("Email sender");
        db.AddRange(user, role, new User("email-admin", "Email Admin", true), new User("email-outsider", "Outsider"));
        await db.SaveChangesAsync(ct);
        var permissions = await db.Permissions.Where(x => x.Name == Permissions.MessageView || x.Name == Permissions.MessageForward).ToListAsync(ct);
        foreach (var permission in permissions) db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id,
            BranchId = await db.Branches.Select(x => x.Id).SingleAsync(ct), DepartmentId = await db.Departments.Select(x => x.Id).SingleAsync(ct) });
        await db.SaveChangesAsync(ct);
        return await db.Messages.Select(x => x.Id).SingleAsync(ct);
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected post-save failure.");
    }

    private sealed class Factory(string connection, string from = "sender@example.com", bool failSave = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:ORP", connection)
                .UseSetting("BootstrapDatabase", "false").UseSetting("Email:FromAddress", from)
                .UseSetting("Email:FromDisplayName", "ORP").ConfigureLogging(logging => logging.ClearProviders());
            if (failSave) builder.ConfigureServices(services => services.AddDbContext<ORPDbContext>(options => options.AddInterceptors(new FailAfterSave())));
        }
    }
}
