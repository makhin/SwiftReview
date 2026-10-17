using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Workflows;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.WorkflowDefinitionId);
        builder.Property(x => x.ConfirmationMode).HasDefaultValue(ConfirmationMode.ReviewSession);
        builder.Property(x => x.ConfirmationActorMode).HasDefaultValue(ConfirmationActorMode.Assignee);
        builder.Property(x => x.AllowReviewerReuse).HasDefaultValue(false);
        builder.Property(x => x.AssignmentRetentionMode).HasDefaultValue(AssignmentRetentionMode.PerConfirmation);
        builder.Property(x => x.RejectAllowed).HasDefaultValue(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkflowDefinitions_ConfirmationPolicy",
            "[ConfirmationMode] IN (0, 1, 2) AND [ConfirmationActorMode] IN (0, 1) AND [AssignmentRetentionMode] IN (0, 1)"));
        builder.Property(x => x.UndoApprovalMode).HasDefaultValue(UndoApprovalMode.Disabled);
        builder.Property(x => x.UndoActorMode).HasDefaultValue(UndoActorMode.OriginalReviewer);
        builder.Property(x => x.UndoActiveReviewMode).HasDefaultValue(UndoActiveReviewMode.Block);
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkflowDefinitions_UndoPolicy",
            "[UndoApprovalMode] IN (0, 1, 2) AND [UndoActorMode] IN (0, 1) AND [UndoActiveReviewMode] IN (0, 1)"));
        builder.Navigation(x => x.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    { builder.ToTable("WorkflowSteps"); builder.HasKey(x => x.Id); builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Order }).IsUnique(); }
}
