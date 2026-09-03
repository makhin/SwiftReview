using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class MessageQueries(SwiftReviewDbContext db) : IMessageQueries
{
    public async Task<MessageDetailsDto?> GetAsync(long id, UserAccess access, CancellationToken ct)
    {
        if (!access.Permissions.Contains(Permissions.MessageView)) return null;
        var x = await Accessible(access).SingleOrDefaultAsync(x => x.Id == id, ct);
        return x is null ? null : new MessageDetailsDto(x.Id, x.ExternalId, x.MessageType, x.BranchId, x.DepartmentId,
            x.State, x.ReceivedAt, x.CurrentAssigneeId, x.Sender, x.Receiver, x.Account, x.Currency, x.Amount, x.Reference);
    }

    public async Task<PagedResult<MessageListItemDto>> SearchAsync(MessageSearchRequest request, UserAccess access, CancellationToken ct)
    {
        if (!access.Permissions.Contains(Permissions.MessageView)) return new([], 0);
        var query = Accessible(access);
        var f = request.Filter;
        if (f?.States is { Count: > 0 }) query = query.Where(x => f.States.Contains(x.State));
        if (f?.Branches is { Count: > 0 }) query = query.Where(x => f.Branches.Contains(x.BranchId));
        if (f?.Departments is { Count: > 0 }) query = query.Where(x => f.Departments.Contains(x.DepartmentId));
        if (f?.MessageTypes is { Count: > 0 }) query = query.Where(x => f.MessageTypes.Contains(x.MessageType));
        if (f?.DateFrom is not null) query = query.Where(x => x.ReceivedAt >= f.DateFrom);
        if (f?.DateTo is not null) query = query.Where(x => x.ReceivedAt <= f.DateTo);
        if (!string.IsNullOrWhiteSpace(f?.Account)) query = query.Where(x => x.Account != null && x.Account.Contains(f.Account));
        if (!string.IsNullOrWhiteSpace(f?.Currency)) query = query.Where(x => x.Currency == f.Currency);
        var count = await query.CountAsync(ct);
        query = ApplySort(query, request.Sort);
        var rows = await query.Skip(request.Skip).Take(request.Take).ToListAsync(ct);
        var items = rows.Select(x => new MessageListItemDto(x.Id, x.ExternalId, x.MessageType, x.BranchId,
            x.DepartmentId, x.State, x.ReceivedAt, x.CurrentAssigneeId, x.Account, x.Currency, x.Amount)).ToList();
        return new(items, count);
    }

    public async Task<DashboardSummaryDto> DashboardAsync(UserAccess access, CancellationToken ct)
    {
        if (!access.Permissions.Contains(Permissions.MessageView)) return new(0, 0, 0, 0, 0, 0);
        var q = Accessible(access);
        return new(await q.CountAsync(ct), await q.CountAsync(x => x.State != MessageState.Completed && x.State != MessageState.Rejected, ct),
            await q.CountAsync(x => x.State == MessageState.New || x.State == MessageState.Assigned || x.State == MessageState.FirstReviewInProgress, ct),
            await q.CountAsync(x => x.State == MessageState.WaitingForSecondReview || x.State == MessageState.SecondReviewInProgress, ct),
            await q.CountAsync(x => x.State == MessageState.WaitingForThirdReview || x.State == MessageState.ThirdReviewInProgress, ct),
            await q.CountAsync(x => x.State == MessageState.Completed, ct));
    }

    public async Task<IReadOnlyList<AuditEventDto>> AuditAsync(long messageId, UserAccess access, CancellationToken ct)
    {
        if (!access.Permissions.Contains(Permissions.AuditView) || !await Accessible(access).AnyAsync(x => x.Id == messageId, ct)) return [];
        return await db.AuditEvents.Where(x => x.MessageId == messageId).OrderBy(x => x.Timestamp).ThenBy(x => x.Id)
            .Select(x => new AuditEventDto(x.Id, x.EventType, x.UserId, x.Timestamp, x.OldState, x.NewState, x.DetailsJson, x.CorrelationId)).ToListAsync(ct);
    }

    private IQueryable<MessageReadRow> Accessible(UserAccess access) => db.ReadMessages()
        .Where(x => access.BranchIds.Contains(x.BranchId) && access.DepartmentIds.Contains(x.DepartmentId));

    private static IQueryable<MessageReadRow> ApplySort(IQueryable<MessageReadRow> query, IReadOnlyList<SortClause>? sort)
    {
        var clauses = sort is { Count: > 0 } ? sort : [new SortClause("receivedAt", "desc")];
        IOrderedQueryable<MessageReadRow>? ordered = null;
        foreach (var clause in clauses)
        {
            var desc = clause.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            ordered = clause.Field.ToLowerInvariant() switch
            {
                "state" => Apply(query, ordered, x => x.State, desc),
                "messagetype" => Apply(query, ordered, x => x.MessageType, desc),
                "amount" => Apply(query, ordered, x => x.Amount, desc),
                "externalid" => Apply(query, ordered, x => x.ExternalId, desc),
                _ => Apply(query, ordered, x => x.ReceivedAt, desc)
            };
        }
        return ordered!.ThenBy(x => x.Id);
    }

    private static IOrderedQueryable<MessageReadRow> Apply<TKey>(IQueryable<MessageReadRow> query,
        IOrderedQueryable<MessageReadRow>? ordered, Expression<Func<MessageReadRow, TKey>> key, bool descending) =>
        ordered is null
            ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
            : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
}
