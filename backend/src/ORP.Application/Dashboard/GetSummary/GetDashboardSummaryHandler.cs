using ORP.Application.Abstractions;

namespace ORP.Application.Dashboard.GetSummary;

public sealed class GetDashboardSummaryHandler(IMessageQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<DashboardSummaryDto> HandleAsync(CancellationToken ct) =>
        await queries.DashboardAsync(await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException(), ct);
}
