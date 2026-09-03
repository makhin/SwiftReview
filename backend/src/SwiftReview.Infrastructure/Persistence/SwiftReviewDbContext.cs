using Microsoft.EntityFrameworkCore;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class SwiftReviewDbContext(DbContextOptions<SwiftReviewDbContext> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SwiftMessageRecord> SwiftMessageSource => Set<SwiftMessageRecord>();
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwiftReviewDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Database.IsRelational() && ChangeTracker.Entries<SwiftMessageRecord>().Any(x => x.State != EntityState.Unchanged))
            throw new InvalidOperationException("The SWIFT message source is read-only.");
        if (ChangeTracker.Entries<AuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
        return base.SaveChangesAsync(cancellationToken);
    }
}
