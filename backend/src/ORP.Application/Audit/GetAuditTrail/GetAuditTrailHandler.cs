using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Application.Audit.GetAuditTrail;

public sealed class AuditTrailValidator : AbstractValidator<AuditTrailRequest>
{
    public AuditTrailValidator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
    }
}

public sealed class GetAuditTrailHandler(IMessageQueries queries, IUserAccessService users, ICurrentUser current,
    IValidator<AuditTrailRequest> validator)
{
    public async Task<PagedResult<AuditEventDto>> HandleAsync(long messageId, AuditTrailRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!access.Permissions.Contains(Permissions.AuditView)) throw new UnauthorizedAccessException();
        return await queries.AuditAsync(messageId, request, access, ct)
            ?? throw new ResourceNotFoundException("Message was not found or is outside the user's access scope.");
    }
}
