using Microsoft.EntityFrameworkCore;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public sealed class ORPDbContext(DbContextOptions<ORPDbContext> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SwiftMessageRecord> SwiftMessageSource => Set<SwiftMessageRecord>();
    public DbSet<SwiftMessageBodyRecord> SwiftMessageBodies => Set<SwiftMessageBodyRecord>();
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
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ORP");
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
        var changedWorkflowIds = ChangeTracker.Entries<WorkflowStep>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity.WorkflowDefinitionId)
            .ToHashSet();
        foreach (var entry in ChangeTracker.Entries<WorkflowDefinition>().Where(x =>
                     x.State != EntityState.Deleted && x.Entity.IsActive &&
                     (x.State == EntityState.Added || changedWorkflowIds.Contains(x.Entity.Id))))
            _ = entry.Entity.RequiredLevels();
        if (Database.IsRelational() && ChangeTracker.Entries<SwiftMessageRecord>().Any(x => x.State != EntityState.Unchanged))
            throw new InvalidOperationException("The SWIFT message source is read-only.");
        if (Database.IsRelational() && ChangeTracker.Entries<SwiftMessageBodyRecord>().Any(x => x.State != EntityState.Unchanged))
            throw new InvalidOperationException("The SWIFT message body source is read-only.");
        if (ChangeTracker.Entries<AuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
    }
}
