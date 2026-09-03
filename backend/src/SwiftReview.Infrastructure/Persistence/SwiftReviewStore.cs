using Microsoft.EntityFrameworkCore;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Auditing;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class SwiftReviewStore(SwiftReviewDbContext db) : ISwiftReviewStore
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
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
