using NSubstitute;
using SwiftReview.Application.Abstractions;
using SwiftReview.Application.Messages.Import;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Workflows;
using Xunit;

namespace SwiftReview.Application.Tests;

public sealed class ImportMessageHandlerTests
{
    [Fact]
    public async Task DuplicateExternalId_IsIdempotent()
    {
        var store = Substitute.For<ISwiftReviewStore>();
        var existing = Message("EXT-1");
        store.FindMessageByExternalIdAsync("EXT-1", Arg.Any<CancellationToken>()).Returns(existing);
        var handler = Handler(store);
        var result = await handler.HandleAsync(Request(), CancellationToken.None);
        Assert.False(result.Created); Assert.Same(existing, result.Message);
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewMessage_WritesBusinessAuditAndOutboxTogether()
    {
        var store = Substitute.For<ISwiftReviewStore>();
        var workflow = new WorkflowDefinition("Single", "MT199", 1).AddStep(1, 1);
        var handler = Handler(store, workflow);
        var result = await handler.HandleAsync(Request(), CancellationToken.None);
        Assert.True(result.Created);
        store.Received(1).AddMessage(Arg.Any<Message>());
        store.Received(1).AddRawData(Arg.Any<MessageRawData>());
        store.Received(1).AddAudit(Arg.Any<Domain.Auditing.AuditEvent>());
        store.Received(1).AddOutbox(Arg.Any<Domain.Outbox.OutboxMessage>());
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentDuplicate_ReturnsWinningImportInsteadOfFailing()
    {
        var store = Substitute.For<ISwiftReviewStore>(); var winner = Message("EXT-1");
        store.FindMessageByExternalIdAsync("EXT-1", Arg.Any<CancellationToken>()).Returns((Message?)null, winner);
        store.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<int>(new DuplicateExternalIdException("race")));
        var result = await Handler(store).HandleAsync(Request(), CancellationToken.None);
        Assert.False(result.Created); Assert.Same(winner, result.Message);
    }

    [Fact]
    public async Task DirectApplicationCall_StillRunsValidation()
    {
        var store = Substitute.For<ISwiftReviewStore>();
        var invalid = Request() with { ExternalId = "" };
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            Handler(store).HandleAsync(invalid, CancellationToken.None));
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void UserAccess_RequiresBothBranchAndDepartment()
    {
        var access = new UserAccess(1, "u", new HashSet<string> { Domain.Identity.Permissions.MessageView }, new HashSet<int> { 1 }, new HashSet<int> { 2 });
        Assert.True(access.CanAccess(1, 2)); Assert.False(access.CanAccess(1, 3)); Assert.False(access.CanAccess(2, 2));
    }

    private static ImportMessageRequest Request() => new("EXT-1", "MT199", 1, 1, DateTimeOffset.UtcNow, "A", "B", null, "EUR", 10, null, "raw");
    private static Message Message(string id) => new(id, "MT199", 1, 1, 0, DateTimeOffset.UtcNow, "A", "B", null, null, null, null);
    private static IClock Clock() { var c = Substitute.For<IClock>(); c.UtcNow.Returns(DateTimeOffset.UtcNow); return c; }
    private static ICorrelationContext Correlation() { var c = Substitute.For<ICorrelationContext>(); c.CorrelationId.Returns("test"); return c; }
    private static ImportMessageHandler Handler(ISwiftReviewStore store, WorkflowDefinition? workflow = null)
    {
        workflow ??= new WorkflowDefinition("Single", "MT199", 1).AddStep(1, 1);
        var resolver = Substitute.For<IWorkflowResolver>();
        resolver.ResolveAsync("MT199", 1, 1, Arg.Any<CancellationToken>()).Returns(workflow);
        return new ImportMessageHandler(store, resolver, new ImportMessageValidator(), Clock(), Correlation());
    }
}
