using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MessageNamesSqlTests
{
    [Fact]
    public async Task GridSearchAndDetailsReturnNamesForUnassignedAndReviewedMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        await using var db = new ORPDbContext(fixture.Options);
        var admin = new User("admin", "Administrator", true);
        var assignee = new User("assignee", "Assigned user");
        var reviewer = new User("reviewer", "Reviewing user");
        var extraDepartment = new Department("Accounting");
        db.AddRange(admin, assignee, reviewer, extraDepartment);
        await db.SaveChangesAsync(ct);
        var message = await db.Messages.Include(x => x.Departments).SingleAsync(ct);
        var department = await db.Departments.SingleAsync(x => x.Id != extraDepartment.Id, ct);
        db.MessageDepartments.Add(new MessageDepartment(message.Id, extraDepartment.Id));
        await db.SaveChangesAsync(ct);
        var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(ct);
        var access = new UserAccess(admin.Id, admin.UserName, admin.DisplayName, true, []);
        var gridQueries = new MessageGridQueries(db);
        var queries = new MessageQueries(db);

        var unassigned = Assert.Single((await gridQueries.LoadAsync(new(), access, ct)).Items);
        Assert.Equal("Paris", unassigned.BranchName);
        Assert.Equal([department.Id, extraDepartment.Id], unassigned.DepartmentIds);
        Assert.Equal(["CS", "Accounting"], unassigned.DepartmentNames);
        Assert.Equal(workflow.Name, unassigned.WorkflowDefinitionName);
        Assert.Null(unassigned.CurrentAssigneeName);
        Assert.Null(unassigned.ActiveReviewerName);
        Assert.Equal("New", unassigned.StateName);

        message.Assign(assignee.Id);
        var review = message.StartReview(1, reviewer.Id, workflow, [], DateTimeOffset.UtcNow,
            isGlobalAdministrator: true);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        var row = Assert.Single((await gridQueries.LoadAsync(new(), access, ct)).Items);
        Assert.Equal(assignee.Id, row.CurrentAssigneeId);
        Assert.Equal("Assigned user", row.CurrentAssigneeName);
        Assert.Equal(reviewer.Id, row.ActiveReviewerId);
        Assert.Equal("Reviewing user", row.ActiveReviewerName);
        Assert.Equal("First review in progress", row.StateName);
        var details = await queries.GetAsync(message.Id, access, ct);
        Assert.NotNull(details);
        Assert.Equal(row.BranchName, details.BranchName);
        Assert.Equal(row.DepartmentNames, details.DepartmentNames);
        Assert.Equal(row.CurrentAssigneeName, details.CurrentAssigneeName);
        Assert.Equal(row.StateName, details.StateName);
        var item = Assert.Single((await queries.SearchAsync(new(0, 20, null, null), access, ct)).Items);
        Assert.Equal(row.BranchName, item.BranchName);
        Assert.Equal(row.DepartmentNames, item.DepartmentNames);
        Assert.Equal(row.CurrentAssigneeName, item.CurrentAssigneeName);
        Assert.Equal(row.ActiveReviewerName, item.ActiveReviewerName);
        Assert.Equal(row.StateName, item.StateName);
    }
}
