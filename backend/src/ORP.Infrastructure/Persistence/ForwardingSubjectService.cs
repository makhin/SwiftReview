using Microsoft.EntityFrameworkCore;
using ORP.Application.Authorization;
using ORP.Application.Emails;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class ForwardingSubjectService(ORPDbContext db, MessageAuthorizationService authorization,
    ForwardingSubjectResolver resolver) : IForwardingSubjectService
{
    public async Task<string> GetAsync(long messageId, CancellationToken ct)
    {
        var authorized = await authorization.RequireAsync(messageId, Permissions.MessageForward, ct);
        var source = await db.SwiftMessages.AsNoTracking().Where(x => x.MessageId == messageId)
            .Select(x => new { x.MessageType, x.TransactionReferenceNumber, x.RelatedReference }).SingleAsync(ct);
        var rules = await db.ForwardingSubjectRules.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        return resolver.Resolve(new(source.MessageType, authorized.Source.BranchId,
            authorized.Message.ProcessingDepartmentIds, source.TransactionReferenceNumber, source.RelatedReference), rules);
    }
}
