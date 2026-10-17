using System.Text.RegularExpressions;

namespace ORP.Importer;

public enum SaaDepartmentMatchKind { Exact = 1, MtSeries = 2, MxFamily = 3 }

public sealed class SaaDepartmentRule
{
    public int Id { get; set; }
    public int? WorkflowDefinitionId { get; set; }
    public int DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public SaaMessageDirection Direction { get; set; }
    public SaaDepartmentMatchKind MatchKind { get; set; }
    public string MessageTypePattern { get; set; } = null!;
    public bool IsActive { get; set; }
}

public sealed class SaaDepartmentResolution(IReadOnlyList<int> departmentIds, IReadOnlyList<int> ruleIds)
{
    public IReadOnlyList<int> DepartmentIds { get; } = departmentIds;
    public IReadOnlyList<int> RuleIds { get; } = ruleIds;
}

public sealed class SaaDepartmentRuleResolver : ISaaMessageDepartmentResolver
{
    private readonly SaaDepartmentRule[] rules;
    private static readonly Regex MxPattern = new(@"\AMX[a-z]{4}\.[0-9]{3}(?:\.[0-9]{3}\.[0-9]{2})?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public SaaDepartmentRuleResolver(IEnumerable<SaaDepartmentRule> rules)
    {
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        this.rules = rules.Where(rule => rule.IsActive).ToArray();
        if (this.rules.Length == 0 || this.rules.Any(rule => rule.Id <= 0 || rule.DepartmentId <= 0 ||
            rule.BranchId is <= 0 || string.IsNullOrWhiteSpace(rule.MessageTypePattern) ||
            !Enum.IsDefined(typeof(SaaMessageDirection), rule.Direction) ||
            !Enum.IsDefined(typeof(SaaDepartmentMatchKind), rule.MatchKind) || !ValidPattern(rule)))
            throw new InvalidOperationException("Active department rules are empty or invalid.");
    }

    public SaaDepartmentResolution? Resolve(int branchId, string messageType, SaaMessageDirection direction)
    {
        var matches = SelectRules(rules, branchId, messageType, direction);
        if (matches.Length == 0) return null;
        return new SaaDepartmentResolution(matches.Select(rule => rule.DepartmentId).Distinct().OrderBy(id => id).ToArray(),
            matches.Select(rule => rule.Id).OrderBy(id => id).ToArray());
    }

    public static SaaDepartmentRule[] SelectRules(IEnumerable<SaaDepartmentRule> rules, int branchId,
        string messageType, SaaMessageDirection direction)
    {
        return rules.Where(rule => rule.IsActive && (rule.BranchId is null || rule.BranchId == branchId) &&
                rule.Direction == direction && Matches(rule, messageType))
            .GroupBy(rule => rule.DepartmentId).SelectMany(group =>
            {
                var scoped = group.Where(rule => rule.BranchId == branchId).ToArray();
                var candidates = scoped.Length > 0 ? scoped : group.ToArray();
                var exact = candidates.Where(rule => rule.MatchKind == SaaDepartmentMatchKind.Exact).ToArray();
                var preferred = exact.Length > 0 ? exact : candidates;
                if (preferred.Select(rule => rule.WorkflowDefinitionId).Distinct().Count() != 1)
                    throw new FormatException("Equally preferred department rules select different workflows.");
                return preferred;
            }).ToArray();
    }

    private static bool ValidPattern(SaaDepartmentRule rule) => rule.MatchKind switch
    {
        SaaDepartmentMatchKind.Exact => Regex.IsMatch(rule.MessageTypePattern, @"\AMT[0-9]{3}\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || MxPattern.IsMatch(rule.MessageTypePattern),
        SaaDepartmentMatchKind.MtSeries => rule.MessageTypePattern.Length == 5 &&
            rule.MessageTypePattern.StartsWith("MT", StringComparison.OrdinalIgnoreCase) &&
            rule.MessageTypePattern[2] >= '0' && rule.MessageTypePattern[2] <= '9' &&
            rule.MessageTypePattern.EndsWith("xx", StringComparison.OrdinalIgnoreCase),
        SaaDepartmentMatchKind.MxFamily => MxPattern.IsMatch(rule.MessageTypePattern),
        _ => false
    };

    private static bool Matches(SaaDepartmentRule rule, string messageType) => rule.MatchKind switch
    {
        SaaDepartmentMatchKind.Exact => string.Equals(rule.MessageTypePattern, messageType, StringComparison.OrdinalIgnoreCase),
        SaaDepartmentMatchKind.MtSeries => messageType.Length == 5 &&
            messageType.StartsWith(rule.MessageTypePattern.Substring(0, 3), StringComparison.OrdinalIgnoreCase) &&
            messageType[3] >= '0' && messageType[3] <= '9' && messageType[4] >= '0' && messageType[4] <= '9',
        SaaDepartmentMatchKind.MxFamily => MxPattern.IsMatch(messageType) && (string.Equals(rule.MessageTypePattern, messageType, StringComparison.OrdinalIgnoreCase) ||
            (rule.MessageTypePattern.Count(c => c == '.') == 1 &&
             messageType.StartsWith(rule.MessageTypePattern + ".", StringComparison.OrdinalIgnoreCase))),
        _ => false
    };
}
