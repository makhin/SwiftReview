using DevExtreme.AspNet.Data;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
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
            db.SwiftMessageSource.Add(new SwiftMessageRecord
            {
                MessageId = id,
                ExternalId = $"MSG-{id}",
                MessageType = "MT199",
                BranchId = 1,
                DepartmentId = 1,
                ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(index),
                Sender = "A",
                Receiver = "B"
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
}
