using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Outbox;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class SwiftReviewStore(SwiftReviewDbContext db) : ISwiftReviewStore
{
    public Task<Message?> FindMessageAsync(long id, CancellationToken ct) => db.Messages.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Message?> FindMessageByExternalIdAsync(string id, CancellationToken ct) => db.Messages.SingleOrDefaultAsync(x => x.ExternalId == id, ct);
    public Task<WorkflowDefinition?> FindWorkflowAsync(int id, CancellationToken ct) => db.WorkflowDefinitions.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<Review>> GetReviewsAsync(long id, CancellationToken ct) => db.Reviews.Where(x => x.MessageId == id).OrderBy(x => x.Level).ToListAsync(ct);
    public Task<Assignment?> GetActiveAssignmentAsync(long id, CancellationToken ct) => db.Assignments.SingleOrDefaultAsync(x => x.MessageId == id && x.EndedAt == null, ct);
    public void AddMessage(Message x) => db.Messages.Add(x);
    public void AddRawData(MessageRawData x) => db.MessageRawData.Add(x);
    public void AddReview(Review x) => db.Reviews.Add(x);
    public void AddAssignment(Assignment x) => db.Assignments.Add(x);
    public void AddAudit(AuditEvent x) => db.AuditEvents.Add(x);
    public void AddOutbox(OutboxMessage x) => db.OutboxMessages.Add(x);
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try { return await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 2601 or 2627 } sql &&
            sql.Message.Contains("IX_Messages_ExternalId", StringComparison.Ordinal))
        { throw new DuplicateExternalIdException("A message with the same external ID was imported concurrently.", ex); }
    }
}
