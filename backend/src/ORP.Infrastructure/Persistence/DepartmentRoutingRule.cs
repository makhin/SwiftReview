namespace ORP.Infrastructure.Persistence;

public sealed class DepartmentRoutingRule
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public int Direction { get; set; }
    public int MatchKind { get; set; }
    public string MessageTypePattern { get; set; } = null!;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string SourceFile { get; set; } = null!;
    public string SourceSheet { get; set; } = null!;
    public int SourceRow { get; set; }
}
