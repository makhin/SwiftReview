using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ORPDbContext))]
[Migration("20260905210000_ValidateWorkflowConfiguration")]
public sealed class ValidateWorkflowConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1
                FROM [ORP].[WorkflowDefinitions] AS workflow
                WHERE workflow.[IsActive] = 1
                  AND
                  (
                      NOT EXISTS
                      (
                          SELECT 1
                          FROM [ORP].[WorkflowSteps] AS requiredStep
                          WHERE requiredStep.[WorkflowDefinitionId] = workflow.[Id]
                            AND requiredStep.[Required] = 1
                            AND requiredStep.[ReviewLevel] = 1
                      )
                      OR EXISTS
                      (
                          SELECT 1
                          FROM [ORP].[WorkflowSteps] AS step
                          WHERE step.[WorkflowDefinitionId] = workflow.[Id]
                            AND step.[ReviewLevel] NOT BETWEEN 1 AND 3
                      )
                      OR EXISTS
                      (
                          SELECT step.[ReviewLevel]
                          FROM [ORP].[WorkflowSteps] AS step
                          WHERE step.[WorkflowDefinitionId] = workflow.[Id]
                          GROUP BY step.[ReviewLevel]
                          HAVING COUNT(*) > 1
                      )
                      OR EXISTS
                      (
                          SELECT 1
                          FROM [ORP].[WorkflowSteps] AS earlier
                          INNER JOIN [ORP].[WorkflowSteps] AS later
                              ON later.[WorkflowDefinitionId] = earlier.[WorkflowDefinitionId]
                             AND later.[Order] > earlier.[Order]
                          WHERE earlier.[WorkflowDefinitionId] = workflow.[Id]
                            AND earlier.[Required] = 1
                            AND later.[Required] = 1
                            AND later.[ReviewLevel] <= earlier.[ReviewLevel]
                      )
                  )
            )
                THROW 51000, 'Active workflow configuration is incompatible with the message state machine.', 1;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE [ORP].[RegisterNewMessages]
                @CorrelationId nvarchar(100) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @StartedTransaction bit = 0;
                DECLARE @RegisteredAt datetimeoffset = SYSUTCDATETIME();
                DECLARE @EffectiveCorrelationId nvarchar(100) = COALESCE(
                    NULLIF(LTRIM(RTRIM(@CorrelationId)), N''),
                    CONCAT(N'registration-', CONVERT(nvarchar(36), NEWID())));
                DECLARE @Registered TABLE
                (
                    [MessageId] bigint NOT NULL,
                    [State] nvarchar(40) NOT NULL,
                    [WorkflowDefinitionId] int NOT NULL
                );

                IF @@TRANCOUNT = 0
                BEGIN
                    BEGIN TRANSACTION;
                    SET @StartedTransaction = 1;
                END;

                BEGIN TRY
                    INSERT INTO [ORP].[Messages]
                        ([MessageId], [State], [CurrentAssigneeId], [WorkflowDefinitionId])
                    OUTPUT inserted.[MessageId], inserted.[State], inserted.[WorkflowDefinitionId]
                        INTO @Registered ([MessageId], [State], [WorkflowDefinitionId])
                    SELECT
                        source.[MessageID],
                        N'New',
                        NULL,
                        workflow.[Id]
                    FROM [ORP].[SwiftMessageSource] AS source
                    CROSS APPLY
                    (
                        SELECT TOP (1) candidate.[Id]
                        FROM [ORP].[WorkflowDefinitions] AS candidate
                        WHERE candidate.[IsActive] = 1
                          AND candidate.[MessageType] = source.[MessageType]
                          AND candidate.[DepartmentId] = source.[DepartmentId]
                          AND (candidate.[BranchId] = source.[BranchId] OR candidate.[BranchId] IS NULL)
                          AND EXISTS
                          (
                              SELECT 1
                              FROM [ORP].[WorkflowSteps] AS requiredStep
                              WHERE requiredStep.[WorkflowDefinitionId] = candidate.[Id]
                                AND requiredStep.[Required] = 1
                                AND requiredStep.[ReviewLevel] = 1
                          )
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM [ORP].[WorkflowSteps] AS step
                              WHERE step.[WorkflowDefinitionId] = candidate.[Id]
                                AND step.[ReviewLevel] NOT BETWEEN 1 AND 3
                          )
                          AND NOT EXISTS
                          (
                              SELECT step.[ReviewLevel]
                              FROM [ORP].[WorkflowSteps] AS step
                              WHERE step.[WorkflowDefinitionId] = candidate.[Id]
                              GROUP BY step.[ReviewLevel]
                              HAVING COUNT(*) > 1
                          )
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM [ORP].[WorkflowSteps] AS earlier
                              INNER JOIN [ORP].[WorkflowSteps] AS later
                                  ON later.[WorkflowDefinitionId] = earlier.[WorkflowDefinitionId]
                                 AND later.[Order] > earlier.[Order]
                              WHERE earlier.[WorkflowDefinitionId] = candidate.[Id]
                                AND earlier.[Required] = 1
                                AND later.[Required] = 1
                                AND later.[ReviewLevel] <= earlier.[ReviewLevel]
                          )
                        ORDER BY
                            CASE WHEN candidate.[BranchId] = source.[BranchId] THEN 0 ELSE 1 END,
                            candidate.[Id]
                    ) AS workflow
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM [ORP].[Messages] AS existing WITH (UPDLOCK, HOLDLOCK)
                        WHERE existing.[MessageId] = source.[MessageID]
                    );

                    INSERT INTO [ORP].[AuditEvents]
                        ([MessageId], [EventType], [UserId], [Timestamp], [OldState], [NewState],
                         [DetailsJson], [CorrelationId], [ReviewId])
                    SELECT
                        registered.[MessageId],
                        N'MessageRegistered',
                        NULL,
                        @RegisteredAt,
                        NULL,
                        registered.[State],
                        CONCAT(N'{"workflowDefinitionId":', registered.[WorkflowDefinitionId], N'}'),
                        @EffectiveCorrelationId,
                        NULL
                    FROM @Registered AS registered;

                    IF @StartedTransaction = 1 COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF @StartedTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE [ORP].[RegisterNewMessages]
                @CorrelationId nvarchar(100) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @StartedTransaction bit = 0;
                DECLARE @RegisteredAt datetimeoffset = SYSUTCDATETIME();
                DECLARE @EffectiveCorrelationId nvarchar(100) = COALESCE(
                    NULLIF(LTRIM(RTRIM(@CorrelationId)), N''),
                    CONCAT(N'registration-', CONVERT(nvarchar(36), NEWID())));
                DECLARE @Registered TABLE
                (
                    [MessageId] bigint NOT NULL,
                    [State] nvarchar(40) NOT NULL,
                    [WorkflowDefinitionId] int NOT NULL
                );

                IF @@TRANCOUNT = 0
                BEGIN
                    BEGIN TRANSACTION;
                    SET @StartedTransaction = 1;
                END;

                BEGIN TRY
                    INSERT INTO [ORP].[Messages]
                        ([MessageId], [State], [CurrentAssigneeId], [WorkflowDefinitionId])
                    OUTPUT inserted.[MessageId], inserted.[State], inserted.[WorkflowDefinitionId]
                        INTO @Registered ([MessageId], [State], [WorkflowDefinitionId])
                    SELECT
                        source.[MessageID],
                        N'New',
                        NULL,
                        workflow.[Id]
                    FROM [ORP].[SwiftMessageSource] AS source
                    CROSS APPLY
                    (
                        SELECT TOP (1) candidate.[Id]
                        FROM [ORP].[WorkflowDefinitions] AS candidate
                        WHERE candidate.[IsActive] = 1
                          AND candidate.[MessageType] = source.[MessageType]
                          AND candidate.[DepartmentId] = source.[DepartmentId]
                          AND (candidate.[BranchId] = source.[BranchId] OR candidate.[BranchId] IS NULL)
                        ORDER BY
                            CASE WHEN candidate.[BranchId] = source.[BranchId] THEN 0 ELSE 1 END,
                            candidate.[Id]
                    ) AS workflow
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM [ORP].[Messages] AS existing WITH (UPDLOCK, HOLDLOCK)
                        WHERE existing.[MessageId] = source.[MessageID]
                    );

                    INSERT INTO [ORP].[AuditEvents]
                        ([MessageId], [EventType], [UserId], [Timestamp], [OldState], [NewState],
                         [DetailsJson], [CorrelationId], [ReviewId])
                    SELECT
                        registered.[MessageId],
                        N'MessageRegistered',
                        NULL,
                        @RegisteredAt,
                        NULL,
                        registered.[State],
                        CONCAT(N'{"workflowDefinitionId":', registered.[WorkflowDefinitionId], N'}'),
                        @EffectiveCorrelationId,
                        NULL
                    FROM @Registered AS registered;

                    IF @StartedTransaction = 1 COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF @StartedTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
            END;
            """);
    }
}
