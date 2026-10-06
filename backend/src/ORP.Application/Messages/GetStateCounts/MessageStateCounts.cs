using ORP.Application.Abstractions;
using ORP.Domain.Messages;

namespace ORP.Application.Messages.GetStateCounts;

public static class MessageStateCounts
{
    public static IReadOnlyList<MessageStateCountDto> FromTypeCounts(
        IEnumerable<(MessageState State, string MessageType, int Count)> counts)
    {
        var byState = counts.ToLookup(x => x.State);
        return Enum.GetValues<MessageState>().Select(state =>
        {
            var breakdown = byState[state].OrderBy(x => x.MessageType, StringComparer.Ordinal)
                .Select(x => new MessageTypeCountDto(x.MessageType, x.Count)).ToArray();
            return new MessageStateCountDto(state, breakdown.Sum(x => x.Count), breakdown);
        }).ToArray();
    }
}
