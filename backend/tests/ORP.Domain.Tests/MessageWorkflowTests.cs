using ORP.Domain.Assignments;
using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class MessageWorkflowTests
{
    [Fact]
    public void New_Assign_StartFirstReview()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10);
        var review = message.StartReview(1, 10, workflow, reviews, Now);
        Assert.Equal(MessageState.FirstReviewInProgress, message.State);
        Assert.Equal(ReviewStatus.InProgress, review.Status);
    }

    [Fact]
    public void TwoReviews_FirstThenSecond_Completes()
    {
        var (message, workflow, reviews) = Create(1, 2);
        message.Assign(2);
        CompleteLevel(message, workflow, reviews, 1, 10);
        Assert.Equal(MessageState.WaitingForSecondReview, message.State);
        CompleteLevel(message, workflow, reviews, 2, 11);
        Assert.Equal(MessageState.Completed, message.State);
    }

    [Fact]
    public void ThreeReviews_RequiresThirdBeforeCompletion()
    {
        var (message, workflow, reviews) = Create(1, 2, 3);
        message.Assign(2);
        CompleteLevel(message, workflow, reviews, 1, 10);
        CompleteLevel(message, workflow, reviews, 2, 11);
        Assert.Equal(MessageState.WaitingForThirdReview, message.State);
        CompleteLevel(message, workflow, reviews, 3, 12);
        Assert.Equal(MessageState.Completed, message.State);
    }

    [Fact]
    public void SingleReview_CompletesAfterLevelOne()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(2); CompleteLevel(message, workflow, reviews, 1, 10);
        Assert.Equal(MessageState.Completed, message.State);
    }

    [Fact]
    public void New_ApproveTransition_IsRejected()
    {
        var (message, workflow, reviews) = Create(1);
        var unrelated = new Review(message.Id, 1, 10, Now);
        reviews.Add(unrelated);
        Assert.Throws<DomainRuleViolationException>(() => message.Approve(unrelated, workflow, reviews, 10, null, Now));
        Assert.Equal(ReviewStatus.InProgress, unrelated.Status);
        Assert.Equal(MessageState.New, message.State);
    }

    [Fact]
    public void FourEyes_PreventsReviewerReuse()
    {
        var (message, workflow, reviews) = Create(1, 2);
        message.Assign(2); CompleteLevel(message, workflow, reviews, 1, 10);
        message.Assign(10);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(2, 10, workflow, reviews, Now));
    }

    [Fact]
    public void SameReviewCannotBeApprovedTwice()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10); var review = message.StartReview(1, 10, workflow, reviews, Now); reviews.Add(review);
        message.Approve(review, workflow, reviews, 10, null, Now);
        Assert.Throws<DomainRuleViolationException>(() => message.Approve(review, workflow, reviews, 10, null, Now));
    }

    [Fact]
    public void Reject_AllowsNoComment()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10);
        var review = message.StartReview(1, 10, workflow, reviews, Now);
        reviews.Add(review);

        message.Reject(review, 10, null, Now);

        Assert.Equal(MessageState.Rejected, message.State);
        Assert.Equal(ReviewStatus.Rejected, review.Status);
        Assert.Null(review.Comment);
    }

    [Fact]
    public void AssignmentToSelf_IsRejected() => Assert.Throws<DomainRuleViolationException>(() => new Assignment(1, 7, 7, Now));

    [Fact]
    public void UndoLastApproval_ReopensThatLevelWithoutLosingHistory()
    {
        var (message, workflow, reviews) = Create(1, 2);
        message.Assign(2); CompleteLevel(message, workflow, reviews, 1, 10);
        var review = Assert.Single(reviews);
        message.UndoLastApproval(review, workflow, reviews, 10, Now.AddMinutes(1));
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(ReviewStatus.Undone, review.Status);
        message.Assign(10);
        Assert.Equal(ReviewStatus.InProgress, message.StartReview(1, 10, workflow, reviews, Now.AddMinutes(2)).Status);
    }

    [Fact]
    public void UndoEarlierApproval_AfterLaterApproval_IsRejected()
    {
        var (message, workflow, reviews) = Create(1, 2, 3);
        message.Assign(2);
        CompleteLevel(message, workflow, reviews, 1, 10);
        CompleteLevel(message, workflow, reviews, 2, 11);
        CompleteLevel(message, workflow, reviews, 3, 12);

        Assert.Throws<DomainRuleViolationException>(() =>
            message.UndoLastApproval(reviews.Single(x => x.Level == 1), workflow, reviews, 10, Now.AddMinutes(1)));
        Assert.Equal(MessageState.Completed, message.State);
        Assert.All(reviews, x => Assert.Equal(ReviewStatus.Approved, x.Status));
    }

    [Fact]
    public void OptionalThirdStep_DoesNotBlockCompletion()
    {
        var workflow = new WorkflowDefinition("Optional third", "MT199", 1).AddStep(1, 1).AddStep(2, 2).AddStep(3, 3, false);
        var message = new Message(1, workflow.Id);
        var reviews = new List<Review>(); message.Assign(2);
        CompleteLevel(message, workflow, reviews, 1, 10); CompleteLevel(message, workflow, reviews, 2, 11);
        Assert.Equal(MessageState.Completed, message.State);
    }

    [Fact]
    public void InactiveWorkflow_CannotStartReview()
    {
        var (message, workflow, reviews) = Create(1); message.Assign(10); workflow.Deactivate();
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(1, 10, workflow, reviews, Now));
        Assert.Equal(MessageState.Assigned, message.State);
    }

    [Fact]
    public void WorkflowWithoutRequiredLevel_IsRejectedBeforeUse()
    {
        var workflow = new WorkflowDefinition("Optional", "MT199", 1).AddStep(1, 1, false);

        var exception = Assert.Throws<DomainRuleViolationException>(() => workflow.RequiredLevels());

        Assert.Equal("A workflow must contain at least one required review level.", exception.Message);
    }

    [Fact]
    public void InvalidInactiveWorkflow_CannotBeActivated()
    {
        var workflow = new WorkflowDefinition("Draft", "MT199", 1).AddStep(1, 1, false);
        workflow.Deactivate();

        Assert.Throws<DomainRuleViolationException>(() => workflow.Activate());
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public void WorkflowWhoseFirstRequiredLevelIsNotOne_CannotStartReview()
    {
        var workflow = new WorkflowDefinition("Invalid", "MT199", 1)
            .AddStep(1, 1, false)
            .AddStep(2, 2);
        var message = new Message(1, workflow.Id);
        message.Assign(10);

        Assert.Throws<DomainRuleViolationException>(() =>
            message.StartReview(2, 10, workflow, [], Now));
        Assert.Equal(MessageState.Assigned, message.State);
    }

    [Fact]
    public void RequiredLevels_MustFollowAscendingWorkflowOrder()
    {
        var workflow = new WorkflowDefinition("Invalid order", "MT199", 1)
            .AddStep(1, 1)
            .AddStep(2, 3)
            .AddStep(3, 2);

        Assert.Throws<DomainRuleViolationException>(() => workflow.RequiredLevels());
    }

    [Fact]
    public void OptionalMiddleLevel_CanBeSkipped()
    {
        var workflow = new WorkflowDefinition("Optional middle", "MT199", 1)
            .AddStep(1, 1)
            .AddStep(2, 2, false)
            .AddStep(3, 3);

        Assert.Equal([1, 3], workflow.RequiredLevels());
    }

    [Fact]
    public void Reassign_PreservesCurrentWorkflowState()
    {
        var (message, _, _) = Create(1); message.Assign(2); message.Assign(3);
        Assert.Equal(MessageState.Assigned, message.State); Assert.Equal(3, message.CurrentAssigneeId);
    }

    [Fact]
    public void ActiveReview_CannotBeReassigned()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10);
        reviews.Add(message.StartReview(1, 10, workflow, reviews, Now));

        Assert.Throws<DomainRuleViolationException>(() => message.Assign(11));
        Assert.Equal(10, message.CurrentAssigneeId);
    }

    [Fact]
    public void OnlyAssignedReviewer_CanStartReview()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10);

        Assert.Throws<DomainRuleViolationException>(() =>
            message.StartReview(1, 11, workflow, reviews, Now));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, MessageState.Assigned)]
    [InlineData(2, MessageState.WaitingForSecondReview)]
    [InlineData(3, MessageState.WaitingForThirdReview)]
    public void CancelReview_PreservesAssignmentAndEarlierApprovals_AndAllowsRestart(int level, MessageState waitingState)
    {
        var (message, workflow, reviews) = Create(1, 2, 3);
        for (var previous = 1; previous < level; previous++)
            CompleteLevel(message, workflow, reviews, previous, previous + 10);
        message.Assign(20);
        var review = message.StartReview(level, 20, workflow, reviews, Now);
        reviews.Add(review);

        message.CancelReview(review, 20, Now.AddMinutes(1));

        Assert.Equal(waitingState, message.State);
        Assert.Equal(20, message.CurrentAssigneeId);
        Assert.Equal(ReviewStatus.Cancelled, review.Status);
        Assert.Equal(Now.AddMinutes(1), review.CompletedAt);
        Assert.All(reviews.Where(r => r.Level < level), r => Assert.Equal(ReviewStatus.Approved, r.Status));
        var restarted = message.StartReview(level, 20, workflow, reviews, Now.AddMinutes(2));
        reviews.Add(restarted);
        Assert.NotSame(review, restarted);
        message.CancelReview(restarted, 20, Now.AddMinutes(3));
        message.Assign(21);
        Assert.Equal(ReviewStatus.InProgress, message.StartReview(level, 21, workflow, reviews, Now.AddMinutes(4)).Status);
    }

    [Fact]
    public void CancelReview_RejectsOtherActorsAndMessages_WithoutMutatingReview()
    {
        var (message, workflow, reviews) = Create(1, 2);
        message.Assign(10);
        var review = message.StartReview(1, 10, workflow, reviews, Now);
        Assert.Throws<DomainRuleViolationException>(() => message.CancelReview(review, 11, Now));
        Assert.Throws<DomainRuleViolationException>(() => message.CancelReview(new Review(2, 1, 10, Now), 10, Now));
        Assert.Throws<DomainRuleViolationException>(() => message.CancelReview(new Review(message.Id, 2, 10, Now), 10, Now));
        Assert.Equal(ReviewStatus.InProgress, review.Status);
        Assert.Equal(MessageState.FirstReviewInProgress, message.State);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("cancel")]
    public void CancelReview_RejectsFinishedReviews(string action)
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(10);
        var review = message.StartReview(1, 10, workflow, reviews, Now);
        reviews.Add(review);
        if (action == "approve") message.Approve(review, workflow, reviews, 10, null, Now);
        else if (action == "reject") message.Reject(review, 10, null, Now);
        else
        {
            message.CancelReview(review, 10, Now);
            reviews.Add(message.StartReview(1, 10, workflow, reviews, Now));
        }
        var state = message.State;
        var status = review.Status;
        Assert.Throws<DomainRuleViolationException>(() => message.CancelReview(review, 10, Now));
        Assert.Equal(state, message.State);
        Assert.Equal(status, review.Status);
    }

    private static (Message Message, WorkflowDefinition Workflow, List<Review> Reviews) Create(params int[] levels)
    {
        var workflow = new WorkflowDefinition("Test", "MT199", 1);
        for (var i = 0; i < levels.Length; i++) workflow.AddStep(i + 1, levels[i]);
        var message = new Message(1, workflow.Id);
        return (message, workflow, []);
    }
    private static void CompleteLevel(Message message, WorkflowDefinition workflow, List<Review> reviews, int level, int reviewer)
    {
        message.Assign(reviewer);
        var review = message.StartReview(level, reviewer, workflow, reviews, Now); reviews.Add(review);
        message.Approve(review, workflow, reviews, reviewer, null, Now);
        message.Unassign();
    }
}
