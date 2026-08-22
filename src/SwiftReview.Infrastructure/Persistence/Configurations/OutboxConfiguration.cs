using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftReview.Domain.Outbox;

namespace SwiftReview.Infrastructure.Persistence.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages"); builder.HasKey(x => x.Id); builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)"); builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => x.LockId).IsUnique().HasFilter("[LockId] IS NOT NULL");
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.LockedUntil, x.OccurredAt });
    }
}
