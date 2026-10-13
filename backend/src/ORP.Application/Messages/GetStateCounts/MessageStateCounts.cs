using ORP.Application.Abstractions;
using ORP.Domain.Messages;

namespace ORP.Application.Messages.GetStateCounts;

public static class MessageStateCounts
{
    public static MessageStateCountsDto FromTypeCounts(
        IEnumerable<(MessageState State, string MessageType, int Count)> counts)
    {
        var byState = counts.ToLookup(x => x.State);
        var items = Enum.GetValues<MessageState>().Select(state =>
        {
            var breakdown = byState[state].OrderBy(x => x.MessageType, StringComparer.Ordinal)
                .Select(x => new MessageTypeCountDto(x.MessageType, x.Count)).ToArray();
            return new MessageStateCountDto(state, breakdown.Sum(x => x.Count), breakdown);
        }).ToArray();
        var breakdown = items.SelectMany(x => x.Breakdown)
            .GroupBy(x => x.MessageType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MessageTypeCountDto(group.Key, group.Sum(x => x.Count))).ToArray();
        return new(items, new MessageCountDto(items.Sum(x => x.Count), breakdown));
    }
}
