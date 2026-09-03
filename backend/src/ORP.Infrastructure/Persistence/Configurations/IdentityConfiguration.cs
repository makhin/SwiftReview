using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    { builder.ToTable("Users"); builder.HasKey(x => x.Id); builder.Property(x => x.UserName).HasMaxLength(80); builder.HasIndex(x => x.UserName).IsUnique(); builder.Property(x => x.DisplayName).HasMaxLength(160); }
}
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder) { builder.ToTable("Roles"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(80); builder.HasIndex(x => x.Name).IsUnique(); }
}
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder) { builder.ToTable("Permissions"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(80); builder.HasIndex(x => x.Name).IsUnique(); }
}
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder) { builder.ToTable("UserRoles"); builder.HasKey(x => new { x.UserId, x.RoleId }); builder.HasOne<User>().WithMany(x => x.Roles).HasForeignKey(x => x.UserId); builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId); }
}
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder) { builder.ToTable("RolePermissions"); builder.HasKey(x => new { x.RoleId, x.PermissionId }); builder.HasOne<Role>().WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId); builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId); }
}
public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder) { builder.ToTable("Branches"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(80); }
}
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder) { builder.ToTable("Departments"); builder.HasKey(x => x.Id); builder.Property(x => x.Name).HasMaxLength(80); }
}
public sealed class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder) { builder.ToTable("UserBranches"); builder.HasKey(x => new { x.UserId, x.BranchId }); builder.HasOne<User>().WithMany(x => x.Branches).HasForeignKey(x => x.UserId); builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> builder) { builder.ToTable("UserDepartments"); builder.HasKey(x => new { x.UserId, x.DepartmentId }); builder.HasOne<User>().WithMany(x => x.Departments).HasForeignKey(x => x.UserId); builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict); }
}
