using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class ConfirmationPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void DefaultsPreserveSessionOwnershipAndDistinctReviewers()
    {
        var workflow = Workflow();
        Assert.Equal(ConfirmationMode.ReviewSession, workflow.ConfirmationMode);
        Assert.Equal(ConfirmationActorMode.Assignee, workflow.ConfirmationActorMode);
        Assert.Equal(AssignmentRetentionMode.PerConfirmation, workflow.AssignmentRetentionMode);
        Assert.True(workflow.RejectAllowed);
        Assert.False(workflow.AllowReviewerReuse);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConfiguredLevelsCanBeConfirmedByTheSameUnassignedActor(int count)
    {
        var workflow = Workflow(count);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, ConfirmationActorMode.AnyAuthorizedUser,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        message.Assign(11);
        var reviews = new List<Review>();
        for (var level = 1; level <= count; level++)
        {
            var review = message.StartReview(level, 22, workflow, reviews, Now);
            reviews.Add(review);
            message.Approve(review, workflow, reviews, 22, null, Now);
            Assert.Equal(11, message.CurrentAssigneeId);
        }
        Assert.Equal(MessageState.Completed, message.State);
        Assert.All(reviews, x => Assert.Equal(22, x.ReviewerId));
    }

    [Theory]
    [InlineData(ConfirmationActorMode.Assignee, true, 22)]
    [InlineData(ConfirmationActorMode.AnyAuthorizedUser, false, 11)]
    public void ConfiguredOwnershipAndReviewerReuseAreEnforced(ConfirmationActorMode actorMode, bool reuse, int actor)
    {
        var workflow = Workflow(2);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, actorMode, reuse,
            AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        message.Assign(11);
        var reviews = new List<Review>();
        var first = message.StartReview(1, 11, workflow, reviews, Now);
        reviews.Add(first);
        message.Approve(first, workflow, reviews, 11, null, Now);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(2, actor, workflow, reviews, Now));
        Assert.Equal(MessageState.WaitingForSecondReview, message.State);
    }

    [Fact]
    public void ZeroConfirmationWorkflowIsExplicitAndDoesNotCompleteMessages()
    {
        var workflow = Workflow(0);
        Assert.Throws<DomainRuleViolationException>(() => workflow.RequiredLevels());
        workflow.ConfigureConfirmation(ConfirmationMode.None, ConfirmationActorMode.AnyAuthorizedUser,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        Assert.Empty(workflow.RequiredLevels());
        var message = new Message(1, workflow.Id);
        Assert.Equal(MessageState.New, message.State);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(1, 11, workflow, [], Now));
        workflow.AddStep(1, 1);
        Assert.Throws<DomainRuleViolationException>(() => workflow.RequiredLevels());
    }

    [Fact]
    public void ImmediateConfirmationRequiresAssignmentEvenForAdministrator()
    {
        var workflow = Workflow();
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, ConfirmationActorMode.AnyAuthorizedUser,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(1, 11, workflow, [], Now, isGlobalAdministrator: true));
    }

    [Fact]
    public void UndoCancelsActiveNextReviewAndPreservesAssignmentAndHistory()
    {
        var workflow = Workflow(2);
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, ConfirmationActorMode.AnyAuthorizedUser,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        var message = new Message(1, workflow.Id);
        message.Assign(11);
        var reviews = new List<Review>();
        var first = message.StartReview(1, 22, workflow, reviews, Now);
        reviews.Add(first);
        message.Approve(first, workflow, reviews, 22, null, Now);
        var second = message.StartReview(2, 22, workflow, reviews, Now);
        reviews.Add(second);
        Assert.Same(second, message.UndoLastApproval(first, workflow, reviews, 33, Now));
        Assert.Equal(ReviewStatus.Undone, first.Status);
        Assert.Equal(ReviewStatus.Cancelled, second.Status);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(11, message.CurrentAssigneeId);
    }

    private static WorkflowDefinition Workflow(int levels = 1)
    {
        var workflow = new WorkflowDefinition(MessageDirection.Incoming, "Configurable", "TYPE", 7);
        for (var level = 1; level <= levels; level++) workflow.AddStep(level, level);
        return workflow;
    }
}
