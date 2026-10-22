using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class ConfirmationPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(ConfirmationMode.Immediate, false)]
    [InlineData(ConfirmationMode.Immediate, true)]
    [InlineData(ConfirmationMode.ReviewSession, false)]
    [InlineData(ConfirmationMode.ReviewSession, true)]
    public void OnlyCurrentAssigneeCanStartAndApprove(ConfirmationMode mode, bool administrator)
    {
        var workflow = Workflow();
        workflow.ConfigureConfirmation(mode, true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        message.Assign(11);
        Assert.Throws<DomainRuleViolationException>(() =>
            message.StartReview(1, 22, workflow, [], Now, isGlobalAdministrator: administrator));
        var review = message.StartReview(1, 11, workflow, [], Now);
        // A persisted legacy review may belong to someone other than the current assignee.
        typeof(Message).GetProperty(nameof(Message.CurrentAssigneeId))!.SetValue(message, 22);
        Assert.Throws<DomainRuleViolationException>(() =>
            message.Approve(review, workflow, [review], 11, null, Now));
        Assert.Throws<DomainRuleViolationException>(() =>
            message.Approve(review, workflow, [review], 22, null, Now));
        Assert.Equal(ReviewStatus.InProgress, review.Status);
    }

    [Fact]
    public void DefaultsPreserveSessionOwnershipAndDistinctReviewers()
    {
        var workflow = Workflow();
        Assert.Equal(ConfirmationMode.ReviewSession, workflow.ConfirmationMode);
        Assert.Equal(AssignmentRetentionMode.PerConfirmation, workflow.AssignmentRetentionMode);
        Assert.True(workflow.RejectAllowed);
        Assert.False(workflow.AllowReviewerReuse);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConfiguredLevelsCanBeConfirmedByTheSameAssignedActor(int count)
    {
        var workflow = Workflow(count);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        message.Assign(22);
        var reviews = new List<Review>();
        for (var level = 1; level <= count; level++)
        {
            var review = message.StartReview(level, 22, workflow, reviews, Now);
            reviews.Add(review);
            message.Approve(review, workflow, reviews, 22, null, Now);
            Assert.Equal(22, message.CurrentAssigneeId);
        }
        Assert.Equal(MessageState.Completed, message.State);
        Assert.All(reviews, x => Assert.Equal(22, x.ReviewerId));
    }

    [Theory]
    [InlineData(true, 22)]
    [InlineData(false, 11)]
    public void ConfiguredOwnershipAndReviewerReuseAreEnforced(bool reuse, int actor)
    {
        var workflow = Workflow(2);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, reuse,
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
        workflow.ConfigureConfirmation(ConfirmationMode.None,
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
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, workflow.Id);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(1, 11, workflow, [], Now, isGlobalAdministrator: true));
    }

    [Fact]
    public void UndoCancelsActiveNextReviewAndPreservesAssignmentAndHistory()
    {
        var workflow = Workflow(2);
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        var message = new Message(1, workflow.Id);
        message.Assign(11);
        var reviews = new List<Review>();
        var first = message.StartReview(1, 11, workflow, reviews, Now);
        reviews.Add(first);
        message.Approve(first, workflow, reviews, 11, null, Now);
        var second = message.StartReview(2, 11, workflow, reviews, Now);
        reviews.Add(second);
        Assert.Same(second, message.UndoLastApproval(first, workflow, reviews, 33, Now));
        Assert.Equal(ReviewStatus.Undone, first.Status);
        Assert.Equal(ReviewStatus.Cancelled, second.Status);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(11, message.CurrentAssigneeId);
    }

    private static WorkflowDefinition Workflow(int levels = 1)
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Configurable");
        for (var level = 1; level <= levels; level++) workflow.AddStep(level, level);
        return workflow;
    }
}
