using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Common;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;
using Xunit;

namespace SwiftReview.Domain.Tests;

public sealed class MessageWorkflowTests
{
    [Fact]
    public void New_Assign_StartFirstReview()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(2);
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
        Assert.Throws<DomainRuleViolationException>(() => message.Approve(unrelated, workflow, reviews, null, Now));
        Assert.Equal(ReviewStatus.InProgress, unrelated.Status);
        Assert.Equal(MessageState.New, message.State);
    }

    [Fact]
    public void FourEyes_PreventsReviewerReuse()
    {
        var (message, workflow, reviews) = Create(1, 2);
        message.Assign(2); CompleteLevel(message, workflow, reviews, 1, 10);
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(2, 10, workflow, reviews, Now));
    }

    [Fact]
    public void SameReviewCannotBeApprovedTwice()
    {
        var (message, workflow, reviews) = Create(1);
        message.Assign(2); var review = message.StartReview(1, 10, workflow, reviews, Now); reviews.Add(review);
        message.Approve(review, workflow, reviews, null, Now);
        Assert.Throws<DomainRuleViolationException>(() => message.Approve(review, workflow, reviews, null, Now));
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
        var (message, workflow, reviews) = Create(1); message.Assign(2); workflow.Deactivate();
        Assert.Throws<DomainRuleViolationException>(() => message.StartReview(1, 10, workflow, reviews, Now));
        Assert.Equal(MessageState.Assigned, message.State);
    }

    [Fact]
    public void Reassign_PreservesCurrentWorkflowState()
    {
        var (message, _, _) = Create(1); message.Assign(2); message.Assign(3);
        Assert.Equal(MessageState.Assigned, message.State); Assert.Equal(3, message.CurrentAssigneeId);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static (Message Message, WorkflowDefinition Workflow, List<Review> Reviews) Create(params int[] levels)
    {
        var workflow = new WorkflowDefinition("Test", "MT199", 1);
        for (var i = 0; i < levels.Length; i++) workflow.AddStep(i + 1, levels[i]);
        var message = new Message(1, workflow.Id);
        return (message, workflow, []);
    }
    private static void CompleteLevel(Message message, WorkflowDefinition workflow, List<Review> reviews, int level, int reviewer)
    {
        var review = message.StartReview(level, reviewer, workflow, reviews, Now); reviews.Add(review);
        message.Approve(review, workflow, reviews, null, Now);
    }
}
