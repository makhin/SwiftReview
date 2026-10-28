using System.Text;
using Microsoft.Extensions.Logging;
using ORP.Domain.Emails;

namespace ORP.Application.Emails;

public interface IForwardingSubjectService
{
    Task<string> GetAsync(long messageId, CancellationToken ct);
}

public sealed record ForwardingSubjectContext(string? MessageType, int BranchId,
    IReadOnlyList<int> ProcessingDepartmentIds, string? Field20, string? Field21);

public sealed class ForwardingSubjectResolver(ILogger<ForwardingSubjectResolver> logger)
{
    public string Resolve(ForwardingSubjectContext context, IEnumerable<ForwardingSubjectRule> rules)
    {
        foreach (var rule in rules.Where(r => r.IsActive &&
            (r.BranchId is null || r.BranchId == context.BranchId) &&
            (r.DepartmentId is null || context.ProcessingDepartmentIds.Contains(r.DepartmentId.Value)) &&
            (r.MessageType is null || string.Equals(r.MessageType, context.MessageType, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.Priority).ThenBy(r => r.Id))
        {
            var subject = Render(rule.Template, context, out var invalid);
            if (invalid || subject is not null && !ValidSubject(subject))
            {
                logger.LogWarning("Forwarding subject rule {RuleId} has an invalid template or result.", rule.Id);
                continue;
            }
            if (subject is not null) return subject;
        }
        var fallback = $"FW: {context.MessageType}";
        return !string.IsNullOrWhiteSpace(context.MessageType) && ValidSubject(fallback)
            ? fallback : "FW: SWIFT message";
    }

    private static bool ValidSubject(string value) =>
        !string.IsNullOrWhiteSpace(value) && CreateEmailValidator.ValidHeader(value, 500);

    // Parse only the template, never substituted values (references may themselves contain braces).
    private static string? Render(string template, ForwardingSubjectContext context, out bool invalid)
    {
        invalid = false;
        var missing = false;
        var result = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '}') { invalid = true; return null; }
            if (template[i] != '{') { result.Append(template[i]); continue; }
            var end = template.IndexOf('}', i + 1);
            if (end < 0) { invalid = true; return null; }
            var token = template[(i + 1)..end];
            string? value;
            switch (token)
            {
                case "MessageType": value = context.MessageType; break;
                case "Field20": value = context.Field20; break;
                case "Field21": value = context.Field21; break;
                default: invalid = true; return null;
            }
            if (string.IsNullOrWhiteSpace(value)) missing = true;
            result.Append(value);
            i = end;
        }
        return missing ? null : result.ToString();
    }
}
