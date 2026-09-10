using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Tests;

internal static class ReviewTestData
{
    // Existing scenario fixtures submit decisions for the latest attempt.
    // Stale-request regression tests capture and send their original start response explicitly.
    internal static async Task<long> LatestReviewIdAsync(this MessageApiFactory factory, long messageId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ORPDbContext>().Reviews
            .Where(r => r.MessageId == messageId).OrderByDescending(r => r.Id)
            .Select(r => r.Id).FirstOrDefaultAsync(Xunit.TestContext.Current.CancellationToken);
    }
}
