using Bogus;
using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence.Configurations;

namespace ORP.Infrastructure.Persistence;

public static class MockDataSeeder
{
    public static async Task SeedAsync(ORPDbContext db, CancellationToken ct = default)
    {
        if (await db.SwiftMessageSource.AnyAsync(ct)) return;

        var faker = new Faker<MockMessageFields>("en_GB")
            .CustomInstantiator(f => new MockMessageFields(
                f.Random.AlphaNumeric(5).ToUpperInvariant(),
                Bic(f),
                Bic(f),
                f.Finance.Account(12),
                f.PickRandom("EUR", "GBP", "USD", "CHF", "JPY"),
                f.Finance.Amount(500, 5_000_000, 2),
                $"PAY-{f.Random.Number(10_000_000, 99_999_999)}-{f.Random.AlphaNumeric(4).ToUpperInvariant()}"))
            .UseSeed(20260902);
        var messages = Enumerable.Range(1, 75).Select(i => new Domain.Messages.Message(i,
            (i - 1) % SeedConfiguration.MessageTypes.Length + 1)).ToList();
        var source = messages.Select(message =>
        {
            var fake = faker.Generate();
            var typeIndex = ((int)message.Id - 1) % SeedConfiguration.MessageTypes.Length;
            return new SwiftMessageRecord
            {
                MessageId = message.Id,
                ExternalId = $"MSG-{message.Id:00000}-{fake.IdSuffix}",
                MessageType = SeedConfiguration.MessageTypes[typeIndex],
                BranchId = ((int)message.Id - 1) % 3 + 1,
                DepartmentId = typeIndex % 3 + 1,
                ReceivedAt = SeedConfiguration.SeedReceivedAt((int)message.Id),
                Sender = fake.Sender,
                Receiver = fake.Receiver,
                Account = fake.Account,
                Currency = fake.Currency,
                Amount = fake.Amount,
                Reference = fake.Reference
            };
        });
        db.Messages.AddRange(messages);
        db.SwiftMessageSource.AddRange(source);

        await db.SaveChangesAsync(ct);
    }

    private static string Bic(Faker faker) =>
        $"{faker.Random.String2(4, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}GB{faker.Random.Number(10, 99)}{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}";

    private sealed record MockMessageFields(string IdSuffix, string Sender, string Receiver, string Account,
        string Currency, decimal Amount, string Reference);
}
