namespace ORP.Domain.Emails;

public sealed class ForwardingSubjectRule
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public string? MessageType { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public string Template { get; set; } = null!;
}
