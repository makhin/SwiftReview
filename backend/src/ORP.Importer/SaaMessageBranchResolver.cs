namespace ORP.Importer;

public interface ISaaMessageBranchResolver
{
    int? ResolveBranchId(string branch);
}

/// <summary>A snapshot of branch names and IDs supplied by the consuming application.</summary>
public sealed class SaaMessageBranchResolver : ISaaMessageBranchResolver
{
    private readonly Dictionary<string, int?> branches = new(StringComparer.OrdinalIgnoreCase);

    public SaaMessageBranchResolver(IEnumerable<KeyValuePair<string, int>> branches)
    {
        if (branches is null) throw new ArgumentNullException(nameof(branches));
        foreach (var branch in branches)
        {
            if (string.IsNullOrWhiteSpace(branch.Key) || branch.Value <= 0)
                throw new ArgumentException("Branch mappings require a non-empty name and a positive ID.", nameof(branches));
            var name = branch.Key.Trim();
            if (this.branches.TryGetValue(name, out var existing) && existing != branch.Value)
                this.branches[name] = null;
            else if (!this.branches.ContainsKey(name))
                this.branches.Add(name, branch.Value);
        }
    }

    public int? ResolveBranchId(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch) || !branches.TryGetValue(branch.Trim(), out var id))
            return null;
        if (id is null)
            throw new FormatException("Header field 'Branch' matches multiple branch IDs.");
        return id;
    }
}
