using ORP.Importer;

namespace ORP.Importer.ConsoleApp;

// Temporary rule until the department-specific business rules are defined.
internal sealed class ConfiguredDepartmentResolver(int departmentId) : ISaaMessageDepartmentResolver
{
    public int? ResolveDepartmentId(SaaMessageDocument document) => departmentId;
}
