using ORP.Application.Abstractions;
using ORP.Application.Audit.GetAuditTrail;
using ORP.Application.Messages.Search;
using ORP.Application.Reviews;
using Xunit;

namespace ORP.Application.Tests;

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
    public async Task AuditRequest_RejectsInvalidPageBounds()
    {
        var validator = new AuditTrailValidator();
        var ct = TestContext.Current.CancellationToken;
        Assert.False((await validator.ValidateAsync(new AuditTrailRequest(-1, 100), ct)).IsValid);
        Assert.False((await validator.ValidateAsync(new AuditTrailRequest(0, 0), ct)).IsValid);
        Assert.False((await validator.ValidateAsync(new AuditTrailRequest(0, 501), ct)).IsValid);
        Assert.True((await validator.ValidateAsync(new AuditTrailRequest(), ct)).IsValid);
    }

    [Fact]
    public async Task ReviewAndUndo_ValidateLevelOptionalCommentAndReviewId()
    {
        var start = await new StartReviewValidator().ValidateAsync(new StartReviewRequest(4), TestContext.Current.CancellationToken);
        var approve = await new ApproveReviewValidator().ValidateAsync(new ApproveReviewRequest(4, null), TestContext.Current.CancellationToken);
        var reject = await new RejectReviewValidator().ValidateAsync(new RejectReviewRequest(1, null), TestContext.Current.CancellationToken);
        var longReject = await new RejectReviewValidator().ValidateAsync(new RejectReviewRequest(1, new string('x', 2001)), TestContext.Current.CancellationToken);
        var undo = await new UndoReviewValidator().ValidateAsync(new UndoReviewRequest(0), TestContext.Current.CancellationToken);
        Assert.False(start.IsValid); Assert.False(approve.IsValid); Assert.True(reject.IsValid); Assert.False(longReject.IsValid); Assert.False(undo.IsValid);
        Assert.Contains(start.Errors, x => x.PropertyName == "Level");
        Assert.Contains(approve.Errors, x => x.PropertyName == "Level");
        Assert.Contains(longReject.Errors, x => x.PropertyName == "Comment");
        Assert.Contains(undo.Errors, x => x.PropertyName == "ReviewId");
    }
}
