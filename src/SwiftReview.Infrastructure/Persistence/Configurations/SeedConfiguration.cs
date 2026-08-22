using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;

namespace SwiftReview.Infrastructure.Persistence.Configurations;

public sealed class SeedConfiguration :
    IEntityTypeConfiguration<Branch>, IEntityTypeConfiguration<Department>, IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<User>, IEntityTypeConfiguration<UserRole>,
    IEntityTypeConfiguration<RolePermission>, IEntityTypeConfiguration<UserBranch>, IEntityTypeConfiguration<UserDepartment>,
    IEntityTypeConfiguration<Message>
{
    internal static readonly string[] MessageTypes = ["MT199", "MT299", "MT671", "MT700", "MT710", "MT760", "MT799", "MT999"];
    public void Configure(EntityTypeBuilder<Branch> b) => b.HasData(new { Id = 1, Name = "London" }, new { Id = 2, Name = "Dublin" }, new { Id = 3, Name = "Singapore" });
    public void Configure(EntityTypeBuilder<Department> b) => b.HasData(new { Id = 1, Name = "CS" }, new { Id = 2, Name = "TFO" }, new { Id = 3, Name = "DC" });
    public void Configure(EntityTypeBuilder<Permission> b) => b.HasData(Permissions.All.Select((name, i) => new { Id = i + 1, Name = name }));
    public void Configure(EntityTypeBuilder<Role> b) => b.HasData(
        new { Id = 1, Name = "CS Reviewer" }, new { Id = 2, Name = "TFO Reviewer" }, new { Id = 3, Name = "DC Reviewer" },
        new { Id = 4, Name = "DC Senior Reviewer" }, new { Id = 5, Name = "Supervisor" }, new { Id = 6, Name = "Administrator" });
    public void Configure(EntityTypeBuilder<User> b) => b.HasData(
        new { Id = 1, UserName = "cs-reviewer", DisplayName = "CS Reviewer" },
        new { Id = 2, UserName = "tfo-reviewer", DisplayName = "TFO Reviewer" },
        new { Id = 3, UserName = "dc-reviewer", DisplayName = "DC Reviewer" },
        new { Id = 4, UserName = "dc-senior", DisplayName = "DC Senior Reviewer" },
        new { Id = 5, UserName = "supervisor", DisplayName = "Supervisor" },
        new { Id = 6, UserName = "admin", DisplayName = "Administrator" });
    public void Configure(EntityTypeBuilder<UserRole> b) => b.HasData(Enumerable.Range(1, 6).Select(i => new { UserId = i, RoleId = i }));
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        var id = Permissions.All.Select((name, i) => (name, id: i + 1)).ToDictionary(x => x.name, x => x.id);
        var rows = new List<object>();
        Add(1, Permissions.MessageView, Permissions.ReviewLevel1);
        Add(2, Permissions.MessageView, Permissions.ReviewLevel1, Permissions.ReviewLevel2);
        Add(3, Permissions.MessageView, Permissions.ReviewLevel1);
        Add(4, Permissions.MessageView, Permissions.ReviewLevel2, Permissions.ReviewLevel3, Permissions.ReviewReject, Permissions.ReviewUndo);
        Add(5, Permissions.MessageView, Permissions.MessageAssign, Permissions.ReviewLevel1, Permissions.ReviewLevel2, Permissions.ReviewLevel3, Permissions.ReviewReject, Permissions.ReviewUndo, Permissions.AuditView);
        Add(6, Permissions.All);
        b.HasData(rows);
        void Add(int roleId, params string[] names) { foreach (var name in names) rows.Add(new { RoleId = roleId, PermissionId = id[name] }); }
    }
    public void Configure(EntityTypeBuilder<UserBranch> b)
    {
        var rows = new List<object>();
        foreach (var user in Enumerable.Range(1, 6))
            foreach (var branch in user >= 4 ? Enumerable.Range(1, 3) : [((user - 1) % 3) + 1]) rows.Add(new { UserId = user, BranchId = branch });
        b.HasData(rows);
    }
    public void Configure(EntityTypeBuilder<UserDepartment> b)
    {
        var rows = new List<object>();
        for (var user = 1; user <= 6; user++)
            foreach (var department in user >= 4 ? Enumerable.Range(1, 3) : [user <= 1 ? 1 : user == 2 ? 2 : 3]) rows.Add(new { UserId = user, DepartmentId = department });
        b.HasData(rows);
    }
    public void Configure(EntityTypeBuilder<Message> b)
    {
        var rows = Enumerable.Range(1, 75).Select(i =>
        {
            var typeIndex = (i - 1) % MessageTypes.Length;
            var levels = RequiredLevelCount(typeIndex + 1);
            var state = ValidStates(levels)[((i - 1) / MessageTypes.Length) % ValidStates(levels).Length];
            return new
            {
                Id = (long)i,
                ExternalId = $"SEED-{i:0000}",
                MessageType = MessageTypes[typeIndex],
                BranchId = ((i - 1) % 3) + 1,
                OwningDepartmentId = (typeIndex % 3) + 1,
                State = state,
                ReceivedAt = SeedReceivedAt(i),
                CurrentAssigneeId = state == MessageState.New ? (int?)null : AssigneeFor(state, levels),
                WorkflowDefinitionId = typeIndex + 1,
                Sender = $"BANK{i % 9:00}",
                Receiver = "SWIFTREVIEW",
                Account = $"ACCT-{i:00000}",
                Currency = new[] { "EUR", "USD", "GBP" }[(i - 1) % 3],
                Amount = (decimal?)(1000 + i * 17.25m),
                Reference = $"REF-{i:0000}"
            };
        });
        b.HasData(rows);
    }

    internal static int RequiredLevelCount(int workflowId) => workflowId % 3 switch { 1 => 1, 2 => 2, _ => 3 };
    internal static DateTimeOffset SeedReceivedAt(int i) => new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero).AddHours(i);
    internal static MessageState[] ValidStates(int levels) => levels switch
    {
        1 => [MessageState.New, MessageState.Assigned, MessageState.FirstReviewInProgress, MessageState.Completed],
        2 => [MessageState.New, MessageState.Assigned, MessageState.FirstReviewInProgress, MessageState.WaitingForSecondReview, MessageState.SecondReviewInProgress, MessageState.Completed],
        _ => [MessageState.New, MessageState.Assigned, MessageState.FirstReviewInProgress, MessageState.WaitingForSecondReview,
            MessageState.SecondReviewInProgress, MessageState.WaitingForThirdReview, MessageState.ThirdReviewInProgress, MessageState.Completed]
    };
    internal static int AssigneeFor(MessageState state, int levels) => state switch
    {
        MessageState.SecondReviewInProgress or MessageState.WaitingForSecondReview => 6,
        MessageState.ThirdReviewInProgress or MessageState.WaitingForThirdReview => 4,
        MessageState.Completed => levels switch { 1 => 5, 2 => 6, _ => 4 },
        _ => 5
    };
}

