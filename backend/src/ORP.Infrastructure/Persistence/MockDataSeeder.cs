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
                Bic(f)))
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
                WarehouseId = $"MSG-{message.Id:00000}-{fake.IdSuffix}",
                MessageType = MessageTypes[typeIndex],
                BranchId = ((int)message.Id - 1) % 3 + 1,
                DepartmentId = typeIndex % 3 + 1,
                MessageDate = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero).AddHours(message.Id),
                SenderRequestor = fake.Sender,
                ReceiverResponder = fake.Receiver,
                Body = $"{{1:F01MOCK{message.Id:0000000000}}}\n{{2:I{MessageTypes[typeIndex][2..]}MOCK}}",
                RoutingStatus = SwiftMessageRoutingStatus.Routed,
                LastSynchronizedAtUtc = DateTimeOffset.UtcNow
            };
        }).ToList();
        db.Messages.AddRange(messages);
        db.SwiftMessages.AddRange(source);
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
        AddWithId(db, new Role("Operations manager"), 5);

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
            AddWithId(db, new User(user.UserName, user.DisplayName, user.Id == 5), user.Id);
        }

        var grants = new List<RolePermission>();
        Grant(1, Permissions.MessageView, Permissions.ReviewLevel1);
        Grant(2, Permissions.MessageView, Permissions.ReviewLevel1, Permissions.ReviewLevel2);
        Grant(3, Permissions.MessageView, Permissions.ReviewLevel1);
        Grant(4, Permissions.MessageView, Permissions.ReviewLevel2, Permissions.ReviewLevel3, Permissions.ReviewUndo);
        Grant(5, Permissions.All);
        db.RolePermissions.AddRange(grants);

        foreach (var user in users)
        {
            var branches = user.Id switch
            {
                4 or 5 => new[] { 1, 2, 3 },
                1 or 8 or 10 => [1],
                2 or 6 or 11 => [2],
                _ => [3]
            };
            var departments = user.Id == 5 ? new[] { 1, 2, 3 } :
                new[] { user.RoleId == 4 ? 3 : user.RoleId };
            foreach (var branchId in branches)
                foreach (var departmentId in departments)
                    db.UserRoles.Add(new UserRole { UserId = user.Id, BranchId = branchId,
                        DepartmentId = departmentId, RoleId = user.RoleId });
        }

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

    private static T AddWithId<T>(ORPDbContext db, T entity, int id) where T : class
    {
        db.Add(entity);
        db.Entry(entity).Property("Id").CurrentValue = id;
        return entity;
    }

    private static string Bic(Faker faker) =>
        $"{faker.Random.String2(4, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}GB{faker.Random.Number(10, 99)}{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}";

    private sealed record MockMessageFields(string IdSuffix, string Sender, string Receiver);
}
