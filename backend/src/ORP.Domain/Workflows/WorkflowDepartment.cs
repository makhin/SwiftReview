namespace ORP.Domain.Workflows;

public sealed class WorkflowDepartment
{
    private WorkflowDepartment() { }
    public WorkflowDepartment(int departmentId) => DepartmentId = departmentId;

    public int WorkflowDefinitionId { get; private set; }
    public int DepartmentId { get; private set; }
}
