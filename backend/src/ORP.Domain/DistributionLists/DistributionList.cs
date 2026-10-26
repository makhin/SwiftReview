namespace ORP.Domain.DistributionLists;

public sealed class DistributionList
{
    private DistributionList() { }
    public DistributionList(int departmentId, string name, IReadOnlyList<string> emails)
    {
        DepartmentId = departmentId;
        Replace(name, emails);
    }

    public int Id { get; private set; }
    public int DepartmentId { get; private set; }
    public string Name { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<DistributionListEmail> Emails { get; private set; } = [];

    public void Replace(string name, IReadOnlyList<string> emails)
    {
        Name = name.Trim();
        var desired = emails.Select(e => e.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var email in Emails.Where(e => !desired.Contains(e.Email)).ToArray())
            Emails.Remove(email);
        var existing = Emails.ToDictionary(e => e.Email, StringComparer.OrdinalIgnoreCase);
        foreach (var value in emails.Select(e => e.Trim()))
        {
            if (existing.TryGetValue(value, out var email)) email.ChangeAddress(value);
            else Emails.Add(new DistributionListEmail(value));
        }
    }
}

public sealed class DistributionListEmail
{
    private DistributionListEmail() { }
    internal DistributionListEmail(string email) => Email = email;
    public int Id { get; private set; }
    public int DistributionListId { get; private set; }
    public string Email { get; private set; } = null!;
    internal void ChangeAddress(string email) => Email = email;
}
