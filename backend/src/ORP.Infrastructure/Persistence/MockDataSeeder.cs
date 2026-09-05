using Bogus;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public static class MockDataSeeder
{
    private static readonly string[] MessageTypes = ["MT199", "MT299", "MT671", "MT700", "MT710", "MT760", "MT799", "MT999"];

    public static async Task SeedAsync(ORPDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsInMemory())
            throw new InvalidOperationException("Mock data can only be seeded into an in-memory database.");
        if (await db.Users.AnyAsync(ct) || await db.Messages.AnyAsync(ct)) return;

        SeedReferenceData(db);

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
            (i - 1) % MessageTypes.Length + 1)).ToList();
        var source = messages.Select(message =>
        {
            var fake = faker.Generate();
            var typeIndex = ((int)message.Id - 1) % MessageTypes.Length;
            return new SwiftMessageRecord
            {
                MessageId = message.Id,
                ExternalId = $"MSG-{message.Id:00000}-{fake.IdSuffix}",
                MessageType = MessageTypes[typeIndex],
                BranchId = ((int)message.Id - 1) % 3 + 1,
                DepartmentId = typeIndex % 3 + 1,
                ReceivedAt = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero).AddHours(message.Id),
                Sender = fake.Sender,
                Receiver = fake.Receiver,
                Account = fake.Account,
                Currency = fake.Currency,
                Amount = fake.Amount,
                Reference = fake.Reference
            };
        }).ToList();
        db.Messages.AddRange(messages);
        db.SwiftMessageSource.AddRange(source);
        var registeredAt = DateTimeOffset.UtcNow;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        db.AuditEvents.AddRange(messages.Select(message => new AuditEvent(message,
            AuditEventType.MessageRegistered, null, registeredAt, null, MessageState.New,
            JsonSerializer.Serialize(new AuditEventDetailsDto(WorkflowDefinitionId: message.WorkflowDefinitionId), jsonOptions),
            "mock-seed")));

        await db.SaveChangesAsync(ct);
    }

    private static void SeedReferenceData(ORPDbContext db)
    {
        AddWithId(db, new Branch("London"), 1);
        AddWithId(db, new Branch("Dublin"), 2);
        AddWithId(db, new Branch("Singapore"), 3);
        AddWithId(db, new Department("CS"), 1);
        AddWithId(db, new Department("TFO"), 2);
        AddWithId(db, new Department("DC"), 3);

        var permissionIds = Permissions.All.Select((name, index) => (name, id: index + 1))
            .ToDictionary(x => x.name, x => x.id);
        foreach (var (name, id) in permissionIds) AddWithId(db, new Permission(name), id);

        AddWithId(db, new Role("CS Reviewer"), 1);
        AddWithId(db, new Role("TFO Reviewer"), 2);
        AddWithId(db, new Role("DC Reviewer"), 3);
        AddWithId(db, new Role("DC Senior Reviewer"), 4);
        AddWithId(db, new Role("Administrator"), 5);

        (int Id, string UserName, string DisplayName, int RoleId)[] users =
        [
            (1, "amelia.hart", "Amelia Hart", 1),
            (2, "theo.mercer", "Theo Mercer", 2),
            (3, "priya.nair", "Priya Nair", 3),
            (4, "victor.stone", "Victor Stone", 4),
            (5, "admin", "Administrator", 5),
            (6, "lucas.bennett", "Lucas Bennett", 1),
            (7, "sofia.lindberg", "Sofia Lindberg", 1),
            (8, "kenji.mori", "Kenji Mori", 2),
            (9, "nadia.kowalska", "Nadia Kowalska", 2),
            (10, "mateo.silva", "Mateo Silva", 3),
            (11, "elena.petrova", "Elena Petrova", 3)
        ];
        foreach (var user in users)
        {
            AddWithId(db, new User(user.UserName, user.DisplayName), user.Id);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = user.RoleId });
        }

        var grants = new List<RolePermission>();
        Grant(1, Permissions.MessageView, Permissions.ReviewLevel1);
        Grant(2, Permissions.MessageView, Permissions.ReviewLevel1, Permissions.ReviewLevel2);
        Grant(3, Permissions.MessageView, Permissions.ReviewLevel1);
        Grant(4, Permissions.MessageView, Permissions.ReviewLevel2, Permissions.ReviewLevel3, Permissions.ReviewReject, Permissions.ReviewUndo);
        Grant(5, Permissions.All);
        db.RolePermissions.AddRange(grants);

        db.UserBranches.AddRange(new[]
        {
            LinkBranches(1, 1), LinkBranches(2, 2), LinkBranches(3, 3), LinkBranches(4, 1, 2, 3),
            LinkBranches(5, 1, 2, 3), LinkBranches(6, 2), LinkBranches(7, 3),
            LinkBranches(8, 1), LinkBranches(9, 3), LinkBranches(10, 1), LinkBranches(11, 2)
        }.SelectMany(x => x));
        db.UserDepartments.AddRange(new[]
        {
            LinkDepartments(1, 1), LinkDepartments(2, 2), LinkDepartments(3, 3), LinkDepartments(4, 3),
            LinkDepartments(5, 2), LinkDepartments(6, 1), LinkDepartments(7, 1),
            LinkDepartments(8, 2), LinkDepartments(9, 2), LinkDepartments(10, 3), LinkDepartments(11, 3)
        }.SelectMany(x => x));

        string[] workflowNames = ["Single Review", "Two Reviews", "Three Reviews", "MT700 Single Review",
            "MT710 Two Reviews", "MT760 Three Reviews", "MT799 Single Review", "MT999 Two Reviews"];
        for (var workflowId = 1; workflowId <= MessageTypes.Length; workflowId++)
        {
            var workflow = new WorkflowDefinition(workflowNames[workflowId - 1], MessageTypes[workflowId - 1],
                (workflowId - 1) % 3 + 1);
            var levelCount = (workflowId % 3) switch { 1 => 1, 2 => 2, _ => 3 };
            for (var level = 1; level <= levelCount; level++) workflow.AddStep(level, level);
            AddWithId(db, workflow, workflowId);
        }

        void Grant(int roleId, params string[] names)
        {
            grants.AddRange(names.Select(name => new RolePermission { RoleId = roleId, PermissionId = permissionIds[name] }));
        }
    }

    private static UserBranch[] LinkBranches(int userId, params int[] branchIds) =>
        branchIds.Select(branchId => new UserBranch { UserId = userId, BranchId = branchId }).ToArray();

    private static UserDepartment[] LinkDepartments(int userId, params int[] departmentIds) =>
        departmentIds.Select(departmentId => new UserDepartment { UserId = userId, DepartmentId = departmentId }).ToArray();

    private static T AddWithId<T>(ORPDbContext db, T entity, int id) where T : class
    {
        db.Add(entity);
        db.Entry(entity).Property("Id").CurrentValue = id;
        return entity;
    }

    private static string Bic(Faker faker) =>
        $"{faker.Random.String2(4, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}GB{faker.Random.Number(10, 99)}{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}";

    private sealed record MockMessageFields(string IdSuffix, string Sender, string Receiver, string Account,
        string Currency, decimal Amount, string Reference);
}
