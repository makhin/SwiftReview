using System.Text.RegularExpressions;
using ORP.Domain.Messages;

namespace ORP.Domain.Workflows;

public static class WorkflowMatching
{
    private static readonly Regex MtSeries = new(@"\AMT[0-9]xx\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MtType = new(@"\AMT[0-9]{3}\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MxFamily = new(@"\AMX[a-z]{4}\.[0-9]{3}\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MxType = new(@"\AMX[a-z]{4}\.[0-9]{3}(?:\.[0-9]{3}\.[0-9]{2})?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Selection returns every equally preferred candidate; callers reject ambiguity
    // and validate the selected workflow's review steps in their own error vocabulary.
    public static WorkflowDefinition[] GetPreferred(IEnumerable<WorkflowDefinition> candidates,
        string messageType, MessageDirection direction, IReadOnlyCollection<int> departmentIds, int branchId)
    {
        var requested = departmentIds.Order().ToArray();
        var matches = candidates.Where(candidate => candidate.IsActive &&
            candidate.Direction == direction && candidate.DepartmentIds.SequenceEqual(requested) &&
            (candidate.BranchId is null || candidate.BranchId == branchId) &&
            Matches(candidate.MessageType, messageType)).ToArray();
        var scoped = matches.Where(candidate => candidate.BranchId == branchId).ToArray();
        var preferred = scoped.Length > 0 ? scoped : matches;
        var exact = preferred.Where(candidate =>
            string.Equals(candidate.MessageType, messageType, StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length > 0 ? exact : preferred;
    }

    public static WorkflowDefinition[] GetPreferred(IEnumerable<WorkflowDefinition> candidates,
        string messageType, MessageDirection direction, int departmentId, int branchId) =>
        GetPreferred(candidates, messageType, direction, [departmentId], branchId);

    private static bool Matches(string pattern, string messageType)
    {
        if (string.Equals(pattern, messageType, StringComparison.OrdinalIgnoreCase)) return true;
        if (MtSeries.IsMatch(pattern))
            return MtType.IsMatch(messageType) && pattern[2] == messageType[2];
        return MxFamily.IsMatch(pattern) && MxType.IsMatch(messageType) &&
            messageType.StartsWith(pattern + ".", StringComparison.OrdinalIgnoreCase);
    }
}