public sealed class WorkflowSeedConfiguration : IEntityTypeConfiguration<Domain.Workflows.WorkflowDefinition>, IEntityTypeConfiguration<Domain.Workflows.WorkflowStep>
{
    public void Configure(EntityTypeBuilder<Domain.Workflows.WorkflowDefinition> b) => b.HasData(
        new { Id = 1, Name = "Single Review", MessageType = "MT199", DepartmentId = 1, BranchId = (int?)null, IsActive = true },
        new { Id = 2, Name = "Two Reviews", MessageType = "MT299", DepartmentId = 2, BranchId = (int?)null, IsActive = true },
        new { Id = 3, Name = "Three Reviews", MessageType = "MT671", DepartmentId = 3, BranchId = (int?)null, IsActive = true },
        new { Id = 4, Name = "MT700 Single Review", MessageType = "MT700", DepartmentId = 1, BranchId = (int?)null, IsActive = true },
        new { Id = 5, Name = "MT710 Two Reviews", MessageType = "MT710", DepartmentId = 2, BranchId = (int?)null, IsActive = true },
        new { Id = 6, Name = "MT760 Three Reviews", MessageType = "MT760", DepartmentId = 3, BranchId = (int?)null, IsActive = true },
        new { Id = 7, Name = "MT799 Single Review", MessageType = "MT799", DepartmentId = 1, BranchId = (int?)null, IsActive = true },
        new { Id = 8, Name = "MT999 Two Reviews", MessageType = "MT999", DepartmentId = 2, BranchId = (int?)null, IsActive = true });
    public void Configure(EntityTypeBuilder<Domain.Workflows.WorkflowStep> b)
    {
        var rows = new List<object>(); var id = 1;
        for (var workflowId = 1; workflowId <= 8; workflowId++)
            for (var level = 1; level <= SeedConfiguration.RequiredLevelCount(workflowId); level++)
                rows.Add(new { Id = id++, WorkflowDefinitionId = workflowId, Order = level, ReviewLevel = level, Required = true });
        b.HasData(rows);
    }
}

