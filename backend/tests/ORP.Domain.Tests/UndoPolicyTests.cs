using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class UndoPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    [Theory]
    [InlineData(UndoApprovalMode.Disabled, 1, false)]
    [InlineData(UndoApprovalMode.Disabled, 2, false)]
    [InlineData(UndoApprovalMode.LatestNonFinal, 1, false)]
    [InlineData(UndoApprovalMode.LatestNonFinal, 2, true)]
    [InlineData(UndoApprovalMode.LatestNonFinal, 3, true)]
    [InlineData(UndoApprovalMode.Latest, 1, true)]
    public void Mode_ControlsUndoOfFirstApproval(UndoApprovalMode mode, int count, bool allowed)
    {
        var (message, workflow, reviews) = Create(Enumerable.Range(1, count).ToArray());
        workflow.ConfigureUndo(mode, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Block);
        var first = Approve(message, workflow, reviews, 1, 10);
        if (allowed)
        {
            Assert.Null(message.UndoLastApproval(first, workflow, reviews, 20, Now));
            Assert.Equal(MessageState.Assigned, message.State);
            Assert.Equal(ReviewStatus.Undone, first.Status);
        }
        else AssertDenied(message, workflow, reviews, first, 20);
    }

    [Theory]
    [InlineData(UndoActorMode.OriginalReviewer, 10, true)]
    [InlineData(UndoActorMode.OriginalReviewer, 20, false)]
    [InlineData(UndoActorMode.AnyAuthorizedUser, 20, true)]
    public void Ownership_IsConfigured(UndoActorMode mode, int actor, bool allowed)
    {
        var (message, workflow, reviews) = Create(1, 2);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, mode, UndoActiveReviewMode.Block);
        var first = Approve(message, workflow, reviews, 1, 10);
        if (allowed) message.UndoLastApproval(first, workflow, reviews, actor, Now);
        else AssertDenied(message, workflow, reviews, first, actor);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void LatestNonFinal_NeverUndoesCompletedWorkflowOrEarlierApproval(int count)
    {
        var (message, workflow, reviews) = Create(Enumerable.Range(1, count).ToArray());
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        foreach (var level in workflow.RequiredLevels()) Approve(message, workflow, reviews, level, level + 10);
        foreach (var review in reviews) AssertDenied(message, workflow, reviews, review, 50);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveNextReview_IsCancelled_AndStaleApprovalCannotComplete(bool skippedSecond)
    {
        var (message, workflow, reviews) = Create(skippedSecond ? [1, 3] : [1, 2]);
        if (skippedSecond) workflow.AddStep(2, 2, false);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        var first = Approve(message, workflow, reviews, 1, 10);
        var next = Start(message, workflow, reviews, skippedSecond ? 3 : 2, 20);
        Assert.Same(next, message.UndoLastApproval(first, workflow, reviews, 30, Now));
        Assert.Equal(ReviewStatus.Cancelled, next.Status);
        Assert.Equal(ReviewStatus.Undone, first.Status);
        Assert.Equal("Original approval", first.Comment);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Throws<DomainRuleViolationException>(() => message.Approve(next, workflow, reviews, 20, null, Now));
        message.Unassign();
        var restarted = Start(message, workflow, reviews, 1, 10);
        Assert.NotSame(first, restarted);
        Assert.Equal(ReviewStatus.InProgress, restarted.Status);
    }

    [Fact]
    public void ThreeSteps_UndoReturnsToSecondStep_AndPreservesFirstApproval()
    {
        var (message, workflow, reviews) = Create(1, 2, 3);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        var first = Approve(message, workflow, reviews, 1, 10);
        var second = Approve(message, workflow, reviews, 2, 20);
        var third = Start(message, workflow, reviews, 3, 30);
        AssertDenied(message, workflow, reviews, first, 40);
        message.UndoLastApproval(second, workflow, reviews, 40, Now);
        Assert.Equal(MessageState.WaitingForSecondReview, message.State);
        Assert.Equal(ReviewStatus.Approved, first.Status);
        Assert.Equal(ReviewStatus.Undone, second.Status);
        Assert.Equal(ReviewStatus.Cancelled, third.Status);
    }

    [Fact]
    public void OptionalFinalStep_DoesNotMakeFinalApprovalReversible()
    {
        var (message, workflow, reviews) = Create(1, 2);
        workflow.AddStep(3, 3, false);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        Approve(message, workflow, reviews, 1, 10);
        var final = Approve(message, workflow, reviews, 2, 20);
        AssertDenied(message, workflow, reviews, final, 30);
    }

    [Fact]
    public void AdministratorOverride_AllowsFinalUndo_ButDoesNotBypassActiveReviewPolicy()
    {
        var (message, workflow, reviews) = Create(1, 2);
        var first = Approve(message, workflow, reviews, 1, 10);
        var next = Start(message, workflow, reviews, 2, 20);
        AssertDenied(message, workflow, reviews, first, 30, true);
        message.Approve(next, workflow, reviews, 20, null, Now);
        message.Unassign();
        message.UndoLastApproval(next, workflow, reviews, 30, Now, true);
        Assert.Equal(MessageState.WaitingForSecondReview, message.State);
        Assert.Equal(ReviewStatus.Approved, first.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveReview_BlockPolicyPreservesBothAttempts(bool administrator)
    {
        var (message, workflow, reviews) = Create(1, 2);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Block);
        var first = Approve(message, workflow, reviews, 1, 10);
        Start(message, workflow, reviews, 2, 20);
        AssertDenied(message, workflow, reviews, first, 30, administrator);
    }

    [Fact]
    public void Administrator_CanUseActiveUndoPolicy_RegardlessOfOriginalReviewer()
    {
        var (message, workflow, reviews) = Create(1, 2);
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.OriginalReviewer, UndoActiveReviewMode.Cancel);
        var first = Approve(message, workflow, reviews, 1, 10);
        var next = Start(message, workflow, reviews, 2, 20);
        Assert.Same(next, message.UndoLastApproval(first, workflow, reviews, 30, Now, true));
    }

    [Fact]
    public void RejectedMessage_AndRepeatedUndo_AreRejected()
    {
        var (message, workflow, reviews) = Create(1, 2);
        workflow.ConfigureUndo(UndoApprovalMode.Latest, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        var first = Approve(message, workflow, reviews, 1, 10);
        message.UndoLastApproval(first, workflow, reviews, 30, Now);
        AssertDenied(message, workflow, reviews, first, 30, true);
        var replacement = Approve(message, workflow, reviews, 1, 10);
        var next = Start(message, workflow, reviews, 2, 20);
        message.Reject(next, 20, null, Now);
        AssertDenied(message, workflow, reviews, replacement, 30);
        AssertDenied(message, workflow, reviews, replacement, 30, true);
    }

    [Theory]
    [InlineData(99, 0, 0)]
    [InlineData(1, 99, 0)]
    [InlineData(1, 1, 99)]
    public void UnknownPolicy_IsRejected(int approval, int actor, int active)
    {
        var (_, workflow, _) = Create(1, 2);
        Assert.Throws<DomainRuleViolationException>(() => workflow.ConfigureUndo(
            (UndoApprovalMode)approval, (UndoActorMode)actor, (UndoActiveReviewMode)active));
        Assert.Equal(UndoApprovalMode.Disabled, workflow.UndoApprovalMode);
    }

    private static void AssertDenied(Message message, WorkflowDefinition workflow, List<Review> reviews, Review target, int actor, bool admin = false)
    {
        var state = message.State;
        var statuses = reviews.Select(r => r.Status).ToArray();
        Assert.Throws<DomainRuleViolationException>(() => message.UndoLastApproval(target, workflow, reviews, actor, Now, admin));
        Assert.Equal(state, message.State);
        Assert.Equal(statuses, reviews.Select(r => r.Status));
    }

    private static (Message, WorkflowDefinition, List<Review>) Create(params int[] levels)
    {
        var workflow = new WorkflowDefinition("Generic workflow", "MT199", 1);
        foreach (var level in levels) workflow.AddStep(level, level);
        return (new Message(1, workflow.Id), workflow, []);
    }

    private static Review Start(Message message, WorkflowDefinition workflow, List<Review> reviews, int level, int reviewer)
    {
        message.Assign(reviewer);
        var review = message.StartReview(level, reviewer, workflow, reviews, Now);
        reviews.Add(review);
        return review;
    }

    private static Review Approve(Message message, WorkflowDefinition workflow, List<Review> reviews, int level, int reviewer)
    {
        var review = Start(message, workflow, reviews, level, reviewer);
        message.Approve(review, workflow, reviews, reviewer, "Original approval", Now);
        message.Unassign();
        return review;
    }
}
