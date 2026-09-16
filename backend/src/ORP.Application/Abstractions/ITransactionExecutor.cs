namespace ORP.Application.Abstractions;

public interface ITransactionExecutor
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
