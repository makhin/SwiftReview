using System.Data;
using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Endpoints;

// Keep the permission read and review creation atomic with administrative revocations.
// A revocation must either see the active review or finish before authorization reads access.
// Workflow changes share this transaction boundary so they cannot race the first review start.
public sealed class StartReviewTransactionFilter(ORPDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!db.Database.IsRelational()) return await next(context);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,
                context.HttpContext.RequestAborted);
            var result = await next(context);
            await transaction.CommitAsync(context.HttpContext.RequestAborted);
            return result;
        });
    }
}
