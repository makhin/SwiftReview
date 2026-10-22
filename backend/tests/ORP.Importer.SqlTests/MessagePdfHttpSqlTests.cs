using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Domain.Identity;
using ORP.Domain.Auditing;
using ORP.Infrastructure.Persistence;
using UglyToad.PdfPig;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MessagePdfHttpSqlTests
{
    [Fact]
    public async Task ReturnsBodyAsPdfOnlyForAccessibleMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File("PDF body", "pdf-message.prt");
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        long id;
        string body;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            db.AddRange(new User("pdf-admin", "PDF Admin", true), new User("pdf-outsider", "PDF Outsider"));
            await db.SaveChangesAsync(ct);
            id = await db.Messages.Select(message => message.Id).SingleAsync(ct);
            body = await db.SwiftMessages.Select(message => message.Body).SingleAsync(ct);
        }

        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "pdf-admin");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "pdf-audit-test");
        var before = DateTimeOffset.UtcNow;
        using var response = await client.GetAsync($"/api/messages/{id}/pdf", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(ct));
        // Positioned line breaks are tested separately by the converter tests.
        Assert.Equal(body.Replace("\r", "").Replace("\n", ""),
            string.Concat(pdf.GetPages().Select(page => page.Text)));

        await using (var db = new ORPDbContext(fixture.Options))
        {
            var audit = await db.AuditEvents.SingleAsync(x => x.EventType == AuditEventType.MessagePdfGenerated, ct);
            Assert.Equal(id, audit.MessageId);
            Assert.Equal(await db.Users.Where(x => x.UserName == "pdf-admin").Select(x => x.Id).SingleAsync(ct), audit.UserId);
            Assert.InRange(audit.Timestamp, before, DateTimeOffset.UtcNow);
            Assert.Equal("pdf-audit-test", audit.CorrelationId);
            Assert.Null(audit.OldState);
            Assert.Null(audit.NewState);
            Assert.Null(audit.ReviewId);
        }

        using var missing = await client.GetAsync("/api/messages/9223372036854775807/pdf", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "pdf-outsider");
        using var denied = await client.GetAsync($"/api/messages/{id}/pdf", ct);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(1, await verify.AuditEvents.CountAsync(x => x.EventType == AuditEventType.MessagePdfGenerated, ct));
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection)
            .UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
