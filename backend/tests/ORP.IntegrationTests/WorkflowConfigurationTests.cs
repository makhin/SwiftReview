using Microsoft.EntityFrameworkCore;
using ORP.Domain.Common;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class WorkflowConfigurationTests
{
    [Fact]
    public async Task InvalidActiveWorkflow_IsRejectedWhenSaved()
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseInMemoryDatabase($"invalid-workflow-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ORPDbContext(options);
        db.WorkflowDefinitions.Add(new WorkflowDefinition("Invalid", "MT199", 1)
            .AddStep(1, 1, false)
            .AddStep(2, 2));

        await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
