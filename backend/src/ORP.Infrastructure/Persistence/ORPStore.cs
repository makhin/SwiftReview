using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ORP.Application.Abstractions;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public sealed class ORPStore(ORPDbContext db) : IORPStore
{
    public Task<Message?> FindMessageAsync(long id, CancellationToken ct) => db.Messages.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<MessageSourceDto?> FindMessageSourceAsync(long id, CancellationToken ct) => db.SwiftMessageSource.AsNoTracking()
        .Where(x => x.MessageId == id)
        .Select(x => new MessageSourceDto(x.MessageId, x.ExternalId, x.MessageType, x.BranchId, x.DepartmentId,
            x.ReceivedAt, x.Sender, x.Receiver, x.Account, x.Currency, x.Amount, x.Reference))
        .SingleOrDefaultAsync(ct);
    public Task<WorkflowDefinition?> FindWorkflowAsync(int id, CancellationToken ct) => db.WorkflowDefinitions.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<Review>> GetReviewsAsync(long id, CancellationToken ct) => db.Reviews.Where(x => x.MessageId == id).OrderBy(x => x.Level).ToListAsync(ct);
    public Task<Assignment?> GetActiveAssignmentAsync(long id, CancellationToken ct) => db.Assignments.SingleOrDefaultAsync(x => x.MessageId == id && x.EndedAt == null, ct);
    public void AddReview(Review x) => db.Reviews.Add(x);
    public void AddAssignment(Assignment x) => db.Assignments.Add(x);
    public void AddAudit(AuditEvent x) => db.AuditEvents.Add(x);
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw Conflict(exception);
        }
        catch (DbUpdateException exception) when (IsActiveAssignmentConflict(exception))
        {
            throw Conflict(exception);
        }
    }

    private static bool IsActiveAssignmentConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
        sqlException.Message.Contains("IX_Assignments_MessageId", StringComparison.Ordinal);

    private static ConcurrentUpdateException Conflict(Exception exception) =>
        new("The message was changed by another operation. Refresh and try again.", exception);
}
