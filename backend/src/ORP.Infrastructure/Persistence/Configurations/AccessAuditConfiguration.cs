using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class AccessAuditConfiguration : IEntityTypeConfiguration<AccessAuditEvent>
{
    public void Configure(EntityTypeBuilder<AccessAuditEvent> builder)
    {
        builder.ToTable("AccessAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TargetUserId, x.Timestamp });
        builder.HasIndex(x => new { x.RoleId, x.Timestamp });
    }
}
