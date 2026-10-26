using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.DistributionLists;
using ORP.Domain.Common;
using ORP.Domain.DistributionLists;

namespace ORP.Infrastructure.Persistence;

public sealed class DistributionListService(ORPDbContext db, ICurrentUser current, IUserAccessService accessService,
    IValidator<SaveDistributionListRequest> validator, ITransactionExecutor transactions) : IDistributionListService
{
    public async Task<IReadOnlyList<DistributionListDto>> GetAllAsync(int departmentId, CancellationToken ct)
    {
        await RequireAccessAsync(departmentId, false, ct);
        var lists = await db.DistributionLists.AsNoTracking().Include(x => x.Emails)
            .Where(x => x.DepartmentId == departmentId).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        return lists.Select(Map).ToArray();
    }

    public async Task<DistributionListDto> GetAsync(int departmentId, int id, CancellationToken ct)
    {
        await RequireAccessAsync(departmentId, false, ct);
        return Map(await FindAsync(departmentId, id, false, ct));
    }

    public Task<DistributionListDto> CreateAsync(int departmentId, SaveDistributionListRequest request, CancellationToken ct) =>
        MutateAsync(async ct =>
        {
            await RequireAccessAsync(departmentId, true, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var list = new DistributionList(departmentId, request.Name, request.Emails);
            db.DistributionLists.Add(list);
            await db.SaveChangesAsync(ct);
            return Map(list);
        }, ct);

    public Task<DistributionListDto> UpdateAsync(int departmentId, int id, SaveDistributionListRequest request, CancellationToken ct) =>
        MutateAsync(async ct =>
        {
            await RequireAccessAsync(departmentId, true, ct);
            var list = await FindAsync(departmentId, id, true, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            list.Replace(request.Name, request.Emails);
            // Even a replacement containing only email changes must advance the aggregate's rowversion.
            db.Entry(list).Property(x => x.Name).IsModified = true;
            await db.SaveChangesAsync(ct);
            return Map(list);
        }, ct);

    public async Task DeleteAsync(int departmentId, int id, CancellationToken ct) =>
        await MutateAsync(async ct =>
        {
            await RequireAccessAsync(departmentId, true, ct);
            db.DistributionLists.Remove(await FindAsync(departmentId, id, true, ct));
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private async Task RequireAccessAsync(int departmentId, bool manage, CancellationToken ct)
    {
        var access = await accessService.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (!(manage ? DistributionListAccess.CanManage(access, departmentId) : DistributionListAccess.CanRead(access, departmentId)))
            throw new UnauthorizedAccessException();
        if (!await db.Departments.AnyAsync(x => x.Id == departmentId, ct))
            throw new ResourceNotFoundException("Department was not found.");
    }

    private async Task<DistributionList> FindAsync(int departmentId, int id, bool tracking, CancellationToken ct)
    {
        var query = db.DistributionLists.Include(x => x.Emails).Where(x => x.DepartmentId == departmentId && x.Id == id);
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct)
            ?? throw new ResourceNotFoundException("Distribution list was not found.");
    }

    private Task<T> MutateAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) =>
        transactions.ExecuteAsync(async token =>
        {
            // Translate conflicts inside the execution strategy so SQL deadlock victims are not replayed.
            try
            {
                return await operation(token);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw Conflict(exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
            {
                throw Conflict(exception);
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                throw Conflict(exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new DomainRuleViolationException("A distribution list with this name, or an email in this list, already exists.");
            }
        }, ct);

    private static ConcurrentUpdateException Conflict(Exception exception) =>
        new("The distribution list was changed by another operation. Reload and try again.", exception);

    private static DistributionListDto Map(DistributionList list) => new(list.Id, list.DepartmentId, list.Name,
        list.Emails.Select(x => x.Email).Order(StringComparer.OrdinalIgnoreCase).ToArray());
}
