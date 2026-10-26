using System.Text.Json.Serialization;

namespace ORP.Application.DistributionLists;

public sealed record DistributionListDto(int Id, int DepartmentId, string Name, IReadOnlyList<string> Emails);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveDistributionListRequest(string Name, IReadOnlyList<string> Emails);

public interface IDistributionListService
{
    Task<IReadOnlyList<DistributionListDto>> GetAllAsync(int departmentId, CancellationToken ct);
    Task<DistributionListDto> GetAsync(int departmentId, int id, CancellationToken ct);
    Task<DistributionListDto> CreateAsync(int departmentId, SaveDistributionListRequest request, CancellationToken ct);
    Task<DistributionListDto> UpdateAsync(int departmentId, int id, SaveDistributionListRequest request, CancellationToken ct);
    Task DeleteAsync(int departmentId, int id, CancellationToken ct);
}
