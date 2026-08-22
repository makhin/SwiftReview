using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Workflows;

namespace SwiftReview.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(100).IsRequired(); builder.HasIndex(x => x.ExternalId).IsUnique();
        builder.Property(x => x.MessageType).HasMaxLength(20).IsRequired(); builder.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Sender).HasMaxLength(100); builder.Property(x => x.Receiver).HasMaxLength(100);
        builder.Property(x => x.Account).HasMaxLength(100); builder.Property(x => x.Currency).HasMaxLength(3); builder.Property(x => x.Amount).HasPrecision(19, 4);
        builder.Property(x => x.Reference).HasMaxLength(200); builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.OwningDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CurrentAssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BranchId, x.OwningDepartmentId, x.State, x.ReceivedAt });
    }
}

public sealed class MessageRawDataConfiguration : IEntityTypeConfiguration<MessageRawData>
{
    public void Configure(EntityTypeBuilder<MessageRawData> builder)
    {
        builder.ToTable("MessageRawData"); builder.HasKey(x => x.MessageId); builder.Property(x => x.RawContent).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.Message).WithOne().HasForeignKey<MessageRawData>(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews"); builder.HasKey(x => x.Id); builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.HasIndex(x => new { x.MessageId, x.Level }).HasFilter("[Status] <> N'Undone'").IsUnique();
        builder.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.MessageId).HasFilter("[EndedAt] IS NULL").IsUnique();
        builder.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents"); builder.HasKey(x => x.Id); builder.Property(x => x.EventType).HasMaxLength(80);
        builder.Property(x => x.OldState).HasMaxLength(40); builder.Property(x => x.NewState).HasMaxLength(40);
        builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)"); builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MessageId, x.Timestamp });
    }
}
