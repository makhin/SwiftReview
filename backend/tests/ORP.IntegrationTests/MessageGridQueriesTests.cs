using DevExtreme.AspNet.Data;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class MessageGridQueriesTests
{
    [Fact]
    public async Task DepartmentScope_IsAppliedBeforeDevExtremeFilters()
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseInMemoryDatabase($"message-grid-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ORPDbContext(options);
        var ct = TestContext.Current.CancellationToken;
        var users = Enumerable.Range(1, 70)
            .Select(index => new User($"user-{index}", $"User {index}"))
            .ToList();
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
        db.UserDepartments.AddRange(users.Select(user => new UserDepartment
        {
            UserId = user.Id,
            DepartmentId = 1
        }));

        for (var index = 0; index < users.Count; index++)
        {
            var id = index + 1L;
            var message = new Message(id, 1);
            message.Assign(users[index].Id);
            db.Messages.Add(message);
            db.SwiftMessages.Add(new SwiftMessageRecord
            {
                MessageId = id,
                WarehouseId = $"MSG-{id}",
                MessageType = "MT199",
                BranchId = 1,
                DepartmentId = 1,
                MessageDate = DateTimeOffset.UtcNow.AddMinutes(index),
                SenderRequestor = "A",
                ReceiverResponder = "B",
                RoutingStatus = SwiftMessageRoutingStatus.Routed,
                LoadedAtUtc = DateTimeOffset.UtcNow,
                LastSynchronizedAtUtc = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);

        var access = new UserAccess(users[0].Id, users[0].UserName,
            new HashSet<string> { Permissions.MessageView }, new HashSet<int> { 1 },
            new HashSet<int> { 1 });
        var loadOptions = new DataSourceLoadOptionsBase
        {
            Skip = 0,
            Take = 100,
            RequireTotalCount = true
        };

        var result = await new MessageGridQueries(db).LoadAsync(loadOptions, access,
            MessageAssignmentScopes.Departments, ct);

        Assert.Equal(70, result.totalCount);
        Assert.Equal(70, result.data.Cast<object>().Count());
    }

    [Fact]
    public async Task UnsupportedAssignmentScope_IsRejected()
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseInMemoryDatabase($"message-grid-{Guid.NewGuid():N}")
            .Options;
        using var db = new ORPDbContext(options);
        var access = new UserAccess(1, "user", new HashSet<string> { Permissions.MessageView },
            new HashSet<int> { 1 }, new HashSet<int> { 1 });

        await Assert.ThrowsAsync<FormatException>(() => new MessageGridQueries(db).LoadAsync(
            new DataSourceLoadOptionsBase { Take = 20 }, access, "unknown", CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MineScope_IncludesOwnerOfActiveReview(bool skipMiddleLevel)
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseInMemoryDatabase($"message-grid-active-review-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ORPDbContext(options);
        var ct = TestContext.Current.CancellationToken;
        var reviewer = new User("reviewer", "Reviewer");
        db.Users.Add(reviewer);
        await db.SaveChangesAsync(ct);

        var workflow = new WorkflowDefinition("Review", "MT199", 1).AddStep(1, 1);
        if (skipMiddleLevel)
            workflow.AddStep(2, 2, required: false).AddStep(3, 3);
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(ct);
        var message = new Message(1, workflow.Id);
        var reviews = new List<Review>();
        message.Assign(reviewer.Id);
        var activeReview = message.StartReview(1, reviewer.Id, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(activeReview);
        db.Messages.Add(message);
        db.Reviews.Add(activeReview);
        db.SwiftMessages.Add(new SwiftMessageRecord
        {
            MessageId = message.Id,
            WarehouseId = "MSG-ACTIVE",
            MessageType = "MT199",
            BranchId = 1,
            DepartmentId = 1,
            MessageDate = DateTimeOffset.UtcNow,
            SenderRequestor = "A",
            ReceiverResponder = "B",
            RoutingStatus = SwiftMessageRoutingStatus.Routed,
            LoadedAtUtc = DateTimeOffset.UtcNow,
            LastSynchronizedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var reviewerResult = await LoadMine(reviewer);
        var row = Assert.IsType<MessageGridRowDto>(Assert.Single(reviewerResult.data.Cast<object>()));

        Assert.Equal(reviewer.Id, row.CurrentAssigneeId);
        Assert.Equal(activeReview.Id, row.ActiveReviewId);
        Assert.Equal(activeReview.Level, row.ActiveReviewLevel);
        Assert.Equal(reviewer.Id, row.ActiveReviewerId);
        Assert.Equal(skipMiddleLevel ? [1, 3] : new[] { 1 }, row.RequiredReviewLevels);

        var apiResult = await new MessageGridQueries(db).LoadAsync(
            ORP.Api.Infrastructure.DevExtremeLoadOptions.Parse(
                new ORP.Api.Infrastructure.DevExtremeGridRequest(0, 20)),
            new UserAccess(reviewer.Id, reviewer.UserName, new HashSet<string> { Permissions.MessageView },
                new HashSet<int> { 1 }, new HashSet<int> { 1 }),
            MessageAssignmentScopes.Mine, ct);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(apiResult));
        Assert.Equal(row.RequiredReviewLevels,
            json.RootElement.GetProperty("data")[0].GetProperty("RequiredReviewLevels")
                .EnumerateArray().Select(level => level.GetInt32()).ToArray());

        Task<DevExtreme.AspNet.Data.ResponseModel.LoadResult> LoadMine(User user) =>
            new MessageGridQueries(db).LoadAsync(
                new DataSourceLoadOptionsBase { Take = 20 },
                new UserAccess(user.Id, user.UserName, new HashSet<string> { Permissions.MessageView },
                    new HashSet<int> { 1 }, new HashSet<int> { 1 }),
                MessageAssignmentScopes.Mine,
                ct);
    }

}
