using System.Text.Json;
using System.Text.Json.Serialization;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Messages.Get;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Application.Tests;

public sealed class MessageViewTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
    private static MessageDetailsDto Message(MessageState state = MessageState.Assigned) =>
        new(901, "BACKEND-001", "MT299", 1, [1], state, Now, 7, "SENDER", "RECEIVER",
            "{4:\r\n  :20:reference\n-}", MessageDirection.Outgoing)
        { BranchName = "London", DepartmentNames = "CS", CurrentAssigneeName = "Jordan" };
    private static MessageViewData Data(int[] levels, params Review[] reviews) => new(levels, reviews, [],
        new Dictionary<int, MessageViewUser> { [7] = new("Jordan", "Reviewer"), [8] = new("Alex", "Approver") });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LevelsAndJsonFollowContract(int count)
    {
        var message = Message(count == 0 ? MessageState.ViewOnly : MessageState.Assigned) with
        {
            TransactionReferenceNumber = count == 0 ? null : "8801IC000673",
            RelatedReference = count == 0 ? null : "1234",
            CurrentAssigneeId = count == 0 ? null : 7,
            CurrentAssigneeName = count == 0 ? null : "Jordan",
            ConfirmationMode = count == 0 ? ConfirmationMode.None
                : count == 1 ? ConfirmationMode.Immediate : ConfirmationMode.ReviewSession,
            CanConfirm = count == 1,
            ConfirmationLevel = count == 1 ? 1 : null,
            CanReview = count > 1
        };
        var result = MessageViewAdapter.Map(message, Data(Enumerable.Range(1, count).ToArray()));
        Assert.Equal(count, result.Approvals.Count);
        if (count > 0)
        {
            Assert.Equal("pending", result.Approvals[0].Status);
            Assert.Equal("Jordan", result.Approvals[0].Assignee);
            Assert.All(result.Approvals.Skip(1), a => { Assert.Equal("waiting", a.Status); Assert.Null(a.Assignee); });
        }
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, jsonOptions));
        var root = json.RootElement;
        Assert.Equal("901", root.GetProperty("id").GetString());
        Assert.Equal(message.Body, root.GetProperty("rawText").GetString());
        Assert.Equal(message.StateName, root.GetProperty("status").GetString());
        Assert.Equal(message.TransactionReferenceNumber, root.GetProperty("transactionReferenceNumber").GetString());
        Assert.Equal(message.RelatedReference, root.GetProperty("relatedReference").GetString());
        Assert.Equal(message.CurrentAssigneeId, root.GetProperty("currentAssigneeId").Deserialize<int?>());
        Assert.Equal(message.CurrentAssigneeName, root.GetProperty("currentAssigneeName").GetString());
        Assert.Equal(message.ConfirmationMode.ToString(), root.GetProperty("confirmationMode").GetString());
        Assert.Equal(message.CanConfirm, root.GetProperty("canConfirm").GetBoolean());
        Assert.Equal(message.ConfirmationLevel, root.GetProperty("confirmationLevel").Deserialize<int?>());
        Assert.Equal(message.CanReview, root.GetProperty("canReview").GetBoolean());
        Assert.Equal(21, root.EnumerateObject().Count());
        if (count > 0)
        {
            var approval = root.GetProperty("approvals")[0];
            Assert.Equal(JsonValueKind.Null, approval.GetProperty("approver").ValueKind);
            Assert.Equal(JsonValueKind.Null, approval.GetProperty("comment").ValueKind);
            Assert.Equal(JsonValueKind.Null, approval.GetProperty("at").ValueKind);
        }
    }

    [Fact]
    public void ActiveReviewerOverridesAssigneeAndDecisionsCarryTheirMetadata()
    {
        var approved = new Review(901, 1, 7, Now);
        approved.Approve("ok", Now.AddMinutes(1));
        var active = new Review(901, 2, 8, Now.AddMinutes(2));
        var result = MessageViewAdapter.Map(Message(MessageState.SecondReviewInProgress), Data([1, 2, 3], approved, active));
        Assert.Equal(new MessageApprovalDto(1, "1st Approval", "approved", null, "Jordan", "Reviewer", "ok", Now.AddMinutes(1)), result.Approvals[0]);
        Assert.Equal("Alex", result.Approvals[1].Assignee);
        Assert.Equal("Approver", result.Approvals[1].Role);
        active.Reject("no", Now.AddMinutes(3));
        result = MessageViewAdapter.Map(Message(MessageState.Rejected), Data([1, 2, 3], approved, active));
        Assert.Equal("rejected", result.Approvals[1].Status);
        Assert.Equal("Alex", result.Approvals[1].Approver);
        Assert.Equal("no", result.Approvals[1].Comment);
        Assert.Null(result.Approvals[1].Assignee);
        Assert.Equal("waiting", result.Approvals[2].Status);
    }

    [Fact]
    public void UndoneAndCancelledReviewsDoNotCountAsDecisions()
    {
        var undone = new Review(901, 1, 7, Now);
        undone.Approve("ok", Now);
        undone.Undo(Now.AddMinutes(1));
        var cancelled = new Review(901, 1, 8, Now.AddMinutes(2));
        cancelled.Cancel(Now.AddMinutes(3));
        var result = MessageViewAdapter.Map(Message(), Data([1, 3], undone, cancelled));
        Assert.Equal("pending", result.Approvals[0].Status);
        Assert.Null(result.Approvals[0].Approver);
        Assert.Null(result.Approvals[0].Comment);
        Assert.Equal("waiting", result.Approvals[1].Status);
        Assert.Equal("3rd Approval", result.Approvals[1].Label);
    }

    [Fact]
    public void AuditIsCompleteOrderedAndMapsEveryEventType()
    {
        var events = Enumerable.Range(1, 505).Reverse().Select(id => new AuditEventDto(id,
            Enum.GetValues<AuditEventType>()[(id - 1) % Enum.GetValues<AuditEventType>().Length], Now.AddMinutes(id / 3), null, null,
            id == 1 ? null : new AuditActorDto(7, "jordan", "Jordan"),
            id == 1 ? new() : new(WorkflowDefinitionId: 2, AssigneeId: 8, ReviewLevel: 1, Comment: "note", PreviousWorkflowDefinitionId: 1), "test")).ToArray();
        var result = MessageViewAdapter.Map(Message(), Data([1]) with { Audit = events });
        Assert.Equal(505, result.AuditLog.Count);
        Assert.Equal(Enumerable.Range(1, 505).Select(i => i.ToString()), result.AuditLog.Select(a => a.Id));
        Assert.Equal("System", result.AuditLog[0].User);
        Assert.Equal("System", result.AuditLog[0].Role);
        Assert.Null(result.AuditLog[0].Detail);
        Assert.Equal("Reviewer", result.AuditLog[1].Role);
        Assert.Equal("Level 1; Assigned to Alex; Workflow changed from 1 to 2; note", result.AuditLog[1].Detail);
        Assert.Equal(Enum.GetValues<AuditEventType>().Length, result.AuditLog.Select(a => a.Type).Distinct().Count());
        Assert.Contains(result.AuditLog, a => a.Type == "pdfGenerate" && a.Action == "Message PDF generated");
    }

    [Theory]
    [InlineData(AuditEventType.MessageUnassigned, 7, null, "Unassigned from Jordan")]
    [InlineData(AuditEventType.MessageReassigned, 7, 8, "Previously assigned to Jordan; Assigned to Alex")]
    [InlineData(AuditEventType.MessageUnassigned, 99, null, "Unassigned from 99")]
    [InlineData(AuditEventType.MessageReassigned, 99, 100, "Previously assigned to 99; Assigned to 100")]
    [InlineData(AuditEventType.MessageUnassigned, null, null, null)]
    [InlineData(AuditEventType.MessageReassigned, null, 8, "Assigned to Alex")]
    public void AssignmentAuditPreservesPreviousAssignee(AuditEventType type, int? previous, int? assigned, string? expected)
    {
        var audit = new AuditEventDto(1, type, Now, null, null, new AuditActorDto(8, "alex", "Alex"),
            new(PreviousAssigneeId: previous, AssigneeId: assigned), "test");
        var result = MessageViewAdapter.Map(Message(), Data([1]) with { Audit = [audit] });
        Assert.Equal(expected, Assert.Single(result.AuditLog).Detail);
    }

    [Fact]
    public async Task HandlerRequiresAuditPermissionBeforeReadingData()
    {
        var views = Substitute.For<IMessageViewQueries>();
        var users = Substitute.For<IUserAccessService>();
        var current = Substitute.For<ICurrentUser>();
        users.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new UserAccess(1, "u", "U", false,
            [new(1, 1, [], [Permissions.MessageView])]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetMessageViewHandler(views, users, current)
            .HandleAsync(901, TestContext.Current.CancellationToken));
        Assert.Empty(views.ReceivedCalls());
    }
}
