using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.DistributionLists;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class DistributionListConfiguration : IEntityTypeConfiguration<DistributionList>
{
    public void Configure(EntityTypeBuilder<DistributionList> builder)
    {
        builder.ToTable("DistributionLists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).UseCollation("Latin1_General_100_CI_AS_SC");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.DepartmentId, x.Name }).IsUnique();
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Emails).WithOne().HasForeignKey(x => x.DistributionListId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DistributionListEmailConfiguration : IEntityTypeConfiguration<DistributionListEmail>
{
    public void Configure(EntityTypeBuilder<DistributionListEmail> builder)
    {
        builder.ToTable("DistributionListEmails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(254).UseCollation("Latin1_General_100_CI_AS_SC");
        builder.HasIndex(x => new { x.DistributionListId, x.Email }).IsUnique();
    }
}
