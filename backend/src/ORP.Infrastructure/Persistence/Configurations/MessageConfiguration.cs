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
        builder.ToTable("Messages"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("MessageId").ValueGeneratedNever();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
        builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CurrentAssigneeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SwiftMessageRecordConfiguration : IEntityTypeConfiguration<SwiftMessageRecord>
{
    public void Configure(EntityTypeBuilder<SwiftMessageRecord> builder)
    {
        builder.ToView("SwiftMessageSource", "ORP");
        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.MessageId).HasColumnName("MessageID").ValueGeneratedNever();
        builder.Property(x => x.ExternalId).HasMaxLength(100);
        builder.Property(x => x.MessageType).HasMaxLength(20);
        builder.Property(x => x.Sender).HasMaxLength(100);
        builder.Property(x => x.Receiver).HasMaxLength(100);
        builder.Property(x => x.Account).HasMaxLength(100);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(19, 4);
        builder.Property(x => x.Reference).HasMaxLength(200);
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
