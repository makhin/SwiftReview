using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence;

public sealed class MessageGridRowDto
{
    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public string MessageType { get; init; } = null!;
    public int BranchId { get; init; }
    public int DepartmentId { get; init; }
    public MessageState State { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public int? CurrentAssigneeId { get; init; }
    public string? Account { get; init; }
    public string? Currency { get; init; }
    public decimal? Amount { get; init; }
}

public sealed class MessageGridQueries(ORPDbContext db)
{
    public Task<LoadResult> LoadAsync(DataSourceLoadOptionsBase options, UserAccess access, CancellationToken ct)
    {
        var query = db.ReadMessages()
            .Where(x => access.Permissions.Contains(Permissions.MessageView) &&
                access.BranchIds.Contains(x.BranchId) && access.DepartmentIds.Contains(x.DepartmentId))
            .Select(x => new MessageGridRowDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                MessageType = x.MessageType,
                BranchId = x.BranchId,
                DepartmentId = x.DepartmentId,
                State = x.State,
                ReceivedAt = x.ReceivedAt,
                CurrentAssigneeId = x.CurrentAssigneeId,
                Account = x.Account,
                Currency = x.Currency,
                Amount = x.Amount
            });
        return DataSourceLoader.LoadAsync(query, options, ct);
    }
}
