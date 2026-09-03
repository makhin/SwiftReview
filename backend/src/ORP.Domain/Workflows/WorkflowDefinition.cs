using ORP.Domain.Common;

namespace ORP.Domain.Workflows;

public sealed class WorkflowDefinition
{
    private readonly List<WorkflowStep> _steps = [];

    private WorkflowDefinition() { }

    public WorkflowDefinition(string name, string messageType, int departmentId, int? branchId = null)
    {
        Name = Required(name, nameof(name));
        MessageType = Required(messageType, nameof(messageType));
        DepartmentId = departmentId;
        BranchId = branchId;
        IsActive = true;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string MessageType { get; private set; } = null!;
    public int DepartmentId { get; private set; }
    public int? BranchId { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<WorkflowStep> Steps => _steps;

    public WorkflowDefinition AddStep(int order, int reviewLevel, bool required = true)
    {
        if (reviewLevel is < 1 or > 3) throw new DomainRuleViolationException("Review level must be between 1 and 3.");
        if (_steps.Any(x => x.Order == order || x.ReviewLevel == reviewLevel))
            throw new DomainRuleViolationException("Workflow step order and review level must be unique.");
        _steps.Add(new WorkflowStep(order, reviewLevel, required));
        return this;
    }

    public IReadOnlyList<int> RequiredLevels() => _steps.Where(x => x.Required).OrderBy(x => x.Order).Select(x => x.ReviewLevel).ToList();
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
}

public sealed class WorkflowStep
{
    private WorkflowStep() { }
    internal WorkflowStep(int order, int reviewLevel, bool required)
    {
        Order = order;
        ReviewLevel = reviewLevel;
        Required = required;
    }

    public int Id { get; private set; }
    public int WorkflowDefinitionId { get; private set; }
    public int Order { get; private set; }
    public int ReviewLevel { get; private set; }
    public bool Required { get; private set; }
}
