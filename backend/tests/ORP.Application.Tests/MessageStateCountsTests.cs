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
        Assert.Equal(new[] { MessageState.New, MessageState.Assigned, MessageState.Completed }, result.Items.Select(x => x.State));
        Assert.Equal(35, result.Total.Count);
        Assert.Equal(new[] { "M123", "M456" }, result.Total.Breakdown.Select(x => x.MessageType));
        Assert.Equal(new[] { 20, 15 }, result.Total.Breakdown.Select(x => x.Count));
        Assert.Equal(result.Total.Count, result.Total.Breakdown.Sum(x => x.Count));
        Assert.Equal(result.Items.Sum(x => x.Count), result.Total.Count);
        var newState = Assert.Single(result.Items, x => x.State == MessageState.New);
        Assert.Equal(25, newState.Count);
        Assert.Equal(new[] { "M123", "M456" }, newState.Breakdown.Select(x => x.MessageType));
        Assert.Equal(new[] { 10, 15 }, newState.Breakdown.Select(x => x.Count));
        var assigned = Assert.Single(result.Items, x => x.State == MessageState.Assigned);
        Assert.Equal(3, assigned.Count);
        Assert.Equal("M123", Assert.Single(assigned.Breakdown).MessageType);
        Assert.All(result.Items, x => Assert.Equal(x.Count, x.Breakdown.Sum(type => type.Count)));
        Assert.All(result.Items, x => Assert.True(x.Count > 0));
    }

    [Fact]
    public void NoAccessibleMessagesReturnsEmptyItemsAndZeroTotal()
    {
        var result = MessageStateCounts.FromTypeCounts([]);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total.Count);
        Assert.Empty(result.Total.Breakdown);
    }

    [Fact]
    public void ZeroCountStatesAreOmittedWithoutExcludingAnyPopulatedState()
    {
        foreach (var state in Enum.GetValues<MessageState>())
        {
            var populated = MessageStateCounts.FromTypeCounts([(state, "MT999", 1)]);
            Assert.Equal(state, Assert.Single(populated.Items).State);
            Assert.Equal(1, populated.Total.Count);
            var empty = MessageStateCounts.FromTypeCounts([(state, "MT999", 0)]);
            Assert.Empty(empty.Items);
            Assert.Equal(0, empty.Total.Count);
            Assert.Empty(empty.Total.Breakdown);
        }
    }

    [Fact]
    public void BreakdownOrderIsIndependentOfSqlResultOrder()
    {
        (MessageState State, string MessageType, int Count)[] counts =
            [(MessageState.New, "M456", 15), (MessageState.New, "M123", 10)];
        Assert.Equal(MessageStateCounts.FromTypeCounts(counts).Total.Breakdown,
            MessageStateCounts.FromTypeCounts(counts.Reverse()).Total.Breakdown);
        Assert.Equal(MessageStateCounts.FromTypeCounts(counts).Items[0].Breakdown,
            MessageStateCounts.FromTypeCounts(counts.Reverse()).Items[0].Breakdown);
    }

    [Fact]
    public void ResponseContainsItemsAndTotalWithSharedCountAndBreakdownStructure()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var result = MessageStateCounts.FromTypeCounts([(MessageState.New, "M123", 10), (MessageState.New, "M456", 15)]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, options));
        var total = json.RootElement.GetProperty("total");
        Assert.Equal(25, total.GetProperty("count").GetInt32());
        Assert.False(total.TryGetProperty("state", out _));
        var breakdown = total.GetProperty("breakdown");
        Assert.Equal("M123", breakdown[0].GetProperty("messageType").GetString());
        Assert.Equal(10, breakdown[0].GetProperty("count").GetInt32());
        Assert.Equal("M456", breakdown[1].GetProperty("messageType").GetString());
        Assert.Equal(15, breakdown[1].GetProperty("count").GetInt32());
        var items = json.RootElement.GetProperty("items");
        var row = items[0];
        Assert.Equal("New", row.GetProperty("state").GetString());
        Assert.Equal("New", row.GetProperty("stateName").GetString());
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(25, row.GetProperty("count").GetInt32());
        Assert.Equal("M123", row.GetProperty("breakdown")[0].GetProperty("messageType").GetString());
        Assert.Equal(10, row.GetProperty("breakdown")[0].GetProperty("count").GetInt32());
        Assert.Equal("M456", row.GetProperty("breakdown")[1].GetProperty("messageType").GetString());
        Assert.Equal(15, row.GetProperty("breakdown")[1].GetProperty("count").GetInt32());
    }
}
