using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Emails;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class ForwardingSubjectRuleConfiguration : IEntityTypeConfiguration<ForwardingSubjectRule>
{
    public void Configure(EntityTypeBuilder<ForwardingSubjectRule> b)
    {
        b.ToTable("ForwardingSubjectRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.MessageType).HasMaxLength(100);
        b.Property(x => x.Template).HasMaxLength(500).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
