using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftReview.Domain.Workflows;
using SwiftReview.Domain.Identity;

namespace SwiftReview.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.MessageType).HasMaxLength(20); builder.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.WorkflowDefinitionId);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.MessageType, x.DepartmentId, x.BranchId })
            .IsUnique().HasFilter("[IsActive] = 1");
    }
}

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    { builder.ToTable("WorkflowSteps"); builder.HasKey(x => x.Id); builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Order }).IsUnique(); }
}
