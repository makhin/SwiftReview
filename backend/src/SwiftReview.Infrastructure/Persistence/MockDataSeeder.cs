using Bogus;
using Microsoft.EntityFrameworkCore;
using SwiftReview.Domain.Messages;

namespace SwiftReview.Infrastructure.Persistence;

public static class MockDataSeeder
{
    public static async Task SeedAsync(SwiftReviewDbContext db, CancellationToken ct = default)
    {
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
        var messages = await db.Messages.OrderBy(x => x.Id).ToListAsync(ct);
        var rawData = await db.MessageRawData.ToDictionaryAsync(x => x.MessageId, ct);

        foreach (var message in messages)
        {
            var fake = faker.Generate();
            var externalId = $"MSG-{message.Id:00000}-{fake.IdSuffix}";

            var entry = db.Entry(message);
            entry.Property(nameof(Message.ExternalId)).CurrentValue = externalId;
            entry.Property(nameof(Message.Sender)).CurrentValue = fake.Sender;
            entry.Property(nameof(Message.Receiver)).CurrentValue = fake.Receiver;
            entry.Property(nameof(Message.Account)).CurrentValue = fake.Account;
            entry.Property(nameof(Message.Currency)).CurrentValue = fake.Currency;
            entry.Property(nameof(Message.Amount)).CurrentValue = fake.Amount;
            entry.Property(nameof(Message.Reference)).CurrentValue = fake.Reference;

            if (rawData.TryGetValue(message.Id, out var raw))
            {
                db.Entry(raw).Property(nameof(MessageRawData.RawContent)).CurrentValue =
                    $"{{1:F01{fake.Sender}}}{{2:I{message.MessageType[2..]}{fake.Receiver}}}{{4::20:{fake.Reference}:21:{externalId}-}}";
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Bic(Faker faker) =>
        $"{faker.Random.String2(4, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}GB{faker.Random.Number(10, 99)}{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}";

    private sealed record MockMessageFields(string IdSuffix, string Sender, string Receiver, string Account,
        string Currency, decimal Amount, string Reference);
}
