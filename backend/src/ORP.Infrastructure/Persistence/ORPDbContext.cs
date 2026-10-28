using Microsoft.EntityFrameworkCore;
using ORP.Domain.Assignments;
using ORP.Domain.DistributionLists;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public sealed class ORPDbContext(DbContextOptions<ORPDbContext> options) : DbContext(options)
{
    public DbSet<ORP.Domain.Emails.OutgoingEmail> OutgoingEmails => Set<ORP.Domain.Emails.OutgoingEmail>();
    public DbSet<ORP.Domain.Emails.ForwardingSubjectRule> ForwardingSubjectRules => Set<ORP.Domain.Emails.ForwardingSubjectRule>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageDepartment> MessageDepartments => Set<MessageDepartment>();
    public DbSet<SwiftMessageRecord> SwiftMessages => Set<SwiftMessageRecord>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DistributionList> DistributionLists => Set<DistributionList>();
    public DbSet<DistributionListEmail> DistributionListEmails => Set<DistributionListEmail>();
    public DbSet<DepartmentRoutingRule> DepartmentRoutingRules => Set<DepartmentRoutingRule>();
    public DbSet<IgnoredImportFile> IgnoredImportFiles => Set<IgnoredImportFile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AccessAuditEvent> AccessAuditEvents => Set<AccessAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orp");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ORPDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureWriteRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureWriteRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureWriteRules()
    {
        var changedDistributionLists = ChangeTracker.Entries<DistributionListEmail>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity.DistributionListId).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<DistributionList>().Where(x =>
                     x.State is EntityState.Unchanged or EntityState.Modified && changedDistributionLists.Contains(x.Entity.Id)))
            entry.Property(x => x.Name).IsModified = true;

        var changedAssignments = ChangeTracker.Entries<Assignment>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity.MessageId).ToHashSet();
        var changedProcessingScopes = ChangeTracker.Entries<MessageDepartment>()
            .Where(x => x.State == EntityState.Modified && x.Property(d => d.CanProcess).IsModified)
            .Select(x => x.Entity.MessageId).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<Message>().Where(x =>
                     x.State is EntityState.Unchanged or EntityState.Modified && (changedProcessingScopes.Contains(x.Entity.Id) || changedAssignments.Contains(x.Entity.Id))))
        {
            // SQL rowversion belongs to Messages. Updating child scopes or assignments must also
            // update its parent, even when the workflow ID itself stays the same.
            entry.Property(x => x.WorkflowDefinitionId).IsModified = true;
        }

        var changedWorkflowIds = ChangeTracker.Entries<WorkflowStep>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity.WorkflowDefinitionId)
            .ToHashSet();
        foreach (var entry in ChangeTracker.Entries<WorkflowDefinition>().Where(x =>
                     x.State != EntityState.Deleted && x.Entity.IsActive &&
                     (x.State is EntityState.Added or EntityState.Modified || changedWorkflowIds.Contains(x.Entity.Id))))
            _ = entry.Entity.RequiredLevels();
        if (ChangeTracker.Entries<AuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
        if (ChangeTracker.Entries<AccessAuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Access audit events are append-only.");
    }
}