public sealed class OperationalSeedConfiguration : IEntityTypeConfiguration<MessageRawData>, IEntityTypeConfiguration<Assignment>,
    IEntityTypeConfiguration<Review>, IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<MessageRawData> b) => b.HasData(Enumerable.Range(1, 75)
        .Select(i => new
        {
            MessageId = (long)i,
            RawContent = $"{{1:F01SEED{i:0000}}}{{2:I{SeedConfiguration.MessageTypes[(i - 1) % 8][2..]}SWIFTREVIEW}}{{4::20:REF-{i:0000}-}}"
        }));

    public void Configure(EntityTypeBuilder<Assignment> b)
    {
        var rows = new List<object>();
        for (var i = 1; i <= 75; i++)
        {
            var typeIndex = (i - 1) % 8; var levels = SeedConfiguration.RequiredLevelCount(typeIndex + 1);
            var state = SeedConfiguration.ValidStates(levels)[((i - 1) / 8) % SeedConfiguration.ValidStates(levels).Length];
            if (state == MessageState.New) continue;
            var assignedTo = SeedConfiguration.AssigneeFor(state, levels);
            rows.Add(new
            {
                Id = (long)i,
                MessageId = (long)i,
                AssignedBy = assignedTo == 6 ? 5 : 6,
                AssignedTo = assignedTo,
                CreatedAt = SeedConfiguration.SeedReceivedAt(i).AddMinutes(1),
                EndedAt = (DateTimeOffset?)null
            });
        }
        b.HasData(rows);
    }

    public void Configure(EntityTypeBuilder<Review> b)
    {
        var rows = new List<object>();
        for (var i = 1; i <= 75; i++)
        {
            var levels = SeedConfiguration.RequiredLevelCount(((i - 1) % 8) + 1);
            var state = SeedConfiguration.ValidStates(levels)[((i - 1) / 8) % SeedConfiguration.ValidStates(levels).Length];
            var completedLevels = CompletedLevels(state, levels);
            for (var level = 1; level <= completedLevels; level++) rows.Add(ReviewRow(i, level, ReviewStatus.Approved));
            var active = ActiveLevel(state);
            if (active is not null) rows.Add(ReviewRow(i, active.Value, ReviewStatus.InProgress));
        }
        b.HasData(rows);
    }

    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        var rows = new List<object>();
        for (var i = 1; i <= 75; i++)
        {
            var levels = SeedConfiguration.RequiredLevelCount(((i - 1) % 8) + 1);
            var state = SeedConfiguration.ValidStates(levels)[((i - 1) / 8) % SeedConfiguration.ValidStates(levels).Length];
            var at = SeedConfiguration.SeedReceivedAt(i); var seq = 1; var correlation = $"seed-{i:0000}";
            Add("MessageImported", null, null, MessageState.New.ToString(), null, at);
            if (state == MessageState.New) continue;
            Add("MessageAssigned", 6, MessageState.New.ToString(), MessageState.Assigned.ToString(), null, at.AddMinutes(1));
            for (var level = 1; level <= CompletedLevels(state, levels); level++)
            {
                var from = level == 1 ? MessageState.Assigned : level == 2 ? MessageState.WaitingForSecondReview : MessageState.WaitingForThirdReview;
                var inProgress = level == 1 ? MessageState.FirstReviewInProgress : level == 2 ? MessageState.SecondReviewInProgress : MessageState.ThirdReviewInProgress;
                var target = level == levels ? MessageState.Completed : level == 1 ? MessageState.WaitingForSecondReview : MessageState.WaitingForThirdReview;
                Add("ReviewStarted", Reviewer(level), from.ToString(), inProgress.ToString(), level, at.AddMinutes(level * 10));
                var approvedAt = at.AddMinutes(level * 10 + 5);
                if (target == MessageState.Completed)
                {
                    rows.Add(new
                    {
                        Id = 900000L + i * 10L + level,
                        MessageId = (long)i,
                        EventType = "ReviewApproved",
                        UserId = (int?)Reviewer(level),
                        Timestamp = approvedAt.AddMilliseconds(-1),
                        OldState = inProgress.ToString(),
                        NewState = target.ToString(),
                        DetailsJson = $"{{\"level\":{level}}}",
                        CorrelationId = correlation
                    });
                    Add("MessageCompleted", Reviewer(level), inProgress.ToString(), target.ToString(), level, approvedAt);
                }
                else Add("ReviewApproved", Reviewer(level), inProgress.ToString(), target.ToString(), level, approvedAt);
            }
            var active = ActiveLevel(state);
            if (active is not null)
            {
                var from = active == 1 ? MessageState.Assigned : active == 2 ? MessageState.WaitingForSecondReview : MessageState.WaitingForThirdReview;
                Add("ReviewStarted", Reviewer(active.Value), from.ToString(), state.ToString(), active, at.AddMinutes(active.Value * 10));
            }

            void Add(string type, int? userId, string? oldState, string? newState, int? level, DateTimeOffset timestamp) => rows.Add(new
            {
                Id = (long)(i * 100 + seq++),
                MessageId = (long)i,
                EventType = type,
                UserId = userId,
                Timestamp = timestamp,
                OldState = oldState,
                NewState = newState,
                DetailsJson = level is null ? "{}" : $"{{\"level\":{level}}}",
                CorrelationId = correlation
            });
        }
        b.HasData(rows);
    }

    private static object ReviewRow(int messageId, int level, ReviewStatus status) => new
    {
        Id = (long)(messageId * 10 + level),
        MessageId = (long)messageId,
        Level = level,
        ReviewerId = Reviewer(level),
        Status = status,
        StartedAt = SeedConfiguration.SeedReceivedAt(messageId).AddMinutes(level * 10),
        CompletedAt = status == ReviewStatus.Approved ? (DateTimeOffset?)SeedConfiguration.SeedReceivedAt(messageId).AddMinutes(level * 10 + 5) : null,
        Comment = status == ReviewStatus.Approved ? "Seed approval" : null
    };
    private static int Reviewer(int level) => level switch { 1 => 5, 2 => 6, _ => 4 };
    private static int? ActiveLevel(MessageState state) => state switch
    { MessageState.FirstReviewInProgress => 1, MessageState.SecondReviewInProgress => 2, MessageState.ThirdReviewInProgress => 3, _ => null };
    private static int CompletedLevels(MessageState state, int levels) => state switch
    {
        MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress => 1,
        MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress => 2,
        MessageState.Completed => levels,
        _ => 0
    };
}
