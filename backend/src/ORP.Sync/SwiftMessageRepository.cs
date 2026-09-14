using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ORP.Sync;

internal sealed class SwiftMessageRepository
{
    private const string WatermarkName = "SwiftMessages";
    private readonly string _connectionString;
    private readonly int _commandTimeout;
    private readonly ILogger<SwiftMessageRepository> _logger;

    public SwiftMessageRepository(string connectionString, ILogger<SwiftMessageRepository> logger = null)
    {
        _connectionString = connectionString;
        _logger = logger ?? NullLogger<SwiftMessageRepository>.Instance;
        _commandTimeout = ReadPositiveInt("CommandTimeoutSeconds", 300);
    }

    public DateTime GetFromUtc(DateTime nowUtc)
    {
        using var connection = Open();
        using var command = Command(connection,
            "SELECT [LastSuccessfulToUtc] FROM [orp].[SyncState] WHERE [Name] = @Name;");
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = WatermarkName;
        var value = command.ExecuteScalar();
        if (value == null || value == DBNull.Value)
            return nowUtc.AddHours(-ReadPositiveInt("InitialLookbackHours", 24));
        var watermark = value is DateTimeOffset offset ? offset.UtcDateTime : Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToUniversalTime();
        return watermark.AddMinutes(-ReadPositiveInt("OverlapMinutes", 5));
    }

    public void ValidateRoutingReferences(RoutingRules rules)
    {
        using var connection = Open();
        ValidateIds(connection, "Branches", rules.BranchIds);
        ValidateIds(connection, "Departments", rules.DepartmentIds);
    }

