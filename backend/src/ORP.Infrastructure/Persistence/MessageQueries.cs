using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence;

public sealed class MessageQueries(ORPDbContext db) : IMessageQueries
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<PagedResult<AuditEventDto>?> AuditAsync(long messageId, AuditTrailRequest request,
        UserAccess access, CancellationToken ct)
    {
        if (!await Accessible(access).AnyAsync(x => x.Id == messageId, ct)) return null;
        var query = db.AuditEvents.AsNoTracking().Where(x => x.MessageId == messageId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .Skip(request.Skip).Take(request.Take)
            .Select(x => new AuditRow(x.Id, x.EventType, x.Timestamp, x.OldState, x.NewState,
                x.UserId, x.User == null ? null : x.User.UserName, x.User == null ? null : x.User.DisplayName,
                x.ReviewId, x.DetailsJson, x.CorrelationId))
            .ToListAsync(ct);
        return new(rows.Select(MapAudit).ToList(), count);
    }

    private static AuditEventDto MapAudit(AuditRow row)
    {
        var stored = JsonSerializer.Deserialize<StoredAuditDetails>(row.DetailsJson, AuditJsonOptions)
            ?? new StoredAuditDetails();
        var actor = row.UserId is null ? null : new AuditActorDto(row.UserId.Value,
            row.UserName ?? string.Empty, row.DisplayName ?? string.Empty);
        var details = new AuditEventDetailsDto(stored.WorkflowDefinitionId, stored.PreviousAssigneeId,
            stored.AssigneeId ?? stored.AssignedTo, row.ReviewId, stored.ReviewLevel ?? stored.Level, stored.Comment);
        return new AuditEventDto(row.Id, row.EventType, row.Timestamp, row.OldState?.ToString(), row.NewState?.ToString(),
            actor, details, row.CorrelationId);
    }

    private IQueryable<MessageReadRow> Accessible(UserAccess access)
    {
        var allDepartments = access.HasAllDepartmentAccess;
        return db.ReadMessages().Where(x => access.BranchIds.Contains(x.BranchId) &&
            (allDepartments || access.DepartmentIds.Contains(x.DepartmentId)));
    }

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

    private sealed record AuditRow(long Id, AuditEventType EventType, DateTimeOffset Timestamp,
        MessageState? OldState, MessageState? NewState, int? UserId, string? UserName, string? DisplayName,
        long? ReviewId, string DetailsJson, string CorrelationId);

    private sealed record StoredAuditDetails(int? WorkflowDefinitionId = null, int? PreviousAssigneeId = null,
        int? AssigneeId = null, int? AssignedTo = null, int? ReviewLevel = null, int? Level = null,
        string? Comment = null);
}
