using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace ORP.Sync;

internal sealed class RouteResult
{
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public string Error { get; set; }
    public bool IsRouted => BranchId.HasValue && DepartmentId.HasValue;
}

internal sealed class RoutingRules
{
    private readonly IReadOnlyList<Rule> _rules;

    private RoutingRules(IReadOnlyList<Rule> rules) => _rules = rules;

    public static RoutingRules FromConfiguration()
    {
        var setting = ConfigurationManager.AppSettings["RoutingRules"];
        if (string.IsNullOrWhiteSpace(setting)) return new RoutingRules(Array.Empty<Rule>());
        var rules = setting.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Rule.Parse).OrderBy(rule => rule.Priority).ToList();
        if (rules.GroupBy(rule => rule.Priority).Any(group => group.Count() > 1))
            throw new ConfigurationErrorsException("Routing rule priorities must be unique.");
        return new RoutingRules(rules);
    }

    public RouteResult Resolve(SwiftMessageData message)
    {
        var rule = _rules.FirstOrDefault(candidate => candidate.Matches(message));
        return rule == null
            ? new RouteResult { Error = "No routing rule matched." }
            : new RouteResult { BranchId = rule.BranchId, DepartmentId = rule.DepartmentId };
    }

    public IReadOnlyCollection<int> BranchIds => _rules.Select(rule => rule.BranchId).Distinct().ToArray();
    public IReadOnlyCollection<int> DepartmentIds => _rules.Select(rule => rule.DepartmentId).Distinct().ToArray();

    private sealed class Rule
    {
        public int Priority { get; set; }
        public string BicField { get; set; }
        public string Bic { get; set; }
        public string Direction { get; set; }
        public IReadOnlyCollection<string> MessageTypes { get; set; }
        public int BranchId { get; set; }
        public int DepartmentId { get; set; }

        public static Rule Parse(string value)
        {
            var parts = value.Split('|').Select(part => part.Trim()).ToArray();
            if (parts.Length != 7 || !int.TryParse(parts[0], out var priority) ||
                !int.TryParse(parts[5], out var branchId) || !int.TryParse(parts[6], out var departmentId))
                throw new ConfigurationErrorsException(
                    "RoutingRules format is priority|bicField|bic|direction|messageTypes|branchId|departmentId.");
            if (!new[] { "OwnBic", "SenderRequestor", "ReceiverResponder" }
                    .Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException($"Unsupported routing BIC field '{parts[1]}'.");
            return new Rule
            {
                Priority = priority,
                BicField = parts[1],
                Bic = Normalize(parts[2]),
                Direction = Normalize(parts[3]),
                MessageTypes = parts[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Normalize).ToArray(),
                BranchId = branchId,
                DepartmentId = departmentId
            };
        }

        public bool Matches(SwiftMessageData message)
        {
            var actualBic = Normalize(BicField.Equals("OwnBic", StringComparison.OrdinalIgnoreCase)
                ? message.OwnBic
                : BicField.Equals("SenderRequestor", StringComparison.OrdinalIgnoreCase)
                    ? message.SenderRequestorBic8 ?? message.SenderRequestor
                    : message.ReceiverResponderBic8 ?? message.ReceiverResponder);
            var bicMatches = string.IsNullOrEmpty(Bic) || Bic.EndsWith("*", StringComparison.Ordinal)
                ? actualBic.StartsWith(Bic.TrimEnd('*'), StringComparison.Ordinal)
                : Bic.Length == 8 ? actualBic.StartsWith(Bic, StringComparison.Ordinal) : actualBic == Bic;
            var directionMatches = string.IsNullOrEmpty(Direction) || Direction == Normalize(message.Direction);
            var fullType = Normalize(message.MessageType);
            var shortType = Normalize(message.MessageTypeShort);
            var typeMatches = MessageTypes.Count == 0 || MessageTypes.Contains(fullType) || MessageTypes.Contains(shortType);
            return bicMatches && directionMatches && typeMatches;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
