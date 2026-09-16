using System.Data;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;

namespace ORP.Infrastructure.Persistence;

public sealed class TransactionExecutor(ORPDbContext db) : ITransactionExecutor
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        ExecuteAsync(async ct => { await operation(ct); return true; }, cancellationToken);

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        // Mock mode has no transaction support. Relational operations include authorization reads.
        if (!db.Database.IsRelational()) return operation(cancellationToken);
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // Each attempt must read fresh state, including permissions, after acquiring the transaction.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await operation(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }
}
