using System.Text.Json;
using System.Text.Json.Serialization;
using ORP.Application.Messages.GetStateCounts;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Application.Tests;

public sealed class MessageStateCountsTests
{
    [Fact]
    public void CountsAreSummedPerStateWithSeparateMessageTypeBreakdowns()
    {
        var result = MessageStateCounts.FromTypeCounts([
            (MessageState.New, "M456", 15), (MessageState.Assigned, "M123", 3),
            (MessageState.New, "M123", 10), (MessageState.Completed, "M123", 7)]);
        Assert.Equal(Enum.GetValues<MessageState>(), result.Select(x => x.State));
        var newState = Assert.Single(result, x => x.State == MessageState.New);
        Assert.Equal(25, newState.Count);
        Assert.Equal(new[] { "M123", "M456" }, newState.Breakdown.Select(x => x.MessageType));
        Assert.Equal(new[] { 10, 15 }, newState.Breakdown.Select(x => x.Count));
        var assigned = Assert.Single(result, x => x.State == MessageState.Assigned);
        Assert.Equal(3, assigned.Count);
        Assert.Equal("M123", Assert.Single(assigned.Breakdown).MessageType);
        Assert.All(result, x => Assert.Equal(x.Count, x.Breakdown.Sum(type => type.Count)));
        Assert.All(result.Where(x => x.State is not (MessageState.New or MessageState.Assigned or MessageState.Completed)), x =>
        {
            Assert.Equal(0, x.Count);
            Assert.Empty(x.Breakdown);
        });
    }

    [Fact]
    public void NoAccessibleMessagesReturnsEveryStateWithZeroAndEmptyBreakdown()
    {
        var result = MessageStateCounts.FromTypeCounts([]);
        Assert.Equal(Enum.GetValues<MessageState>(), result.Select(x => x.State));
        Assert.All(result, x => { Assert.Equal(0, x.Count); Assert.Empty(x.Breakdown); });
    }

    [Fact]
    public void BreakdownOrderIsIndependentOfSqlResultOrder()
    {
        (MessageState State, string MessageType, int Count)[] counts =
            [(MessageState.New, "M456", 15), (MessageState.New, "M123", 10)];
        Assert.Equal(MessageStateCounts.FromTypeCounts(counts)[0].Breakdown,
            MessageStateCounts.FromTypeCounts(counts.Reverse())[0].Breakdown);
    }

    [Fact]
    public void ResponseKeepsExistingFieldsAndAddsCamelCaseBreakdown()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var result = MessageStateCounts.FromTypeCounts([(MessageState.New, "M123", 10), (MessageState.New, "M456", 15)]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, options));
        var row = json.RootElement[0];
        Assert.Equal("New", row.GetProperty("state").GetString());
        Assert.Equal(25, row.GetProperty("count").GetInt32());
        Assert.Equal("M123", row.GetProperty("breakdown")[0].GetProperty("messageType").GetString());
        Assert.Equal(10, row.GetProperty("breakdown")[0].GetProperty("count").GetInt32());
        Assert.Equal("M456", row.GetProperty("breakdown")[1].GetProperty("messageType").GetString());
        Assert.Equal(15, row.GetProperty("breakdown")[1].GetProperty("count").GetInt32());
        Assert.Equal(0, json.RootElement[1].GetProperty("breakdown").GetArrayLength());
    }
}
