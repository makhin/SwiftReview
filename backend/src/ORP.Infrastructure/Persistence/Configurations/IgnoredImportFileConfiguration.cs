using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class IgnoredImportFileConfiguration : IEntityTypeConfiguration<IgnoredImportFile>
{
    public void Configure(EntityTypeBuilder<IgnoredImportFile> builder)
    {
        builder.ToTable("IgnoredImportFiles", table => table.HasCheckConstraint(
            "CK_IgnoredImportFiles_Reason", "[Reason] = N'NoDepartmentRule'"));
        builder.HasKey(x => x.FilePath);
        builder.Property(x => x.FilePath).HasMaxLength(450).UseCollation("Latin1_General_100_CI_AS");
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(40).IsRequired();
    }
}
