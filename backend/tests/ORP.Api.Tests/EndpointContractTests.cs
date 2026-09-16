using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ORP.Api.Tests;

public sealed class EndpointContractTests(MessageApiFactory factory) : IClassFixture<MessageApiFactory>
{
    private static readonly string[] ExpectedOperations =
    [
        "GET /api/messages/state-counts", "GET /api/messages/grid", "GET /api/messages/{id}",
        "PUT /api/messages/{id}/workflow", "POST /api/messages/search", "POST /api/messages/{id}/assign",
        "POST /api/messages/{id}/reassign", "GET /api/messages/{id}/assignment-candidates",
        "GET /api/messages/{id}/audit", "POST /api/messages/{id}/reviews/start",
        "POST /api/messages/{id}/reviews/approve", "POST /api/messages/{id}/reviews/reject",
        "POST /api/messages/{id}/reviews/cancel", "POST /api/messages/{id}/reviews/undo",
        "GET /api/workflows", "GET /api/users", "GET /api/branches", "GET /api/departments",
        "GET /api/message-types", "GET /api/message-states", "GET /api/dashboard/summary", "GET /api/me",
        "GET /api/admin/catalog", "GET /api/admin/users/{id}/access", "PUT /api/admin/users/{id}/access",
        "PUT /api/admin/roles/{id}/permissions", "GET /api/admin/users/grid"
    ];

    [Fact]
    public void Routes_AreUniqueNamedAndAuthorized()
    {
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Where(e => e.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal)).ToArray();
        var operations = endpoints.SelectMany(e => e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods
            .Select(method => $"{method} {Regex.Replace(e.RoutePattern.RawText!, @"\{id:[^}]+\}", "{id}")}")).ToArray();
        Assert.Equal(ExpectedOperations.Order(), operations.Order());
        var names = endpoints.Select(e => e.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        foreach (var endpoint in endpoints)
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.NotEmpty(authorization);
            if (endpoint.RoutePattern.RawText!.StartsWith("/api/admin/", StringComparison.Ordinal) ||
                endpoint.RoutePattern.RawText.EndsWith("/reviews/undo", StringComparison.Ordinal))
                Assert.Contains(authorization, a => a.Policy == "GlobalAdministrator");
        }
    }

    [Fact]
    public async Task AllApiOperations_RequireAuthentication()
    {
        using var client = factory.CreateClient();
        foreach (var operation in ExpectedOperations)
        {
            var parts = operation.Split(' ');
            using var request = new HttpRequestMessage(new HttpMethod(parts[0]), parts[1].Replace("{id}", "1"));
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task MessageGrid_OpenApiDescribesCompleteRequiredRowsMatchingRuntime()
    {
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", ct);
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var response = document.GetProperty("paths").GetProperty("/api/messages/grid").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/MessageGridLoadResultDto", response.GetProperty("anyOf")[0].GetProperty("$ref").GetString());
        Assert.True(JsonElement.DeepEquals(schemas.GetProperty("LoadResult").GetProperty("properties"),
            response.GetProperty("anyOf")[1].GetProperty("properties")));
        var envelope = schemas.GetProperty("MessageGridLoadResultDto");
        Assert.Equal("#/components/schemas/MessageGridRowDto", envelope.GetProperty("properties")
            .GetProperty("data").GetProperty("items").GetProperty("$ref").GetString());
        var rowSchema = schemas.GetProperty("MessageGridRowDto");
        var properties = rowSchema.GetProperty("properties");
        var names = properties.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(names, rowSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
        Assert.Equal("boolean", properties.GetProperty("canReview").GetProperty("type").GetString());
        Assert.Equal("boolean", properties.GetProperty("canChangeWorkflow").GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("workflowDefinitionId", out _));
        Assert.True(properties.TryGetProperty("undoReviewId", out _));
        Assert.Equal("array", properties.GetProperty("requiredReviewLevels").GetProperty("type").GetString());

        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        var result = await client.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=10&requireTotalCount=true", ct);
        Assert.NotEmpty(result.GetProperty("data").EnumerateArray());
        foreach (var row in result.GetProperty("data").EnumerateArray())
            Assert.Equal(names, row.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task OpenApi_DescribesOperationsSuccessAndProblemResponses()
    {
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        var paths = document.GetProperty("paths");
        var operations = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Select(method => (Key: $"{method.Name.ToUpperInvariant()} {path.Name}", Value: method.Value))).ToArray();
        Assert.Equal(ExpectedOperations.Order(), operations.Select(o => o.Key).Order());
        var ids = operations.Select(o => o.Value.GetProperty("operationId").GetString()).ToArray();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Length, ids.Distinct().Count());
        foreach (var operation in operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("summary").GetString()));
            Assert.NotEmpty(operation.Value.GetProperty("tags").EnumerateArray());
            var responses = operation.Value.GetProperty("responses");
            foreach (var status in new[] { "401", "403" })
                Assert.True(responses.GetProperty(status).GetProperty("content")
                    .GetProperty("application/problem+json").TryGetProperty("schema", out _));
            var read = operation.Key.StartsWith("GET ", StringComparison.Ordinal) ||
                operation.Key == "POST /api/messages/search" || operation.Key.EndsWith("/reviews/start", StringComparison.Ordinal);
            var success = read ? "200" : "204";
            Assert.Equal(new[] { success }, responses.EnumerateObject().Where(r => r.Name.StartsWith('2')).Select(r => r.Name));
            if (read)
                Assert.True(responses.GetProperty(success).GetProperty("content")
                    .GetProperty("application/json").TryGetProperty("schema", out _));
        }
        foreach (var path in new[] { "/api/admin/users/{id}/access", "/api/admin/roles/{id}/permissions" })
        {
            var responses = paths.GetProperty(path).GetProperty("put").GetProperty("responses");
            foreach (var status in new[] { "400", "404", "409" })
                Assert.True(responses.GetProperty(status).GetProperty("content")
                    .GetProperty("application/problem+json").TryGetProperty("schema", out _));
        }
    }
}
