using SwiftReview.Application.Abstractions;
using SwiftReview.Application.Messages.Search;
using SwiftReview.Application.Reviews;
using Xunit;

namespace SwiftReview.Application.Tests;

public sealed class ValidationTests
{
    [Fact]
    public async Task GridRequest_RejectsUnboundedPageAndUnknownSort()
    {
        var result = await new MessageSearchValidator().ValidateAsync(new MessageSearchRequest(0, 501,
            [new SortClause("rawContent", "sideways")], null), TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "Take");
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("Field", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("Direction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReviewAndUndo_RejectInvalidLevelCommentAndReviewId()
    {
        var start = await new StartReviewValidator().ValidateAsync(new StartReviewRequest(4), TestContext.Current.CancellationToken);
        var approve = await new ApproveReviewValidator().ValidateAsync(new ApproveReviewRequest(4, null), TestContext.Current.CancellationToken);
        var reject = await new RejectReviewValidator().ValidateAsync(new RejectReviewRequest(1, ""), TestContext.Current.CancellationToken);
        var undo = await new UndoReviewValidator().ValidateAsync(new UndoReviewRequest(0), TestContext.Current.CancellationToken);
        Assert.False(start.IsValid); Assert.False(approve.IsValid); Assert.False(reject.IsValid); Assert.False(undo.IsValid);
        Assert.Contains(start.Errors, x => x.PropertyName == "Level");
        Assert.Contains(approve.Errors, x => x.PropertyName == "Level");
        Assert.Contains(reject.Errors, x => x.PropertyName == "Comment");
        Assert.Contains(undo.Errors, x => x.PropertyName == "ReviewId");
    }
}
