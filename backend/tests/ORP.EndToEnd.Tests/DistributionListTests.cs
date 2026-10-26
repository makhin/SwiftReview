using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ORP.Application.DistributionLists;
using ORP.Domain.DistributionLists;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.EndToEnd.Tests;

public sealed class DistributionListTests
{
    private static string Route(int departmentId, int? id = null) =>
        $"/api/departments/{departmentId}/distribution-lists" + (id is null ? "" : $"/{id}");

    [Fact]
    public async Task CrudTrimsValuesReplacesEmailsAndDeletesChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var manager = env.Client();
        using var forwarder = env.Client("forwarder");
        var created = await CreateAsync(manager, env.DepartmentId, " Operations ", [" z@example.com ", "a@example.com"], ct);
        Assert.Equal("Operations", created.Name);
        Assert.Equal(["a@example.com", "z@example.com"], created.Emails);
        var path = Route(env.DepartmentId, created.Id);
        Assert.Equal(created.Id, (await forwarder.GetFromJsonAsync<DistributionListDto>(path, ct))!.Id);
        await using var db = new ORPDbContext(env.Options);
        var originalVersion = await db.DistributionLists.Where(x => x.Id == created.Id).Select(x => x.RowVersion).SingleAsync(ct);

        using var update = await manager.PutAsJsonAsync(path,
            new SaveDistributionListRequest("Operations", [" A@example.com ", "new@example.com"]), ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await update.Content.ReadAsStringAsync(ct);
        var updated = JsonSerializer.Deserialize<DistributionListDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(["A@example.com", "new@example.com"], updated.Emails);
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(["departmentId", "emails", "id", "name"], json.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.False(update.Headers.Contains("ETag"));
        var newVersion = await db.DistributionLists.Where(x => x.Id == created.Id).Select(x => x.RowVersion).SingleAsync(ct);
        Assert.False(originalVersion.SequenceEqual(newVersion));
        Assert.Equal(updated.Emails, await db.DistributionListEmails.Where(x => x.DistributionListId == created.Id)
            .OrderBy(x => x.Email).Select(x => x.Email).ToArrayAsync(ct));

        using var empty = await manager.PutAsJsonAsync(path, new SaveDistributionListRequest("Renamed", []), ct);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<DistributionListDto>(ct))!.Emails);
        using var refill = await manager.PutAsJsonAsync(path, new SaveDistributionListRequest("Renamed", ["delete@example.com"]), ct);
        Assert.Equal(HttpStatusCode.OK, refill.StatusCode);
        using var deleted = await manager.DeleteAsync(path, ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await db.DistributionLists.AnyAsync(x => x.Id == created.Id, ct));
        Assert.False(await db.DistributionListEmails.AnyAsync(x => x.DistributionListId == created.Id, ct));
        using var missing = await manager.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData("viewer", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("outsider", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("forwarder", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    public async Task HttpPermissionsAreDepartmentScopedAndDoNotRequireMessageView(string? user,
        HttpStatusCode readStatus, HttpStatusCode writeStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var manager = env.Client();
        var list = await CreateAsync(manager, env.DepartmentId, "Protected", ["keep@example.com"], ct);
        using var client = env.Client(user);
        using var all = await client.GetAsync(Route(env.DepartmentId), ct);
        using var single = await client.GetAsync(Route(env.DepartmentId, list.Id), ct);
        using var create = await client.PostAsJsonAsync(Route(env.DepartmentId), new SaveDistributionListRequest("Denied", []), ct);
        using var update = await client.PutAsJsonAsync(Route(env.DepartmentId, list.Id), new SaveDistributionListRequest("Denied", []), ct);
        using var delete = await client.DeleteAsync(Route(env.DepartmentId, list.Id), ct);
        Assert.Equal(readStatus, all.StatusCode);
        Assert.Equal(readStatus, single.StatusCode);
        foreach (var response in new[] { create, update, delete })
        {
            Assert.Equal(writeStatus, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        var saved = (await manager.GetFromJsonAsync<DistributionListDto>(Route(env.DepartmentId, list.Id), ct))!;
        Assert.Equal("Protected", saved.Name);
        Assert.Equal(["keep@example.com"], saved.Emails);
        await using var db = new ORPDbContext(env.Options);
        Assert.Equal(1, await db.DistributionLists.CountAsync(x => x.DepartmentId == env.DepartmentId, ct));
    }

    [Fact]
    public async Task AdminCanUseAllDepartmentsButListIdsCannotCrossDepartments()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var admin = env.Client("admin");
        var list = await CreateAsync(admin, env.DepartmentId, "Shared name", [], ct);
        var other = await CreateAsync(admin, env.OtherDepartmentId, "Shared name", ["other@example.com"], ct);
        var wrong = Route(env.OtherDepartmentId, list.Id);
        using var read = await admin.GetAsync(wrong, ct);
        using var update = await admin.PutAsJsonAsync(wrong, new SaveDistributionListRequest("Wrong", []), ct);
        using var delete = await admin.DeleteAsync(wrong, ct);
        Assert.All(new[] { read, update, delete }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        var all = (await admin.GetFromJsonAsync<DistributionListDto[]>(Route(env.OtherDepartmentId), ct))!;
        Assert.Equal(other.Id, Assert.Single(all).Id);
        using var missingDepartment = await admin.GetAsync(Route(int.MaxValue), ct);
        Assert.Equal(HttpStatusCode.NotFound, missingDepartment.StatusCode);
        using var missingList = await admin.DeleteAsync(Route(env.DepartmentId, int.MaxValue), ct);
        Assert.Equal(HttpStatusCode.NotFound, missingList.StatusCode);
        using var rename = await admin.PutAsJsonAsync(Route(env.OtherDepartmentId, other.Id), new SaveDistributionListRequest("Renamed", []), ct);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        using var removed = await admin.DeleteAsync(Route(env.OtherDepartmentId, other.Id), ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [Fact]
    public async Task ValidationAndUniqueNameConflictsPreserveTheWholeList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var manager = env.Client();
        var first = await CreateAsync(manager, env.DepartmentId, "Alpha", ["shared@example.com"], ct);
        var second = await CreateAsync(manager, env.DepartmentId, "Beta", ["shared@example.com"], ct);
        using var duplicate = await manager.PostAsJsonAsync(Route(env.DepartmentId), new SaveDistributionListRequest(" alpha ", []), ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var conflict = await manager.PutAsJsonAsync(Route(env.DepartmentId, second.Id),
            new SaveDistributionListRequest("ALPHA", ["replacement@example.com"]), ct);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        foreach (var invalid in new[]
                 { new SaveDistributionListRequest(" ", []), new("Invalid", null!),
                     new("Invalid", ["Name <email@example.com>"]), new("Invalid", ["a@example.com", " A@example.com "]) })
        {
            using var response = await manager.PutAsJsonAsync(Route(env.DepartmentId, second.Id), invalid, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("validation_failed", error.GetProperty("code").GetString());
            Assert.True(error.TryGetProperty("errors", out _));
        }
        var saved = (await manager.GetFromJsonAsync<DistributionListDto>(Route(env.DepartmentId, second.Id), ct))!;
        Assert.Equal("Beta", saved.Name);
        Assert.Equal(["shared@example.com"], saved.Emails);
        var sorted = (await manager.GetFromJsonAsync<DistributionListDto[]>(Route(env.DepartmentId), ct))!;
        Assert.Equal([first.Id, second.Id], sorted.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task DatabaseEnforcesCaseInsensitiveEmailUniquenessAndCascadesWithoutLoadingChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var client = env.Client();
        var list = await CreateAsync(client, env.DepartmentId, "Database constraints", ["one@example.com"], ct);
        await using var db = new ORPDbContext(env.Options);
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO orp.DistributionListEmails (DistributionListId, Email) VALUES ({list.Id}, {"ONE@example.com"})", ct));
        await db.DistributionLists.Where(x => x.Id == list.Id).ExecuteDeleteAsync(ct);
        Assert.False(await db.DistributionListEmails.AnyAsync(x => x.DistributionListId == list.Id, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChildOnlyChangesAdvanceRowVersionAndRejectStaleTrackedWrites(bool delete)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var client = env.Client();
        var list = await CreateAsync(client, env.DepartmentId, "Rowversion", ["old@example.com"], ct);
        await using var winner = new ORPDbContext(env.Options);
        await using var stale = new ORPDbContext(env.Options);
        var first = await winner.DistributionLists.Include(x => x.Emails).SingleAsync(x => x.Id == list.Id, ct);
        var second = await stale.DistributionLists.Include(x => x.Emails).SingleAsync(x => x.Id == list.Id, ct);
        Assert.Equal(first.RowVersion, second.RowVersion);
        var oldVersion = first.RowVersion.ToArray();
        // Both contexts have read the same version before either writes; changing only children must update the parent.
        first.Replace(first.Name, ["old@example.com", "winner@example.com"]);
        await winner.SaveChangesAsync(ct);
        Assert.False(oldVersion.SequenceEqual(first.RowVersion));
        if (delete) stale.DistributionLists.Remove(second);
        else second.Replace(second.Name, ["loser@example.com"]);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(ct));
        await using var persisted = new ORPDbContext(env.Options);
        Assert.Equal(["old@example.com", "winner@example.com"], await persisted.DistributionListEmails.Where(x => x.DistributionListId == list.Id)
            .OrderBy(x => x.Email).Select(x => x.Email).ToArrayAsync(ct));
        Assert.True(await persisted.DistributionLists.AnyAsync(x => x.Id == list.Id, ct));
    }

    [Fact]
    public async Task OverlappingHttpUpdatesReturnConflictWithoutRetryOrPartialChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        using var client = env.Client();
        var list = await CreateAsync(client, env.DepartmentId, "Concurrent", ["old@example.com"], ct);
        env.Barrier.ListId = list.Id;
        // The save interceptor holds both requests after their reads, before either starts saving.
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync(Route(env.DepartmentId, list.Id), new SaveDistributionListRequest("First", ["first@example.com"]), ct),
            client.PutAsJsonAsync(Route(env.DepartmentId, list.Id), new SaveDistributionListRequest("Second", ["second@example.com"]), ct));
        try
        {
            Assert.Equal(2, env.Barrier.Arrivals);
            var success = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("concurrent_update", (await conflict.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString());
            var result = (await success.Content.ReadFromJsonAsync<DistributionListDto>(ct))!;
            await using var db = new ORPDbContext(env.Options);
            var persisted = await db.DistributionLists.Include(x => x.Emails).SingleAsync(x => x.Id == list.Id, ct);
            Assert.Equal(result.Name, persisted.Name);
            Assert.Equal(result.Emails, persisted.Emails.Select(x => x.Email).ToArray());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task MigrationGrantsReviewerPermissionsAndAdminCatalogExposesThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await DistributionListEnvironment.CreateAsync(ct);
        await using var db = new ORPDbContext(env.Options);
        var reviewer = await db.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == "Reviewer", ct);
        foreach (var permission in new[] { Permissions.MessageForward, Permissions.DistributionListManage })
            Assert.Contains(reviewer.Permissions, x => x.Permission.Name == permission);
        using var admin = env.Client("admin");
        var catalog = await admin.GetFromJsonAsync<JsonElement>("/api/admin/catalog", ct);
        var permissions = catalog.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(Permissions.MessageForward, permissions);
        Assert.Contains(Permissions.DistributionListManage, permissions);
    }

    private static async Task<DistributionListDto> CreateAsync(HttpClient client, int departmentId, string name,
        IReadOnlyList<string> emails, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(Route(departmentId), new SaveDistributionListRequest(name, emails), ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<DistributionListDto>(ct))!;
        Assert.Equal(Route(departmentId, dto.Id), response.Headers.Location?.OriginalString);
        return dto;
    }
}
