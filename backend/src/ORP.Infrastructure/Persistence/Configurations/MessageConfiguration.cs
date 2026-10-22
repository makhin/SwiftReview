using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Identity;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.ToTable("Messages"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("MessageId").ValueGeneratedNever();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.DepartmentIds);
        builder.Ignore(x => x.ProcessingDepartmentIds);
        builder.HasMany(x => x.Departments).WithOne().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Departments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<SwiftMessageRecord>().WithOne().HasForeignKey<Message>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CurrentAssigneeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MessageDepartmentConfiguration : IEntityTypeConfiguration<MessageDepartment>
{
    public void Configure(EntityTypeBuilder<MessageDepartment> builder)
    {
        builder.ToTable("MessageDepartments");
        builder.HasKey(x => new { x.MessageId, x.DepartmentId });
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DepartmentId);
    }
}

public sealed class SwiftMessageRecordConfiguration : IEntityTypeConfiguration<SwiftMessageRecord>
{
    public void Configure(EntityTypeBuilder<SwiftMessageRecord> builder)
    {
        builder.ToTable("SwiftMessages", "orp");
        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.MessageId).ValueGeneratedOnAdd();
        builder.Property(x => x.Branch).HasMaxLength(100);
        builder.Property(x => x.MessageType).HasMaxLength(100);
        builder.Property(x => x.ReferenceId).HasMaxLength(255);
        builder.ToTable(t => t.HasCheckConstraint("CK_SwiftMessages_Direction", "[Direction] IN (1, 2)"));
        builder.Property(x => x.Sender).HasMaxLength(100);
        builder.Property(x => x.Receiver).HasMaxLength(100);
        builder.Property(x => x.Host).HasMaxLength(255);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.FilePath).HasMaxLength(450).UseCollation("Latin1_General_100_CI_AS");
        builder.HasIndex(x => x.FilePath).IsUnique();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews"); builder.HasKey(x => x.Id); builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.HasIndex(x => new { x.MessageId, x.Level }).HasFilter("[Status] <> N'Undone' AND [Status] <> N'Cancelled'").IsUnique();
        builder.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MessageId, x.Level }).HasFilter("[EndedAt] IS NULL").IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_Assignments_Level",
            "([Level] BETWEEN 1 AND 3) AND ([EndedAt] IS NOT NULL OR [Level] IS NOT NULL)"));
        builder.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents"); builder.HasKey(x => x.Id); builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(80);
        builder.Property(x => x.OldState).HasConversion<string>().HasMaxLength(40); builder.Property(x => x.NewState).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)"); builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Review).WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MessageId, x.Timestamp });
    }
}