    public SyncResult Save(IReadOnlyList<SwiftMessageData> messages, RoutingRules rules, DateTime toUtc,
        string correlationId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        AcquireLock(connection, transaction);
        var result = new SyncResult();
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.WarehouseId) || message.WarehouseId.Length > 30)
            {
                result.Skipped++;
                SyncLog.InvalidWarehouseId(_logger);
                continue;
            }

            if (Exists(connection, transaction, message.WarehouseId))
            {
                result.Skipped++;
                continue;
            }

            var distinctLengths = message.CollectionLengths.Distinct().ToArray();
            if (distinctLengths.Length > 1)
                SyncLog.CollectionLengthMismatch(_logger, message.WarehouseId,
                    string.Join(",", distinctLengths));

            var route = rules.Resolve(message);
            var routingStatus = route.IsRouted ? "Routed" : "Unroutable";
            var messageId = InsertMessage(connection, transaction, message, route, routingStatus, toUtc);
            InsertEntries(connection, transaction, messageId, message);
            result.Inserted++;
            if (routingStatus != "Routed")
            {
                result.RoutingIssues++;
                SyncLog.MessageUnroutable(_logger, message.WarehouseId, route.Error);
            }
        }

        RegisterEligibleMessages(connection, transaction, correlationId);
        SaveWatermark(connection, transaction, toUtc);
        transaction.Commit();
        return result;
    }

    private long InsertMessage(SqlConnection connection, SqlTransaction transaction, SwiftMessageData message,
        RouteResult route, string routingStatus, DateTime synchronizedAtUtc)
    {
        using var command = Command(connection,
            """
            INSERT INTO [orp].[SwiftMessages]
            ([WarehouseId],[BodyContainsMx],[BodyContainsMt],[Json],[Body],[BackendDirection],
             [CounterParty],[CounterPartyCountry],[CreationDate],[Direction],[LastModificationDate],[MessageDate],
             [MessageLength],[MessageFormatVersion],[MessageInputReference],[MessageType],[MessageTypeShort],
             [ModifiedBy],[ReceiverResponder],[SenderRequestor],[SequenceNumber],[SessionNumber],[Service],
             [SourceInterface],[Status],[StatusDate],[BranchId],[DepartmentId],[RoutingStatus],[RoutingError],
             [LastSynchronizedAtUtc])
            VALUES
            (@WarehouseId,@BodyContainsMx,@BodyContainsMt,@Json,@Body,@BackendDirection,
             @CounterParty,@CounterPartyCountry,@CreationDate,@Direction,@LastModificationDate,@MessageDate,
             @MessageLength,@MessageFormatVersion,@MessageInputReference,@MessageType,@MessageTypeShort,
             @ModifiedBy,@ReceiverResponder,@SenderRequestor,@SequenceNumber,@SessionNumber,@Service,
             @SourceInterface,@Status,@StatusDate,@BranchId,@DepartmentId,@RoutingStatus,@RoutingError,@Now);
            SELECT CAST(SCOPE_IDENTITY() AS bigint);
            """, transaction);
        Add(command, "@WarehouseId", Clip(message.WarehouseId, 30));
        Add(command, "@BodyContainsMx", ContainsMx(message.Body));
        Add(command, "@BodyContainsMt", ContainsMt(message.Body));
        Add(command, "@Json", message.Json);
        Add(command, "@Body", message.Body);
        Add(command, "@BackendDirection", Clip(message.BackendDirection, 100));
        Add(command, "@CounterParty", Clip(message.CounterParty, 100));
        Add(command, "@CounterPartyCountry", Clip(message.CounterPartyCountry, 100));
        Add(command, "@CreationDate", Utc(message.CreationDate));
        Add(command, "@Direction", Clip(message.Direction, 8));
        Add(command, "@LastModificationDate", Utc(message.LastModificationDate));
        Add(command, "@MessageDate", Utc(message.MessageDate));
        Add(command, "@MessageLength", message.MessageLength);
        Add(command, "@MessageFormatVersion", Clip(message.MessageFormatVersion, 20));
        Add(command, "@MessageInputReference", Clip(message.MessageInputReference, 100));
        Add(command, "@MessageType", Clip(message.MessageType ?? string.Empty, 20));
        Add(command, "@MessageTypeShort", Clip(message.MessageTypeShort, 10));
        Add(command, "@ModifiedBy", Clip(message.ModifiedBy, 20));
        Add(command, "@ReceiverResponder", Clip(message.ReceiverResponder, 100));
        Add(command, "@SenderRequestor", Clip(message.SenderRequestor, 100));
        Add(command, "@SequenceNumber", Clip(message.SequenceNumber, 20));
        Add(command, "@SessionNumber", Clip(message.SessionNumber, 20));
        Add(command, "@Service", Clip(message.Service, 20));
        Add(command, "@SourceInterface", Clip(message.SourceInterface, 20));
        Add(command, "@Status", Clip(message.Status, 20));
        Add(command, "@StatusDate", Utc(message.StatusDate));
        Add(command, "@BranchId", route.BranchId);
        Add(command, "@DepartmentId", route.DepartmentId);
        Add(command, "@RoutingStatus", routingStatus);
        Add(command, "@RoutingError", Clip(route.Error, 1000));
        Add(command, "@Now", new DateTimeOffset(synchronizedAtUtc, TimeSpan.Zero));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private void InsertEntries(SqlConnection connection, SqlTransaction transaction, long messageId,
        SwiftMessageData message)
    {
        for (var position = 0; position < message.EntryCount; position++)
        {
            using var insert = Command(connection,
                """
                INSERT INTO [orp].[SwiftMessageEntries]
                ([MessageId],[Position],[Account],[Currency],[Amount],[BeneficiaryCustomerAccount],
                 [BeneficiaryCustomerBank],[BeneficiaryCustomerName],[OrderingCustomerAccount],
                 [OrderingCustomerBank],[OrderingCustomerName],[SenderMessageReference],[SettlementDate],
                 [TradeDealDate],[UnitDataOwner],[ValueDate])
                VALUES
                (@MessageId,@Position,@Account,@Currency,@Amount,@BeneficiaryCustomerAccount,
                 @BeneficiaryCustomerBank,@BeneficiaryCustomerName,@OrderingCustomerAccount,
                 @OrderingCustomerBank,@OrderingCustomerName,@SenderMessageReference,@SettlementDate,
                 @TradeDealDate,@UnitDataOwner,@ValueDate);
                """, transaction);
            Add(insert, "@MessageId", messageId);
            Add(insert, "@Position", position);
            Add(insert, "@Account", Clip(At(message.Accounts, position), 100));
            Add(insert, "@Currency", Clip(At(message.Currencies, position), 3));
            Add(insert, "@Amount", At(message.Amounts, position));
            Add(insert, "@BeneficiaryCustomerAccount", Clip(At(message.BeneficiaryCustomerAccounts, position), 255));
            Add(insert, "@BeneficiaryCustomerBank", Clip(At(message.BeneficiaryCustomerBanks, position), 255));
            Add(insert, "@BeneficiaryCustomerName", Clip(At(message.BeneficiaryCustomerNames, position), 255));
            Add(insert, "@OrderingCustomerAccount", Clip(At(message.OrderingCustomerAccounts, position), 255));
            Add(insert, "@OrderingCustomerBank", Clip(At(message.OrderingCustomerBanks, position), 255));
            Add(insert, "@OrderingCustomerName", Clip(At(message.OrderingCustomerNames, position), 255));
            Add(insert, "@SenderMessageReference", Clip(At(message.SenderMessageReferences, position), 255));
            Add(insert, "@SettlementDate", At(message.SettlementDates, position));
            Add(insert, "@TradeDealDate", At(message.TradeDealDates, position));
            Add(insert, "@UnitDataOwner", Clip(At(message.UnitDataOwners, position), 255));
            Add(insert, "@ValueDate", At(message.ValueDates, position));
            insert.ExecuteNonQuery();
        }
    }

    private bool Exists(SqlConnection connection, SqlTransaction transaction, string warehouseId)
    {
        using var command = Command(connection,
            """
            SELECT TOP (1) 1
            FROM [orp].[SwiftMessages] WITH (UPDLOCK, HOLDLOCK)
            WHERE [WarehouseId]=@WarehouseId;
            """, transaction);
        Add(command, "@WarehouseId", warehouseId);
        return command.ExecuteScalar() != null;
    }

    private void RegisterEligibleMessages(SqlConnection connection, SqlTransaction transaction, string correlationId)
    {
        var candidates = LoadRegistrationCandidates(connection, transaction);
        if (candidates.Count == 0) return;

        var workflows = LoadRegistrationWorkflows(connection, transaction);
        var registeredAt = DateTimeOffset.UtcNow;
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? $"registration-{Guid.NewGuid()}"
            : Clip(correlationId, 100);

        foreach (var candidate in candidates)
        {
            var workflow = MessageRegistration.Resolve(candidate, workflows);
            if (workflow == null) continue;

            using var command = Command(connection,
                """
                INSERT INTO [orp].[Messages]
                    ([MessageId],[State],[CurrentAssigneeId],[WorkflowDefinitionId])
                VALUES (@MessageId,N'New',NULL,@WorkflowDefinitionId);

                INSERT INTO [orp].[AuditEvents]
                    ([MessageId],[EventType],[UserId],[Timestamp],[OldState],[NewState],
                     [DetailsJson],[CorrelationId],[ReviewId])
                VALUES
                    (@MessageId,N'MessageRegistered',NULL,@RegisteredAt,NULL,N'New',
                     @DetailsJson,@CorrelationId,NULL);
                """, transaction);
            Add(command, "@MessageId", candidate.MessageId);
            Add(command, "@WorkflowDefinitionId", workflow.Id);
            Add(command, "@RegisteredAt", registeredAt);
            Add(command, "@DetailsJson", $"{{\"workflowDefinitionId\":{workflow.Id.ToString(CultureInfo.InvariantCulture)}}}");
            Add(command, "@CorrelationId", effectiveCorrelationId);
            command.ExecuteNonQuery();
        }
    }

    private List<RegistrationCandidate> LoadRegistrationCandidates(
        SqlConnection connection, SqlTransaction transaction)
    {
        using var command = Command(connection,
            """
            SELECT source.[MessageId],source.[MessageType],source.[BranchId],source.[DepartmentId]
            FROM [orp].[SwiftMessages] AS source
            WHERE source.[RoutingStatus]=N'Routed'
              AND source.[BranchId] IS NOT NULL
              AND source.[DepartmentId] IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1 FROM [orp].[Messages] AS existing WITH (UPDLOCK,HOLDLOCK)
                  WHERE existing.[MessageId]=source.[MessageId]
              );
            """, transaction);
        using var reader = command.ExecuteReader();
        var candidates = new List<RegistrationCandidate>();
        while (reader.Read())
        {
            candidates.Add(new RegistrationCandidate(
                reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return candidates;
    }

    private List<RegistrationWorkflow> LoadRegistrationWorkflows(
        SqlConnection connection, SqlTransaction transaction)
    {
        using var command = Command(connection,
            """
            SELECT workflow.[Id],workflow.[MessageType],workflow.[DepartmentId],workflow.[BranchId],
                   step.[Order],step.[ReviewLevel],step.[Required]
            FROM [orp].[WorkflowDefinitions] AS workflow
            LEFT JOIN [orp].[WorkflowSteps] AS step
                ON step.[WorkflowDefinitionId]=workflow.[Id]
            WHERE workflow.[IsActive]=1
            ORDER BY workflow.[Id],step.[Order];
            """, transaction);
        using var reader = command.ExecuteReader();
        var workflowsById = new Dictionary<int, RegistrationWorkflow>();
        while (reader.Read())
        {
            var workflowId = reader.GetInt32(0);
            if (!workflowsById.TryGetValue(workflowId, out var workflow))
            {
                workflow = new RegistrationWorkflow(
                    workflowId,
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3));
                workflowsById.Add(workflowId, workflow);
            }

            if (!reader.IsDBNull(4))
            {
                workflow.Steps.Add(new RegistrationStep(
                    reader.GetInt32(4), reader.GetInt32(5), reader.GetBoolean(6)));
            }
        }

        return workflowsById.Values.ToList();
    }

    private void SaveWatermark(SqlConnection connection, SqlTransaction transaction, DateTime toUtc)
    {
        using var command = Command(connection,
            """
            UPDATE [orp].[SyncState] SET [LastSuccessfulToUtc]=@Value,[UpdatedAtUtc]=SYSUTCDATETIME() WHERE [Name]=@Name;
            IF @@ROWCOUNT=0 INSERT INTO [orp].[SyncState] ([Name],[LastSuccessfulToUtc],[UpdatedAtUtc]) VALUES (@Name,@Value,SYSUTCDATETIME());
            """, transaction);
        Add(command, "@Name", WatermarkName);
        Add(command, "@Value", new DateTimeOffset(toUtc, TimeSpan.Zero));
        command.ExecuteNonQuery();
    }

    private void AcquireLock(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = Command(connection,
            "DECLARE @result int; EXEC @result=sp_getapplock @Resource=N'orp-swift-sync',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=0; IF @result<0 THROW 51001,'Another ORP.Sync instance is active.',1;",
            transaction);
        command.ExecuteNonQuery();
    }

    private void ValidateIds(SqlConnection connection, string table, IReadOnlyCollection<int> ids)
    {
        foreach (var id in ids)
        {
            using var command = Command(connection, $"SELECT COUNT(*) FROM [orp].[{table}] WHERE [Id]=@Id;");
            Add(command, "@Id", id);
            if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                throw new ConfigurationErrorsException($"Routing rule refers to missing [orp].[{table}] Id {id}.");
        }
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private SqlCommand Command(SqlConnection connection, string sql, SqlTransaction transaction = null) =>
        new SqlCommand(sql, connection, transaction) { CommandTimeout = _commandTimeout };

    private static void Add(SqlCommand command, string name, object value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static T At<T>(IReadOnlyList<T> values, int position) => position < values.Count ? values[position] : default;
    private static string Clip(string value, int length) => value == null || value.Length <= length ? value : value.Substring(0, length);
    private static DateTimeOffset? Utc(DateTime? value) => value.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : (DateTimeOffset?)null;
    private static bool ContainsMt(string body) => !string.IsNullOrEmpty(body) && body.IndexOf("{1:", StringComparison.Ordinal) >= 0;
    private static bool ContainsMx(string body) => !string.IsNullOrEmpty(body) &&
        (body.IndexOf("<Document", StringComparison.OrdinalIgnoreCase) >= 0 ||
         body.IndexOf("iso:std:iso:20022", StringComparison.OrdinalIgnoreCase) >= 0);
    private static int ReadPositiveInt(string key, int fallback) =>
        int.TryParse(ConfigurationManager.AppSettings[key], out var value) && value > 0 ? value : fallback;
}

internal sealed class SyncResult
{
    public int Inserted { get; set; }
    public int Skipped { get; set; }
    public int RoutingIssues { get; set; }
}
