using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ORP.Domain.Identity;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.EndToEnd.Tests;

public sealed class ImportToApprovalTests
{
    [Fact]
    public async Task ImportedFileCompletesThroughTwoIndependentHttpApprovals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await ImportApprovalEnvironment.CreateAsync(ct);
        var id = await ImportAsync(env, ct);
        using var first = env.Client("e2e.first");
        using var second = env.Client("e2e.second");
        var initial = await DetailsAsync(first, id, ct);
        Assert.Equal("New", initial.GetProperty("state").GetString());
        Assert.Equal(env.MessageType, initial.GetProperty("messageType").GetString());
        Assert.Equal(env.BranchName, initial.GetProperty("branchName").GetString());
        Assert.Equal(env.DepartmentName, initial.GetProperty("departmentNames").GetString());
        Assert.Equal("Immediate", initial.GetProperty("confirmationMode").GetString());
        await CompleteAsync(env, id, first, second, ct);
        await AssertCompletedAsync(env, id, second, ct);
    }

    [Fact]
    public async Task ReimportAfterCompletionDoesNotDuplicateOrResetMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await ImportApprovalEnvironment.CreateAsync(ct);
        var id = await ImportAsync(env, ct);
        using var first = env.Client("e2e.first");
        using var second = env.Client("e2e.second");
        await CompleteAsync(env, id, first, second, ct);
        var before = await SnapshotAsync(env, id, ct);
        Assert.Equal(SaaFileImportResult.AlreadyImported, await env.ImportAsync(ct));
        Assert.Equal(before, await SnapshotAsync(env, id, ct));
        await AssertCompletedAsync(env, id, second, ct);
    }

    [Fact]
    public async Task NonAssigneeCannotApproveAndAssignedReviewerCanContinue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await ImportApprovalEnvironment.CreateAsync(ct);
        var id = await ImportAsync(env, ct);
        using var first = env.Client("e2e.first");
        using var second = env.Client("e2e.second");
        await AssignAsync(first, id, env.FirstReviewerId, ct);
        var before = await SnapshotAsync(env, id, ct);
        using var denied = await second.PostAsJsonAsync($"/api/messages/{id}/reviews/confirm", new { level = 1 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(before, await SnapshotAsync(env, id, ct));
        await ConfirmAsync(first, id, 1, ct);
        await AssignAsync(second, id, env.SecondReviewerId, ct);
        await ConfirmAsync(second, id, 2, ct);
        await AssertCompletedAsync(env, id, second, ct);
    }

    [Fact]
    public async Task AlreadyApprovedLevelCannotBeConfirmedAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await ImportApprovalEnvironment.CreateAsync(ct);
        var id = await ImportAsync(env, ct);
        using var first = env.Client("e2e.first");
        using var second = env.Client("e2e.second");
        await AssignAsync(first, id, env.FirstReviewerId, ct);
        await ConfirmAsync(first, id, 1, ct);
        await AssignAsync(second, id, env.SecondReviewerId, ct);
        // The current owner has permission for level 1: rejection must come from
        // workflow validation, rather than the ownership check tested above.
        var before = await SnapshotAsync(env, id, ct);
        using var duplicate = await second.PostAsJsonAsync($"/api/messages/{id}/reviews/confirm", new { level = 1 }, ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(before, await SnapshotAsync(env, id, ct));
        await ConfirmAsync(second, id, 2, ct);
        await AssertCompletedAsync(env, id, second, ct);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CleanupRemovesAllOwnFixturesAndPreservesEarlierRun(bool useRangeScript, bool import)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var earlier = await ImportApprovalEnvironment.CreateAsync(ct);
        var earlierId = await ImportAsync(earlier, ct);
        var before = await SnapshotAsync(earlier, earlierId, ct);
        var current = await ImportApprovalEnvironment.CreateAsync(ct);
        await using (current)
        {
            if (import)
            {
                var id = await ImportAsync(current, ct);
                using var first = current.Client("e2e.first");
                using var second = current.Client("e2e.second");
                await CompleteAsync(current, id, first, second, ct);
                if (useRangeScript)
                {
                    var currentBefore = await SnapshotAsync(current, id, ct);
                    await RunRangeCleanupAsync(current, id, true, ct);
                    Assert.Equal(currentBefore, await SnapshotAsync(current, id, ct));
                    await RunRangeCleanupAsync(current, id, false, ct);
                    await AssertCleanedAsync(current, ct);
                }
            }
            else
            {
                // A failed/ignored import can leave a disposition without a message.
                await using var db = new ORPDbContext(current.Options);
                db.IgnoredImportFiles.Add(new IgnoredImportFile { FilePath = current.FileName,
                    ContentHash = new string('a', 64), IgnoredAtUtc = DateTimeOffset.UtcNow, Reason = "NoDepartmentRule" });
                await db.SaveChangesAsync(ct);
            }
        }
        await AssertCleanedAsync(current, ct);
        Assert.Equal(before, await SnapshotAsync(earlier, earlierId, ct));
        // Verify that the earlier run's directories and permissions still work.
        using var earlierFirst = earlier.Client("e2e.first");
        using var earlierSecond = earlier.Client("e2e.second");
        await CompleteAsync(earlier, earlierId, earlierFirst, earlierSecond, ct);
        await AssertCompletedAsync(earlier, earlierId, earlierSecond, ct);
    }

    [Fact]
    public async Task CleanupRollsBackRatherThanDeleteAnotherRunsRoleGrant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var earlier = await ImportApprovalEnvironment.CreateAsync(ct);
        await using var current = await ImportApprovalEnvironment.CreateAsync(ct);
        var id = await ImportAsync(current, ct);
        int roleId;
        await using (var db = new ORPDbContext(current.Options))
        {
            roleId = await db.Roles.Where(r => r.Name == current.BranchName).Select(r => r.Id).SingleAsync(ct);
            db.UserRoles.Add(new UserRole { UserId = earlier.FirstReviewerId, RoleId = roleId,
                BranchId = earlier.BranchId, DepartmentId = earlier.DepartmentId });
            await db.SaveChangesAsync(ct);
        }
        try
        {
            var before = await SnapshotAsync(current, id, ct);
            var error = await Assert.ThrowsAsync<SqlException>(() => RunRangeCleanupAsync(current, id, false, ct));
            Assert.Equal(51002, error.Number);
            Assert.Equal(before, await SnapshotAsync(current, id, ct));
            await using var verify = new ORPDbContext(current.Options);
            Assert.True(await verify.UserRoles.AnyAsync(r => r.UserId == earlier.FirstReviewerId && r.RoleId == roleId, ct));
            Assert.True(await verify.WorkflowDefinitions.AnyAsync(w => w.Id == current.WorkflowId, ct));
        }
        finally
        {
            // This link was added by this test, not by either fixture.
            await using var db = new ORPDbContext(current.Options);
            await db.UserRoles.Where(r => r.UserId == earlier.FirstReviewerId && r.RoleId == roleId
                && r.BranchId == earlier.BranchId && r.DepartmentId == earlier.DepartmentId).ExecuteDeleteAsync(ct);
        }
    }

    private static async Task RunRangeCleanupAsync(ImportApprovalEnvironment env, long id, bool dryRun, CancellationToken ct)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "delete-messages-by-id-range.sql"), ct);
        script = script.Replace("DECLARE @FromId bigint = NULL;", "DECLARE @FromId bigint = @Id;")
            .Replace("DECLARE @ToId bigint = NULL;", "DECLARE @ToId bigint = @Id;")
            .Replace("DECLARE @DryRun bit = 1;", $"DECLARE @DryRun bit = {(dryRun ? 1 : 0)};");
        await using var db = new ORPDbContext(env.Options);
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = script;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@Id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task AssertCleanedAsync(ImportApprovalEnvironment env, CancellationToken ct)
    {
        await using var db = new ORPDbContext(env.Options);
        Assert.False(await db.Messages.AnyAsync(m => m.BranchId == env.BranchId, ct));
        Assert.False(await db.SwiftMessages.AnyAsync(m => m.FilePath == env.FileName, ct));
        Assert.False(await db.IgnoredImportFiles.AnyAsync(m => m.FilePath == env.FileName, ct));
        Assert.False(await db.Branches.AnyAsync(b => b.Id == env.BranchId, ct));
        Assert.False(await db.Departments.AnyAsync(d => d.Id == env.DepartmentId, ct));
        Assert.False(await db.Roles.AnyAsync(r => r.Name == env.BranchName, ct));
        Assert.False(await db.Users.AnyAsync(u => u.Id == env.FirstReviewerId || u.Id == env.SecondReviewerId, ct));
        Assert.False(await db.WorkflowDefinitions.AnyAsync(w => w.Id == env.WorkflowId, ct));
        Assert.False(await db.WorkflowSteps.AnyAsync(w => w.WorkflowDefinitionId == env.WorkflowId, ct));
        Assert.False(await db.DepartmentRoutingRules.AnyAsync(r => r.BranchId == env.BranchId, ct));
        Assert.False(await db.UserRoles.AnyAsync(r => r.BranchId == env.BranchId, ct));
        // Remaining child rows would also prevent deletion via foreign keys.
        Assert.True(await db.Permissions.AnyAsync(p => p.Name == Permissions.MessageView, ct));
    }

    private static async Task<long> ImportAsync(ImportApprovalEnvironment env, CancellationToken ct)
    {
        Assert.Equal(SaaFileImportResult.Imported, await env.ImportAsync(ct));
        await using var db = new ORPDbContext(env.Options);
        var message = Assert.Single(await db.Messages.Where(m => m.BranchId == env.BranchId).Include(m => m.Departments).ToListAsync(ct));
        var source = Assert.Single(await db.SwiftMessages.Where(m => m.FilePath == env.FileName).ToListAsync(ct));
        Assert.Equal(source.MessageId, message.Id);
        Assert.Equal(env.WorkflowId, message.WorkflowDefinitionId);
        Assert.Equal(env.BranchId, message.BranchId);
        Assert.Equal(env.DepartmentId, Assert.Single(message.Departments).DepartmentId);
        Assert.Equal(env.Reference, source.ReferenceId);
        Assert.Equal(env.MessageType, source.MessageType);
        Assert.Equal(MessageDirection.Incoming, source.Direction);
        Assert.Equal("SENDERXX", source.Sender);
        Assert.Equal("RECEIVERXX", source.Receiver);
        Assert.Equal(ImportApprovalEnvironment.Body, source.Body);
        Assert.Equal(AuditEventType.MessageRegistered, Assert.Single(await db.AuditEvents.Where(a => a.MessageId == message.Id).ToListAsync(ct)).EventType);
        return message.Id;
    }

    private static async Task CompleteAsync(ImportApprovalEnvironment env, long id,
        HttpClient first, HttpClient second, CancellationToken ct)
    {
        await AssignAsync(first, id, env.FirstReviewerId, ct);
        await ConfirmAsync(first, id, 1, ct);
        var waiting = await DetailsAsync(second, id, ct);
        Assert.Equal("WaitingForSecondReview", waiting.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, waiting.GetProperty("currentAssigneeId").ValueKind);
        Assert.Empty(waiting.GetProperty("assignments").EnumerateArray());
        await using (var db = new ORPDbContext(env.Options))
        {
            var review = Assert.Single(await db.Reviews.Where(r => r.MessageId == id).ToListAsync(ct));
            Assert.Equal(1, review.Level);
            Assert.Equal(ReviewStatus.Approved, review.Status);
            Assert.NotNull(Assert.Single(await db.Assignments.Where(a => a.MessageId == id).ToListAsync(ct)).EndedAt);
        }
        await AssignAsync(second, id, env.SecondReviewerId, ct);
        await ConfirmAsync(second, id, 2, ct);
    }

    private static async Task AssignAsync(HttpClient client, long id, int reviewer, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync($"/api/messages/{id}/assign", new { assignedTo = reviewer }, ct);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(reviewer, (await DetailsAsync(client, id, ct)).GetProperty("currentAssigneeId").GetInt32());
    }

    private static async Task ConfirmAsync(HttpClient client, long id, int level, CancellationToken ct)
    {
        var details = await DetailsAsync(client, id, ct);
        Assert.True(details.GetProperty("canConfirm").GetBoolean());
        Assert.Equal(level, details.GetProperty("confirmationLevel").GetInt32());
        using var response = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/confirm",
            new { level, comment = $"Approved level {level}" }, ct);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(ct));
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client, long id, CancellationToken ct) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/messages/{id}", ct);

    private static async Task AssertCompletedAsync(ImportApprovalEnvironment env, long id, HttpClient client, CancellationToken ct)
    {
        var details = await DetailsAsync(client, id, ct);
        Assert.Equal("Completed", details.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("currentAssigneeId").ValueKind);
        Assert.False(details.GetProperty("canConfirm").GetBoolean());
        Assert.Empty(details.GetProperty("assignments").EnumerateArray());
        var card = await client.GetFromJsonAsync<JsonElement>($"/api/messages/{id}/view", ct);
        Assert.Equal(ImportApprovalEnvironment.Body, card.GetProperty("rawText").GetString());
        var approvals = card.GetProperty("approvals").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, approvals.Select(a => a.GetProperty("level").GetInt32()));
        Assert.Equal(new[] { "First reviewer", "Second reviewer" }, approvals.Select(a => a.GetProperty("approver").GetString()));
        Assert.All(approvals, a => Assert.Equal("approved", a.GetProperty("status").GetString()));
        var history = card.GetProperty("auditLog").EnumerateArray().ToArray();
        Assert.Equal(new[] { "create", "assign", "start", "approve", "assign", "start", "approve", "complete" },
            history.Select(a => a.GetProperty("type").GetString()).Where(t => t != "unassign"));
        await using var db = new ORPDbContext(env.Options);
        var message = Assert.Single(await db.Messages.Where(m => m.Id == id).ToListAsync(ct));
        Assert.Equal(MessageState.Completed, message.State);
        Assert.Null(message.CurrentAssigneeId);
        var reviews = await db.Reviews.Where(r => r.MessageId == id).OrderBy(r => r.Level).ToListAsync(ct);
        Assert.Equal(new[] { 1, 2 }, reviews.Select(r => r.Level));
        Assert.Equal(new[] { env.FirstReviewerId, env.SecondReviewerId }, reviews.Select(r => r.ReviewerId));
        Assert.All(reviews, r => { Assert.Equal(ReviewStatus.Approved, r.Status); Assert.NotNull(r.CompletedAt); });
        var assignments = await db.Assignments.Where(a => a.MessageId == id).OrderBy(a => a.Level).ToListAsync(ct);
        Assert.Equal(new[] { env.FirstReviewerId, env.SecondReviewerId }, assignments.Select(a => a.AssignedTo));
        Assert.All(assignments, a => Assert.NotNull(a.EndedAt));
        var audit = await db.AuditEvents.Where(a => a.MessageId == id).OrderBy(a => a.Timestamp).ThenBy(a => a.Id).ToListAsync(ct);
        // Unassignment takes its own timestamp, while confirmation events share
        // the operation's start time. Check its presence separately from this sequence.
        Assert.Equal(new[] { AuditEventType.MessageRegistered, AuditEventType.MessageAssigned,
            AuditEventType.ReviewStarted, AuditEventType.ReviewApproved, AuditEventType.MessageAssigned,
            AuditEventType.ReviewStarted, AuditEventType.ReviewApproved, AuditEventType.MessageCompleted },
            audit.Where(a => a.EventType != AuditEventType.MessageUnassigned).Select(a => a.EventType));
        Assert.Equal(2, audit.Count(a => a.EventType == AuditEventType.MessageUnassigned));
        Assert.Single(audit, a => a.EventType == AuditEventType.MessageRegistered);
        Assert.Single(audit, a => a.EventType == AuditEventType.MessageCompleted);
        Assert.Equal(new[] { env.FirstReviewerId, env.SecondReviewerId },
            audit.Where(a => a.EventType == AuditEventType.ReviewApproved).Select(a => a.UserId!.Value));
        Assert.Equal(2, audit.Count(a => a.EventType == AuditEventType.MessageAssigned));
        Assert.Equal(2, audit.Count(a => a.EventType == AuditEventType.ReviewStarted));
    }

    private static async Task<string> SnapshotAsync(ImportApprovalEnvironment env, long id, CancellationToken ct)
    {
        await using var db = new ORPDbContext(env.Options);
        return JsonSerializer.Serialize(new
        {
            Messages = await db.Messages.Where(m => m.BranchId == env.BranchId).OrderBy(m => m.Id).Select(m => new
                { m.Id, m.State, m.CurrentAssigneeId, m.WorkflowDefinitionId, m.RowVersion }).ToListAsync(ct),
            Sources = await db.SwiftMessages.Where(m => m.FilePath == env.FileName).OrderBy(m => m.MessageId).ToListAsync(ct),
            Assignments = await db.Assignments.Where(a => a.MessageId == id).OrderBy(a => a.Id).ToListAsync(ct),
            Reviews = await db.Reviews.Where(r => r.MessageId == id).OrderBy(r => r.Id).ToListAsync(ct),
            Audit = await db.AuditEvents.Where(a => a.MessageId == id).OrderBy(a => a.Id).Select(a => new
                { a.Id, a.MessageId, a.EventType, a.UserId, a.Timestamp, a.OldState, a.NewState, a.DetailsJson, a.ReviewId, a.CorrelationId }).ToListAsync(ct)
        });
    }
}
