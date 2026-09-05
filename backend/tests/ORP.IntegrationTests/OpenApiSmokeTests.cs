using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class OpenApiSmokeTests
{
    [Fact]
    public async Task ApiHost_PublishesOrvalReadyContract()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Production");
            web.UseSetting("UseMockData", "true");
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = json.RootElement;

        var paths = root.GetProperty("paths");
        foreach (var path in RequiredPaths) Assert.True(paths.TryGetProperty(path, out _), $"OpenAPI path is missing: {path}");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(500, schemas.GetProperty("MessageSearchRequest").GetProperty("properties").GetProperty("take").GetProperty("maximum").GetInt32());
        Assert.False(schemas.GetProperty("MessageDetailsDto").GetProperty("properties").TryGetProperty("rowVersion", out _));
        Assert.False(schemas.GetProperty("AssignMessageRequest").GetProperty("properties").TryGetProperty("rowVersion", out _));
        Assert.Equal(3, schemas.GetProperty("StartReviewRequest").GetProperty("properties").GetProperty("level").GetProperty("maximum").GetInt32());
        Assert.Contains("Completed", schemas.GetProperty("MessageState").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain(schemas.GetProperty("MessageState").GetProperty("enum").EnumerateArray(),
            value => value.ValueKind == JsonValueKind.Null);

        var approveResponses = paths.GetProperty("/api/messages/{id}/reviews/approve").GetProperty("post").GetProperty("responses");
        foreach (var status in new[] { "204", "400", "403", "404", "409" }) Assert.True(approveResponses.TryGetProperty(status, out _));
        var gridParameters = paths.GetProperty("/api/messages/grid").GetProperty("get").GetProperty("parameters")
            .EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToHashSet();
        foreach (var parameter in new[] { "skip", "take", "sort", "filter", "group", "totalSummary", "groupSummary", "requireTotalCount", "requireGroupCount", "assignmentScope" })
            Assert.Contains(parameter, gridParameters);
        Assert.Contains("null", schemas.GetProperty("ApproveReviewRequest").GetProperty("properties").GetProperty("comment").GetProperty("type")
            .EnumerateArray().Select(x => x.GetString()));
        Assert.True(schemas.GetProperty("ReferenceItemDto").GetProperty("properties").TryGetProperty("id", out _));
        Assert.True(schemas.GetProperty("ReferenceItemDto").GetProperty("properties").TryGetProperty("name", out _));
        Assert.True(schemas.GetProperty("MessageStateReferenceDto").GetProperty("properties").TryGetProperty("code", out _));
        Assert.True(schemas.GetProperty("MessageStateReferenceDto").GetProperty("properties").TryGetProperty("label", out _));
        var auditOperation = paths.GetProperty("/api/messages/{id}/audit").GetProperty("get");
        var auditParameters = auditOperation.GetProperty("parameters").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("skip", auditParameters);
        Assert.Contains("take", auditParameters);
        foreach (var status in new[] { "200", "400", "403", "404" })
            Assert.True(auditOperation.GetProperty("responses").TryGetProperty(status, out _));
        var auditProperties = schemas.GetProperty("AuditEventDto").GetProperty("properties");
        Assert.True(auditProperties.TryGetProperty("actor", out _));
        Assert.True(auditProperties.TryGetProperty("details", out _));
        Assert.False(auditProperties.TryGetProperty("detailsJson", out _));
        Assert.False(auditProperties.GetProperty("oldState").TryGetProperty("oneOf", out _));
        Assert.False(auditProperties.GetProperty("newState").TryGetProperty("oneOf", out _));
        Assert.Contains("MessageRegistered", schemas.GetProperty("AuditEventType").GetProperty("enum")
            .EnumerateArray().Select(x => x.GetString()));

        Assert.False(paths.TryGetProperty("/api/messages/import", out _));
        Assert.True(paths.GetProperty("/api/messages/{id}/reviews/start").GetProperty("post").GetProperty("responses")
            .GetProperty("201").GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));
        Assert.True(paths.GetProperty("/api/me").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));

        using var scalar = await client.GetAsync("/scalar", ct);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Contains("ORP API", await scalar.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    private static readonly string[] RequiredPaths =
    [
        "/api/messages/{id}", "/api/messages/grid", "/api/messages/search", "/api/messages/{id}/assign", "/api/messages/{id}/reassign",
        "/api/messages/{id}/reviews/start", "/api/messages/{id}/reviews/approve", "/api/messages/{id}/reviews/reject",
        "/api/messages/{id}/undo", "/api/messages/{id}/audit", "/api/dashboard/summary", "/api/me", "/api/workflows", "/api/users",
        "/api/branches", "/api/departments", "/api/message-types", "/api/message-states"
    ];
}
