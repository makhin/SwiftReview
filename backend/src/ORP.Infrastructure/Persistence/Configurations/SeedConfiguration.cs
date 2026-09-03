using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class SeedConfiguration :
    IEntityTypeConfiguration<Branch>, IEntityTypeConfiguration<Department>, IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<User>, IEntityTypeConfiguration<UserRole>,
    IEntityTypeConfiguration<RolePermission>, IEntityTypeConfiguration<UserBranch>, IEntityTypeConfiguration<UserDepartment>
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

    internal static int RequiredLevelCount(int workflowId) => (workflowId % 3) switch { 1 => 1, 2 => 2, _ => 3 };
    internal static DateTimeOffset SeedReceivedAt(int i) => new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero).AddHours(i);
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
