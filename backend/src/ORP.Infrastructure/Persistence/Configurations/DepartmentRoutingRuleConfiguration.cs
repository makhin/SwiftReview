using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class DepartmentRoutingRuleConfiguration : IEntityTypeConfiguration<DepartmentRoutingRule>
{
    public void Configure(EntityTypeBuilder<DepartmentRoutingRule> builder)
    {
        builder.ToTable("DepartmentRoutingRules", table =>
        {
            table.HasCheckConstraint("CK_DepartmentRoutingRules_Direction", "[Direction] IN (1, 2)");
            table.HasCheckConstraint("CK_DepartmentRoutingRules_MatchKind", "[MatchKind] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_DepartmentRoutingRules_SourceRow", "[SourceRow] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.MessageTypePattern).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceFile).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SourceSheet).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.Direction, x.BranchId });
    }
}
