using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Domain.Common;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Application.Tests;

public sealed class WorkflowDirectionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task ManualChangeMustMatchRoutingAndRefreshesProcessingScope(bool administrator, bool matches, bool viewOnly)
    {
        var ct = TestContext.Current.CancellationToken;
        var store = Substitute.For<IORPStore>();
        var users = Substitute.For<IUserAuthorizationQueries>();
        var resolver = Substitute.For<IWorkflowResolver>();
        var actor = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        actor.UserId.Returns(7);
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        correlation.CorrelationId.Returns("routing-change");
        var message = new Message(1, 99, 10, [20, 30], DateTimeOffset.UtcNow);
        var workflow = new WorkflowDefinition("TARGET", "Target");
        if (viewOnly)
            workflow.ConfigureConfirmation(ConfirmationMode.None, true,
                AssignmentRetentionMode.UntilWorkflowCompleted, false);
        else workflow.AddStep(1, 1);
        typeof(WorkflowDefinition).GetProperty(nameof(WorkflowDefinition.Id))!.SetValue(workflow, 42);
        var other = new WorkflowDefinition("OTHER", "Other").AddStep(1, 1);
        store.FindMessageAsync(1, ct).Returns(message);
        store.FindMessageSourceAsync(1, ct).Returns(new MessageSourceDto(1, "MSG", "MT299", 10, [20, 30],
            DateTimeOffset.UtcNow, "A", "B", MessageDirection.Outgoing));
        store.GetReviewsAsync(1, ct).Returns([]);
        store.FindWorkflowAsync(42, ct).Returns(workflow);
        users.CheckAsync(7, 10, Arg.Any<IReadOnlyList<int>>(), Permissions.WorkflowManage, ct)
            .Returns(new UserPermissionCheck(administrator, true, true));
        resolver.ResolveAsync("MT299", MessageDirection.Outgoing,
            Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 20, 30 })), 10, ct)
            .Returns(new WorkflowResolution(matches ? workflow : other, [20, 30], viewOnly ? [] : [20]));
        var authorization = new MessageAuthorizationService(store, users, actor, correlation,
            NullLogger<MessageAuthorizationService>.Instance);
        var handler = new ChangeMessageWorkflowHandler(store, new ChangeMessageWorkflowValidator(), resolver, actor,
            clock, correlation, new InlineTransactions(), authorization);
        if (matches)
        {
            await handler.HandleAsync(1, new ChangeMessageWorkflowRequest(42), ct);
            Assert.Equal(42, message.WorkflowDefinitionId);
            Assert.Equal(new[] { 20, 30 }, message.DepartmentIds);
            Assert.Equal(viewOnly ? Array.Empty<int>() : new[] { 20 }, message.ProcessingDepartmentIds);
            Assert.Equal(viewOnly ? MessageState.ViewOnly : MessageState.New, message.State);
            store.Received(1).AddAudit(Arg.Is<AuditEvent>(e => e.OldState == MessageState.New && e.NewState == message.State));
            await store.Received(1).SaveChangesAsync(ct);
        }
        else
        {
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => handler.HandleAsync(1, new ChangeMessageWorkflowRequest(42), ct));
            Assert.Equal(99, message.WorkflowDefinitionId);
            await store.DidNotReceive().SaveChangesAsync(ct);
        }
    }

    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
    }
}
