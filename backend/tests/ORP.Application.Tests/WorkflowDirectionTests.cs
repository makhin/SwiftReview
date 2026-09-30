using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Application.Tests;

public sealed class WorkflowDirectionTests
{
    [Theory]
    [InlineData(MessageDirection.Incoming, false, true)]
    [InlineData(MessageDirection.Incoming, true, true)]
    [InlineData(MessageDirection.Outgoing, false, false)]
    [InlineData(MessageDirection.Outgoing, true, false)]
    [InlineData(null, false, false)]
    [InlineData(null, true, false)]
    public async Task ManualChangeRequiresMatchingDirection_EvenForAdministrator(
        MessageDirection? sourceDirection, bool administrator, bool allowed)
    {
        var ct = TestContext.Current.CancellationToken;
        var store = Substitute.For<IORPStore>();
        var users = Substitute.For<IUserAuthorizationQueries>();
        var actor = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        actor.UserId.Returns(7);
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        correlation.CorrelationId.Returns("direction-test");
        var message = new Message(1, 99);
        // Different type/department/branch remains a permitted manual override within access rights.
        var workflow = new WorkflowDefinition(MessageDirection.Incoming, "Manual", "MT999", 30, 40).AddStep(1, 1);
        store.FindMessageAsync(1, ct).Returns(message);
        store.FindMessageSourceAsync(1, ct).Returns(new MessageSourceDto(1, "MSG", "MT299", 10, 20,
            DateTimeOffset.UtcNow, "A", "B", sourceDirection));
        store.GetReviewsAsync(1, ct).Returns([]);
        store.FindWorkflowAsync(42, ct).Returns(workflow);
        users.CheckAsync(7, 10, 20, Permissions.WorkflowManage, ct).Returns(new UserPermissionCheck(administrator, true, true));
        users.CanAccessWorkflowAsync(7, workflow.Id, ct).Returns(true);
        var authorization = new MessageAuthorizationService(store, users, actor, correlation, NullLogger<MessageAuthorizationService>.Instance);
        var handler = new ChangeMessageWorkflowHandler(store, new ChangeMessageWorkflowValidator(), users, actor, clock,
            correlation, new InlineTransactions(), authorization);

        if (allowed)
        {
            await handler.HandleAsync(1, new ChangeMessageWorkflowRequest(42), ct);
            Assert.Equal(workflow.Id, message.WorkflowDefinitionId);
            store.Received(1).AddAudit(Arg.Any<AuditEvent>());
            await store.Received(1).SaveChangesAsync(ct);
        }
        else
        {
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => handler.HandleAsync(1, new ChangeMessageWorkflowRequest(42), ct));
            Assert.Equal(99, message.WorkflowDefinitionId);
            Assert.Equal(MessageState.New, message.State);
            store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
            await store.DidNotReceive().SaveChangesAsync(ct);
        }
    }

    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
    }
}
