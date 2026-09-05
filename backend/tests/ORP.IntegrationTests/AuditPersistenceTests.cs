using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class AuditPersistenceTests
{
    [Fact]
    public async Task AuditEvents_AreAppendOnlyThroughSyncAndAsyncSaveChanges()
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ORPDbContext(options);
        var ct = TestContext.Current.CancellationToken;
        var message = new Message(1, 1);
        var audit = new AuditEvent(message, AuditEventType.MessageAssigned, null,
            DateTimeOffset.UtcNow, MessageState.New, MessageState.Assigned,
            "{\"assignedTo\":7,\"level\":1}", "test");
        var source = new SwiftMessageRecord
        {
            MessageId = 1,
            ExternalId = "LEGACY",
            MessageType = "MT199",
            BranchId = 1,
            DepartmentId = 1,
            ReceivedAt = DateTimeOffset.UtcNow,
            Sender = "A",
            Receiver = "B"
        };
        db.AddRange(message, audit, source);
        await db.SaveChangesAsync(ct);

        var access = new UserAccess(1, "auditor", new HashSet<string> { Domain.Identity.Permissions.AuditView },
            new HashSet<int> { 1 }, new HashSet<int> { 1 });
        var page = await new MessageQueries(db).AuditAsync(1, new AuditTrailRequest(0, 10), access, ct);
        var item = Assert.Single(page!.Items);
        Assert.Equal(7, item.Details.AssigneeId);
        Assert.Equal(1, item.Details.ReviewLevel);

        db.Entry(audit).Property(x => x.CorrelationId).CurrentValue = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));

        db.Entry(audit).State = EntityState.Unchanged;
        db.Remove(audit);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
