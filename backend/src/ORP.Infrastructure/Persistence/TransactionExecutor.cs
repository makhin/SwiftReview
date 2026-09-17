using System.Data;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Logging;

namespace ORP.Infrastructure.Persistence;

public sealed class TransactionExecutor(ORPDbContext db, BusinessActionLog businessActions) : ITransactionExecutor
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        ExecuteAsync(async ct => { await operation(ct); return true; }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var commitStarted = false;
        try
        {
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                // Never replay a mutation once commit might have reached the server.
                if (commitStarted)
                    throw new InvalidOperationException("Transaction commit outcome is unknown. Reload persisted state before retrying.");
                businessActions.Clear();
                // Each attempt must read fresh state, including permissions, after acquiring the transaction.
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var value = await operation(ct);
                commitStarted = true;
                try
                {
                    await transaction.CommitAsync(ct);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("Transaction commit outcome is unknown. Reload persisted state before retrying.", exception);
                }
                return value;
            }, cancellationToken);
            // Keep logging outside the retry delegate so a logging failure cannot replay a mutation.
            businessActions.Committed();
            return result;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            businessActions.Clear();
        }
    }
}
