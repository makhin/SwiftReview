using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MessageReferencesHttpSqlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllMessageResponsesExposeStoredReferences(bool hasReferences)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File(hasReferences
            ? "--- Message Text ---\n        20: Transaction Reference Number\n            8801IC000673\n        21: Related Reference\n            1234"
            : "message without reference fields");
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        long id;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            db.Users.Add(new User("reference-reader", "Reference reader", true));
            await db.SaveChangesAsync(ct);
            id = await db.Messages.Select(m => m.Id).SingleAsync(ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "reference-reader");
        foreach (var endpoint in new[] { "grid", "search", $"{id}", $"{id}/view" })
        {
            using var response = endpoint switch
            {
                "grid" => await client.PostAsJsonAsync("/api/messages/grid", new { page = 1, pageSize = 20 }, ct),
                "search" => await client.PostAsJsonAsync("/api/messages/search", new { skip = 0, take = 20 }, ct),
                _ => await client.GetAsync($"/api/messages/{endpoint}", ct)
            };
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var message = endpoint is "grid" or "search"
                ? Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()) : json.RootElement;
            Assert.Equal(hasReferences ? "8801IC000673" : null, message.GetProperty("transactionReferenceNumber").GetString());
            Assert.Equal(hasReferences ? "1234" : null, message.GetProperty("relatedReference").GetString());
            Assert.Equal("test", message.GetProperty(endpoint.EndsWith("/view", StringComparison.Ordinal) ? "reference" : "externalId").GetString());
        }
        using var schemaResponse = await client.GetAsync("/openapi/v1.json", ct);
        Assert.Equal(HttpStatusCode.OK, schemaResponse.StatusCode);
        using var schema = JsonDocument.Parse(await schemaResponse.Content.ReadAsStringAsync(ct));
        var schemas = schema.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "MessageGridRowDto", "MessageListItemDto", "MessageDetailsDto", "MessageViewDto" })
        {
            var properties = schemas.GetProperty(name).GetProperty("properties");
            Assert.True(properties.TryGetProperty("transactionReferenceNumber", out _), name);
            Assert.True(properties.TryGetProperty("relatedReference", out _), name);
        }
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
