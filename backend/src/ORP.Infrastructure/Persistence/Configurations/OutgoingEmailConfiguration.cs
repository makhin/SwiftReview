using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Emails;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class OutgoingEmailConfiguration : IEntityTypeConfiguration<OutgoingEmail>
{
    public void Configure(EntityTypeBuilder<OutgoingEmail> b)
    {
        b.ToTable("OutgoingEmails");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromAddress).HasMaxLength(254);
        b.Property(x => x.FromDisplayName).HasMaxLength(200);
        b.Property(x => x.Subject).HasMaxLength(500);
        b.Property(x => x.Body).HasColumnType("nvarchar(max)");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BodyFormat).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Importance).HasConversion<string>().HasMaxLength(10);
        b.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Recipients).WithOne().HasForeignKey(x => x.OutgoingEmailId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.OutgoingEmailId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class EmailRecipientConfiguration : IEntityTypeConfiguration<EmailRecipient>
{
    public void Configure(EntityTypeBuilder<EmailRecipient> b)
    {
        b.ToTable("EmailRecipients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Address).HasMaxLength(254);
        b.Property(x => x.DisplayName).HasMaxLength(200);
    }
}
public sealed class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    public void Configure(EntityTypeBuilder<EmailAttachment> b)
    {
        b.ToTable("EmailAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(255);
        b.Property(x => x.Content).HasColumnType("varbinary(max)");
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
    }
}
