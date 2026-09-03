using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Application.Audit.GetAuditTrail;

public sealed class GetAuditTrailHandler(IMessageQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<IReadOnlyList<AuditEventDto>> HandleAsync(long messageId, CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.AuditView)) throw new UnauthorizedAccessException();
        return await queries.AuditAsync(messageId, access, ct);
    }
}
