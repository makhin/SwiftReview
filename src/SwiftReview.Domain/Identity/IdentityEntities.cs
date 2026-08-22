namespace SwiftReview.Domain.Identity;

public sealed class User
{
    private User() { }
    public User(string userName, string displayName) { UserName = userName; DisplayName = displayName; }
    public int Id { get; private set; }
    public string UserName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public ICollection<UserRole> Roles { get; private set; } = [];
    public ICollection<UserBranch> Branches { get; private set; } = [];
    public ICollection<UserDepartment> Departments { get; private set; } = [];
}

public sealed class Role
{
    private Role() { }
    public Role(string name) => Name = name;
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public ICollection<RolePermission> Permissions { get; private set; } = [];
}

public sealed class Permission
{
    private Permission() { }
    public Permission(string name) => Name = name;
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
}

public sealed class UserRole { public int UserId { get; set; } public int RoleId { get; set; } public Role Role { get; set; } = null!; }
public sealed class RolePermission { public int RoleId { get; set; } public int PermissionId { get; set; } public Permission Permission { get; set; } = null!; }
public sealed class UserBranch { public int UserId { get; set; } public int BranchId { get; set; } }
public sealed class UserDepartment { public int UserId { get; set; } public int DepartmentId { get; set; } }

public sealed class Branch { private Branch() { } public Branch(string name) => Name = name; public int Id { get; private set; } public string Name { get; private set; } = null!; }
public sealed class Department { private Department() { } public Department(string name) => Name = name; public int Id { get; private set; } public string Name { get; private set; } = null!; }

public static class Permissions
{
    public const string MessageView = "message.view";
    public const string MessageImport = "message.import";
    public const string MessageAssign = "message.assign";
    public const string ReviewLevel1 = "review.level1";
    public const string ReviewLevel2 = "review.level2";
    public const string ReviewLevel3 = "review.level3";
    public const string ReviewReject = "review.reject";
    public const string ReviewUndo = "review.undo";
    public const string AuditView = "audit.view";
    public const string WorkflowManage = "workflow.manage";
    public static readonly string[] All = [MessageView, MessageAssign, ReviewLevel1, ReviewLevel2, ReviewLevel3, ReviewReject, ReviewUndo, AuditView, WorkflowManage, MessageImport];
}
