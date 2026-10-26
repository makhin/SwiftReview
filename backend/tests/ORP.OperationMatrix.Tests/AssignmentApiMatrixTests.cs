using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Application.Messages.Get;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using ORP.Importer.SqlTests;
using Xunit;

namespace ORP.OperationMatrix.Tests;

public sealed class AssignmentApiMatrixTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } };
    private static readonly int[][] LevelSets = [[1], [1, 2], [1, 3], [1, 2, 3]];

    public static IEnumerable<object[]> Policies()
    {
        foreach (var mode in new[] { ConfirmationMode.Immediate, ConfirmationMode.ReviewSession })
        foreach (var retention in Enum.GetValues<AssignmentRetentionMode>())
        foreach (var reuse in new[] { false, true })
            yield return [mode, retention, reuse];
    }

    public static IEnumerable<object[]> AssignmentPolicies() => Policies().SelectMany(p =>
        Enumerable.Range(0, LevelSets.Length).Select(index => p.Append((object)index).ToArray()));

    [Theory]
    [MemberData(nameof(AssignmentPolicies))]
    public async Task AssignmentOrdersSubsetsAndReviewerReuse(ConfirmationMode mode,
        AssignmentRetentionMode retention, bool reuse, int levelSet)
    {
        await using var h = await Harness.Create();
        var levels = LevelSets[levelSet];
        var workflow = await h.Workflow(levels, mode, retention, reuse);
        var cases = new Cases(output);
        foreach (var order in Permutations(levels))
        for (var mask = 0; mask < 1 << levels.Length; mask++)
        foreach (var same in new[] { false, true })
        {
            var selectedMask = mask;
            await cases.Run($"order={string.Join(',', order)}, mask={mask}, same={same}", async () =>
            {
                var id = await h.Message(workflow);
                var approved = new HashSet<int>();
                var completed = new HashSet<int>();
                async Task<int> Assign(int level)
                {
                    var details = await h.Details(id);
                    var existing = details.Assignments.SingleOrDefault(a => a.Level == level)?.AssignedTo;
                    var desired = h.Reviewers[same ? 0 : Array.IndexOf(levels, level)];
                    var forbidden = approved.Concat(details.Assignments.Where(a => a.Level != level).Select(a => a.AssignedTo)).ToHashSet();
                    if (!reuse && forbidden.Contains(desired))
                    {
                        var candidates = await h.Get<AssignmentCandidateDto[]>($"/api/messages/{id}/assignment-candidates?level={level}");
                        Assert.DoesNotContain(candidates, c => c.Id == desired);
                        await h.Reject(id, "assign", new { assignedTo = desired, level }, HttpStatusCode.BadRequest);
                        desired = h.Reviewers.First(u => !forbidden.Contains(u));
                    }
                    if (existing != desired)
                    {
                        await h.Post(id, "assign", new { assignedTo = desired, level });
                        var after = await h.Details(id);
                        Assert.NotEqual(details.Version, after.Version);
                        Assert.Equal(desired, after.Assignments.Single(a => a.Level == level).AssignedTo);
                        if (level != levels.First(l => !completed.Contains(l)))
                            Assert.Equal(details.CurrentAssigneeId, after.CurrentAssigneeId);
                    }
                    return desired;
                }
                foreach (var level in order.Where(l => (selectedMask & (1 << Array.IndexOf(levels, l))) != 0))
                {
                    var before = await h.Details(id);
                    await Assign(level);
                    if (level != 1)
                    {
                        var after = await h.Details(id);
                        Assert.Equal(before.State, after.State);
                        Assert.Equal(before.CurrentAssigneeId, after.CurrentAssigneeId);
                    }
                }
                foreach (var level in levels)
                {
                    var actor = await Assign(level);
                    var details = await h.Details(id, actor);
                    Assert.Equal(actor, details.CurrentAssigneeId);
                    Assert.Equal(mode == ConfirmationMode.Immediate, details.CanConfirm);
                    Assert.Equal(mode == ConfirmationMode.ReviewSession, details.CanReview);
                    foreach (var future in levels.Where(l => l > level))
                    {
                        // A current assignee cannot approve a future level either.
                        await h.Reject(id, mode == ConfirmationMode.Immediate ? "reviews/confirm" : "reviews/start",
                            new { level = future }, mode == ConfirmationMode.Immediate ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden, actor);
                    }
                    var review = await h.Approve(id, level, actor, mode);
                    approved.Add(actor);
                    completed.Add(level);
                    var after = await h.Details(id);
                    var next = levels.FirstOrDefault(l => !completed.Contains(l));
                    if (next != 0)
                    {
                        var expectedState = next == 2 ? MessageState.WaitingForSecondReview : MessageState.WaitingForThirdReview;
                        Assert.Equal(expectedState, after.State);
                        var explicitNext = details.Assignments.SingleOrDefault(a => a.Level == next);
                        Assert.Equal(explicitNext?.AssignedTo ?? (retention == AssignmentRetentionMode.UntilWorkflowCompleted ? actor : (int?)null), after.CurrentAssigneeId);
                        if (retention == AssignmentRetentionMode.UntilWorkflowCompleted)
                            Assert.NotNull(after.CurrentAssigneeId);
                    }
                    else
                    {
                        Assert.Equal(MessageState.Completed, after.State);
                        Assert.Empty(after.Assignments);
                        Assert.Null(after.CurrentAssigneeId);
                    }
                    await h.Reject(id, mode == ConfirmationMode.Immediate ? "reviews/confirm" : "reviews/approve",
                        new { level, reviewId = review }, mode == ConfirmationMode.Immediate && (after.CurrentAssigneeId is null || after.CurrentAssigneeId == actor)
                            ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden, actor);
                }
                await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0], level = 1 }, HttpStatusCode.Conflict);
                await using var db = h.Db();
                var reviews = await db.Reviews.Where(r => r.MessageId == id).OrderBy(r => r.Level).ToListAsync(h.Ct);
                Assert.Equal(levels, reviews.Select(r => r.Level));
                Assert.All(reviews, r => Assert.Equal(ReviewStatus.Approved, r.Status));
                if (!reuse) Assert.Equal(levels.Length, reviews.Select(r => r.ReviewerId).Distinct().Count());
            });
        }
        cases.Finish();
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task UndoPolicyActorAndActiveReviewMatrix(ConfirmationMode mode,
        AssignmentRetentionMode retention, bool reuse)
    {
        await using var h = await Harness.Create();
        var cases = new Cases(output);
        foreach (var approvalMode in Enum.GetValues<UndoApprovalMode>())
        foreach (var actorMode in Enum.GetValues<UndoActorMode>())
        foreach (var activeMode in Enum.GetValues<UndoActiveReviewMode>())
        {
            var workflow = await h.Workflow([1, 2], mode, retention, reuse, undo: approvalMode,
                undoActor: actorMode, undoActive: activeMode);
            foreach (var caller in new[] { "original", "other", "admin" })
            foreach (var checkpoint in mode == ConfirmationMode.ReviewSession ? new[] { "waiting", "active", "final" } : new[] { "waiting", "final" })
                await cases.Run($"undo={approvalMode}/{actorMode}/{activeMode}, caller={caller}, checkpoint={checkpoint}", async () =>
                {
                    var id = await h.Message(workflow);
                    await h.Post(id, "assign", new { assignedTo = h.Reviewers[0], level = 1 });
                    await h.Post(id, "assign", new { assignedTo = h.Reviewers[1], level = 2 });
                    var reviewId = await h.Approve(id, 1, h.Reviewers[0], mode);
                    var final = checkpoint == "final";
                    var active = checkpoint == "active";
                    if (final) reviewId = await h.Approve(id, 2, h.Reviewers[1], mode);
                    if (active) await h.Start(id, 2, h.Reviewers[1]);
                    var original = final ? h.Reviewers[1] : h.Reviewers[0];
                    var actor = caller == "original" ? original : caller == "admin" ? h.Admin : h.Reviewers[2];
                    var allowed = caller == "admin" && !active ||
                        approvalMode != UndoApprovalMode.Disabled && (!final || approvalMode == UndoApprovalMode.Latest) &&
                        (caller != "other" || actorMode == UndoActorMode.AnyAuthorizedUser) &&
                        (!active || activeMode == UndoActiveReviewMode.Cancel);
                    Assert.Equal(allowed ? reviewId : (long?)null, (await h.GridRow(id, actor)).UndoReviewId);
                    if (!allowed)
                        await h.Reject(id, "reviews/undo", new { reviewId }, HttpStatusCode.Conflict, actor);
                    else
                    {
                        await h.Post(id, "reviews/undo", new { reviewId }, actor);
                        var after = await h.Details(id);
                        Assert.Equal(final ? MessageState.WaitingForSecondReview : MessageState.Assigned, after.State);
                        Assert.Equal(!final && retention == AssignmentRetentionMode.UntilWorkflowCompleted ? h.Reviewers[1] : (int?)null,
                            after.CurrentAssigneeId);
                        if (!final)
                            Assert.Equal(h.Reviewers[1], after.Assignments.Single(a => a.Level == 2).AssignedTo);
                        await using var db = h.Db();
                        Assert.Equal(ReviewStatus.Undone, (await db.Reviews.SingleAsync(r => r.Id == reviewId, h.Ct)).Status);
                        Assert.False(await db.Reviews.AnyAsync(r => r.MessageId == id && r.Status == ReviewStatus.InProgress, h.Ct));
                    }
                });
        }
        cases.Finish();
    }

    [Theory]
    [InlineData(AssignmentRetentionMode.PerConfirmation, false)]
    [InlineData(AssignmentRetentionMode.PerConfirmation, true)]
    [InlineData(AssignmentRetentionMode.UntilWorkflowCompleted, false)]
    [InlineData(AssignmentRetentionMode.UntilWorkflowCompleted, true)]
    public async Task SessionCancelRejectAndReassignmentMatrix(AssignmentRetentionMode retention, bool reuse)
    {
        await using var h = await Harness.Create();
        var cases = new Cases(output);
        foreach (var rejectAllowed in new[] { false, true })
        {
            var workflow = await h.Workflow([1, 2, 3], ConfirmationMode.ReviewSession, retention, reuse, rejectAllowed);
            foreach (var stage in new[] { 1, 2, 3 })
            foreach (var caller in new[] { "owner", "other", "admin" })
                await cases.Run($"reject={rejectAllowed}, stage={stage}, caller={caller}", async () =>
                {
                    var id = await h.Message(workflow);
                    foreach (var level in new[] { 3, 2, 1 })
                        await h.Post(id, "assign", new { assignedTo = h.Reviewers[level - 1], level });
                    for (var level = 1; level < stage; level++)
                        await h.Approve(id, level, h.Reviewers[level - 1], ConfirmationMode.ReviewSession);
                    var owner = h.Reviewers[stage - 1];
                    var actor = caller == "owner" ? owner : caller == "admin" ? h.Admin : h.Reviewers[stage % 3];
                    var reviewId = await h.Start(id, stage, owner);
                    Assert.Equal(reviewId, await h.Start(id, stage, owner));
                    await h.Reject(id, "assign", new { assignedTo = h.Admin, level = stage }, HttpStatusCode.Conflict);
                    if (stage < 3)
                    {
                        await h.Post(id, "assign", new { assignedTo = h.Admin, level = stage + 1 });
                        await h.Post(id, "assign", new { assignedTo = h.Reviewers[stage], level = stage + 1 });
                    }
                    if (caller == "other")
                    {
                        await h.Reject(id, "reviews/cancel", new { level = stage, reviewId }, HttpStatusCode.Forbidden, actor);
                        await h.Reject(id, "reviews/reject", new { level = stage, reviewId }, HttpStatusCode.Forbidden, actor);
                    }
                    else
                    {
                        var before = await h.Details(id);
                        await h.Post(id, "reviews/cancel", new { level = stage, reviewId }, actor);
                        Assert.Equal(before.Assignments, (await h.Details(id)).Assignments);
                        reviewId = await h.Start(id, stage, owner);
                        if (!rejectAllowed)
                            await h.Reject(id, "reviews/reject", new { level = stage, reviewId }, HttpStatusCode.Conflict, actor);
                        else
                        {
                            await h.Post(id, "reviews/reject", new { level = stage, reviewId }, actor);
                            var after = await h.Details(id);
                            Assert.Equal(MessageState.Rejected, after.State);
                            Assert.Empty(after.Assignments);
                            Assert.Null(after.CurrentAssigneeId);
                        }
                    }
                });
        }
        cases.Finish();
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task InvalidLevelsAndCombinedActionMatrix(ConfirmationMode mode,
        AssignmentRetentionMode retention, bool reuse)
    {
        await using var h = await Harness.Create();
        var cases = new Cases(output);
        foreach (var levels in LevelSets)
        {
            var workflow = await h.Workflow(levels, mode, retention, reuse);
            await cases.Run($"levels={string.Join(',', levels)}", async () =>
            {
                var id = await h.Message(workflow);
                foreach (var level in new[] { -1, 0, 4 })
                    await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0], level }, HttpStatusCode.BadRequest);
                foreach (var level in new[] { 1, 2, 3 }.Except(levels))
                    await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0], level }, HttpStatusCode.Conflict);
                await h.Reject(id, "assign", new { assignedTo = 0, level = 1 }, HttpStatusCode.BadRequest);
                await h.Reject(id, "assign", new { assignedTo = int.MaxValue, level = 1 }, HttpStatusCode.NotFound);
                foreach (var future in levels.Skip(1))
                    await h.Reject(id, "reviews/confirm", new { level = future }, HttpStatusCode.Conflict, h.Reviewers[0]);
                // Omitting level remains compatible with current-stage assignment.
                await h.Post(id, "assign", new { assignedTo = h.Reviewers[0] });
                Assert.Equal(1, Assert.Single((await h.Details(id)).Assignments).Level);
                await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0] }, HttpStatusCode.BadRequest);
                await h.Reject(id, "reviews/confirm", new { level = 1 }, HttpStatusCode.Forbidden, h.Reviewers[1]);
                if (mode == ConfirmationMode.ReviewSession)
                {
                    await h.Reject(id, "reviews/confirm", new { level = 1 }, HttpStatusCode.Conflict, h.Reviewers[0]);
                    var unassigned = await h.Message(workflow);
                    await h.Reject(unassigned, "reviews/confirm", new { level = 1 }, HttpStatusCode.Conflict, h.Reviewers[0]);
                }
                else
                {
                    foreach (var level in levels)
                    {
                        var actor = h.Reviewers[Array.IndexOf(levels, level)];
                        var current = (await h.Details(id)).CurrentAssigneeId;
                        if (current.HasValue && current != actor)
                            await h.Post(id, "assign", new { assignedTo = actor, level });
                        await h.Post(id, "reviews/confirm", new { level }, actor);
                        await h.Reject(id, "assign", new { assignedTo = h.Admin, level }, HttpStatusCode.Conflict);
                    }
                    Assert.Equal(MessageState.Completed, (await h.Details(id)).State);
                }
            });
        }
        cases.Finish();
    }

    [Fact]
    public async Task PermissionAndScopeMatrix()
    {
        await using var h = await Harness.Create();
        var workflow = await h.Workflow([1, 2, 3], ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, true);
        var cases = new Cases(output);
        var rights = new[] { Permissions.MessageView, Permissions.MessageAssign, Permissions.ReviewLevel1,
            Permissions.ReviewLevel2, Permissions.ReviewLevel3 };
        for (var mask = 0; mask < 32; mask++)
        foreach (var inScope in new[] { false, true })
        {
            var selected = rights.Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
            var user = await h.RestrictedUser(selected, inScope);
            foreach (var level in new[] { 1, 2, 3 })
                await cases.Run($"rights={mask}, scope={inScope}, level={level}", async () =>
                {
                    var id = await h.Message(workflow);
                    var eligible = inScope && selected.Contains(Permissions.MessageView) && selected.Contains(rights[level + 1]);
                    var candidates = await h.Get<AssignmentCandidateDto[]>($"/api/messages/{id}/assignment-candidates?level={level}");
                    Assert.Equal(eligible, candidates.Any(c => c.Id == user));
                    if (eligible) await h.Post(id, "assign", new { assignedTo = user, level });
                    else await h.Reject(id, "assign", new { assignedTo = user, level }, HttpStatusCode.BadRequest);
                    var canAssign = inScope && selected.Contains(Permissions.MessageView) && selected.Contains(Permissions.MessageAssign);
                    if (canAssign) await h.Post(id, "assign", new { assignedTo = h.Reviewers[0], level }, user);
                    else await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0], level }, HttpStatusCode.Forbidden, user);
                });
        }
        cases.Finish();
    }

    [Fact]
    public async Task NonAssignableWorkflowMatrix()
    {
        await using var h = await Harness.Create();
        var cases = new Cases(output);
        foreach (var viewOnly in new[] { false, true })
        {
            var workflow = await h.Workflow(viewOnly ? [] : [1, 2, 3],
                viewOnly ? ConfirmationMode.None : ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, true);
            if (!viewOnly)
            {
                await using var db = h.Db();
                var saved = await db.WorkflowDefinitions.SingleAsync(w => w.Id == workflow.Id, h.Ct);
                saved.Deactivate();
                await db.SaveChangesAsync(h.Ct);
            }
            foreach (var actor in new[] { h.Admin, h.Reviewers[0] })
            foreach (var level in new[] { 1, 2, 3 })
                await cases.Run($"viewOnly={viewOnly}, actor={actor}, level={level}", async () =>
                {
                    var id = await h.Message(workflow);
                    await h.Reject(id, "assign", new { assignedTo = h.Reviewers[0], level }, HttpStatusCode.Conflict, actor);
                });
        }
        cases.Finish();
    }

    private static IEnumerable<int[]> Permutations(int[] values) => values.Length == 0 ? [Array.Empty<int>()] :
        values.SelectMany(v => Permutations(values.Where(x => x != v).ToArray()).Select(rest => new[] { v }.Concat(rest).ToArray()));

    private sealed class Cases(ITestOutputHelper output)
    {
        private int count;
        private readonly List<string> failures = [];
        public async Task Run(string name, Func<Task> run)
        {
            count++;
            try { await run(); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failures.Add($"{name}: {e.Message}");
            }
        }
        public void Finish()
        {
            output.WriteLine($"API MATRIX: scenarios={count}, passed={count - failures.Count}, failed={failures.Count}");
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(30)));
        }
    }

    private sealed class Harness(SqlImportDatabase fixture, Factory factory, HttpClient client,
        int[] reviewers, int admin, int branch, int department) : IAsyncDisposable
    {
        private readonly Dictionary<long, string> references = [];
        public CancellationToken Ct => TestContext.Current.CancellationToken;
        public int[] Reviewers => reviewers;
        public int Admin => admin;
        public ORPDbContext Db() => new(fixture.Options);
        public static async Task<Harness> Create()
        {
            var ct = TestContext.Current.CancellationToken;
            var fixture = await SqlImportDatabase.CreateAsync();
            try
            {
                await using var db = new ORPDbContext(fixture.Options);
                var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-test-data.sql"), ct);
                await db.Database.OpenConnectionAsync(ct);
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = script;
                    command.CommandTimeout = 120;
                    await command.ExecuteNonQueryAsync(ct);
                }
                var users = await db.Users.ToDictionaryAsync(u => u.UserName, u => u.Id, ct);
                var branch = await db.Branches.Where(b => b.Name == "London").Select(b => b.Id).SingleAsync(ct);
                var department = await db.Departments.Where(d => d.Name == "CS").Select(d => d.Id).SingleAsync(ct);
                var factory = new Factory(fixture.ConnectionString);
                return new(fixture, factory, factory.CreateClient(),
                    [users["theo.mercer"], users["priya.nair"], users["lucas.bennett"]], users["admin"], branch, department);
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async Task<int> RestrictedUser(string[] permissions, bool inScope)
        {
            await using var db = Db();
            var key = Guid.NewGuid().ToString("N");
            var user = new User(key, "Matrix user");
            var role = new Role(key);
            var ids = await db.Permissions.Where(p => permissions.Contains(p.Name)).Select(p => p.Id).ToListAsync(Ct);
            foreach (var id in ids) role.Permissions.Add(new RolePermission { PermissionId = id });
            db.AddRange(user, role);
            await db.SaveChangesAsync(Ct);
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, DepartmentId = department,
                BranchId = inScope ? branch : await db.Branches.Where(b => b.Name == "Dublin").Select(b => b.Id).SingleAsync(Ct) });
            await db.SaveChangesAsync(Ct);
            return user.Id;
        }
        public async Task<WorkflowDefinition> Workflow(int[] levels, ConfirmationMode mode,
            AssignmentRetentionMode retention, bool reuse, bool reject = true, UndoApprovalMode undo = UndoApprovalMode.Latest,
            UndoActorMode undoActor = UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode undoActive = UndoActiveReviewMode.Cancel)
        {
            var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "HTTP matrix");
            foreach (var level in levels) workflow.AddStep(level, level);
            workflow.ConfigureConfirmation(mode, reuse, retention, reject);
            workflow.ConfigureUndo(undo, undoActor, undoActive);
            await using var db = Db(); db.WorkflowDefinitions.Add(workflow); await db.SaveChangesAsync(Ct);
            return workflow;
        }
        public async Task<long> Message(WorkflowDefinition workflow)
        {
            var key = Guid.NewGuid().ToString("N");
            var source = new SwiftMessageRecord { Branch = "London", MessageType = "MT199", Direction = MessageDirection.Incoming,
                ReferenceId = key, Body = "matrix", Sender = "A", Receiver = "B", FileName = key, FilePath = key, ContentHash = new string('0', 64) };
            await using var db = Db(); db.SwiftMessages.Add(source); await db.SaveChangesAsync(Ct);
            var message = new Message(source.MessageId, workflow.Id, branch, department, DateTimeOffset.UtcNow);
            if (workflow.ConfirmationMode == ConfirmationMode.None) message.ApplyRouting(workflow, []);
            db.Messages.Add(message); await db.SaveChangesAsync(Ct);
            references[message.Id] = key;
            return message.Id;
        }
        private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, int? actor)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Debug-User", (actor ?? Admin).ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, Ct);
        }
        public async Task<T> Get<T>(string path, int? actor = null)
        {
            using var response = await Send(HttpMethod.Get, path, null, actor);
            Assert.True(response.IsSuccessStatusCode, $"GET {path}: {response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
            return (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;
        }
        public Task<MessageDetailsDto> Details(long id, int? actor = null) => Get<MessageDetailsDto>($"/api/messages/{id}", actor);
        public async Task<MessageGridRowDto> GridRow(long id, int actor)
        {
            using var response = await Send(HttpMethod.Post, "/api/messages/grid", new { search = references[id] }, actor);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.Single((await response.Content.ReadFromJsonAsync<PagedResult<MessageGridRowDto>>(Json, Ct))!.Items);
        }
        public async Task Post(long id, string action, object body, int? actor = null)
        {
            using var response = await Send(HttpMethod.Post, $"/api/messages/{id}/{action}", body, actor);
            Assert.True(response.StatusCode == HttpStatusCode.NoContent,
                $"POST {id}/{action}: expected 204, got {response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
        public async Task Reject(long id, string action, object body, HttpStatusCode expected, int? actor = null)
        {
            var before = await Snapshot(id);
            using var response = await Send(HttpMethod.Post, $"/api/messages/{id}/{action}", body, actor);
            Assert.True(response.StatusCode == expected,
                $"POST {id}/{action}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
            Assert.Equal(before, await Snapshot(id));
        }
        private async Task<string> Snapshot(long id)
        {
            var details = await Details(id);
            await using var db = Db();
            var audit = await db.AuditEvents.CountAsync(e => e.MessageId == id, Ct);
            var assignments = await db.Assignments.CountAsync(a => a.MessageId == id, Ct);
            var reviews = await db.Reviews.Where(r => r.MessageId == id).OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.Level, r.ReviewerId, r.Status }).ToListAsync(Ct);
            return JsonSerializer.Serialize(new { details, audit, assignments, reviews }, Json);
        }
        public async Task<long> Start(long id, int level, int actor)
        {
            using var response = await Send(HttpMethod.Post, $"/api/messages/{id}/reviews/start", new { level }, actor);
            Assert.True(response.IsSuccessStatusCode, $"start: {response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
            return (await response.Content.ReadFromJsonAsync<StartReviewResponse>(Json, Ct))!.ReviewId;
        }
        public async Task<long> Approve(long id, int level, int actor, ConfirmationMode mode)
        {
            if (mode == ConfirmationMode.Immediate) await Post(id, "reviews/confirm", new { level }, actor);
            else
            {
                var reviewId = await Start(id, level, actor);
                await Post(id, "reviews/approve", new { level, reviewId }, actor);
            }
            await using var db = Db();
            return await db.Reviews.Where(r => r.MessageId == id && r.Level == level && r.Status == ReviewStatus.Approved)
                .Select(r => r.Id).SingleAsync(Ct);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await fixture.DisposeAsync(); }
    }
    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
