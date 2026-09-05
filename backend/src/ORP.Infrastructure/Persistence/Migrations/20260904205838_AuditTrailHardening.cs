using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReviewId",
                schema: "ORP",
                table: "AuditEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ReviewId",
                schema: "ORP",
                table: "AuditEvents",
                column: "ReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEvents_Reviews_ReviewId",
                schema: "ORP",
                table: "AuditEvents",
                column: "ReviewId",
                principalSchema: "ORP",
                principalTable: "Reviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [ORP].[RegisterNewMessages]
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO [ORP].[Messages]
                        ([MessageId], [State], [CurrentAssigneeId], [WorkflowDefinitionId])
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
                        FROM [ORP].[Messages] AS existing
                        WHERE existing.[MessageId] = source.[MessageID]
                    );
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AuditEvents_Reviews_ReviewId",
                schema: "ORP",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ReviewId",
                schema: "ORP",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                schema: "ORP",
                table: "AuditEvents");
        }
    }
}
