-- Run manually in the intended application database after stopping API/importer writers.
-- IDs are orp.Messages.MessageId / orp.SwiftMessages.MessageId; BOTH bounds are inclusive.
-- Set the bounds and review with @DryRun = 1, then set @DryRun = 0 to delete.
-- Preserves users, roles, branches, departments, routing, workflows and physical files.
-- Removing the import record allows the original file to be imported again.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @FromId bigint = NULL;
DECLARE @ToId bigint = NULL;
DECLARE @DryRun bit = 1;

IF @FromId IS NULL OR @ToId IS NULL OR @FromId <= 0 OR @ToId < @FromId
    THROW 51000, 'Set a valid inclusive range: 0 < @FromId <= @ToId.', 1;
IF @DryRun IS NULL
    THROW 51000, '@DryRun must be 0 or 1.', 1;
IF @@TRANCOUNT <> 0
    THROW 51000, 'Run this script outside an existing transaction.', 1;

DECLARE @Targets TABLE
(
    MessageId bigint PRIMARY KEY,
    FilePath nvarchar(450) COLLATE Latin1_General_100_CI_AS NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO @Targets (MessageId, FilePath)
    SELECT MessageId, FilePath
    FROM orp.SwiftMessages WITH (XLOCK, HOLDLOCK)
    WHERE MessageId BETWEEN @FromId AND @ToId;

    -- Lock parent messages before inspecting/deleting their dependent records.
    SELECT DB_NAME() AS DatabaseName, s.MessageId, s.ReferenceId, s.FilePath, m.State
    FROM @Targets t
    JOIN orp.SwiftMessages s ON s.MessageId = t.MessageId
    LEFT JOIN orp.Messages m WITH (XLOCK, HOLDLOCK) ON m.MessageId = t.MessageId
    ORDER BY s.MessageId;

    SELECT 'AuditEvents' AS TableName, COUNT_BIG(*) AS MatchingRows
    FROM orp.AuditEvents a JOIN @Targets t ON t.MessageId = a.MessageId
    UNION ALL SELECT 'Reviews', COUNT_BIG(*) FROM orp.Reviews r JOIN @Targets t ON t.MessageId = r.MessageId
    UNION ALL SELECT 'Assignments', COUNT_BIG(*) FROM orp.Assignments a JOIN @Targets t ON t.MessageId = a.MessageId
    UNION ALL SELECT 'MessageDepartments', COUNT_BIG(*) FROM orp.MessageDepartments d JOIN @Targets t ON t.MessageId = d.MessageId
    UNION ALL SELECT 'Messages', COUNT_BIG(*) FROM orp.Messages m JOIN @Targets t ON t.MessageId = m.MessageId
    UNION ALL SELECT 'SwiftMessages', COUNT_BIG(*) FROM @Targets
    UNION ALL SELECT 'IgnoredImportFiles', COUNT_BIG(*) FROM orp.IgnoredImportFiles i JOIN @Targets t ON t.FilePath = i.FilePath;

    IF @DryRun = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'Preview only. No data deleted.';
        RETURN;
    END;

    DELETE a FROM orp.AuditEvents a JOIN @Targets t ON t.MessageId = a.MessageId;
    DELETE r FROM orp.Reviews r JOIN @Targets t ON t.MessageId = r.MessageId;
    DELETE a FROM orp.Assignments a JOIN @Targets t ON t.MessageId = a.MessageId;
    DELETE d FROM orp.MessageDepartments d JOIN @Targets t ON t.MessageId = d.MessageId;
    DELETE m FROM orp.Messages m JOIN @Targets t ON t.MessageId = m.MessageId;
    DELETE i FROM orp.IgnoredImportFiles i JOIN @Targets t ON t.FilePath = i.FilePath;
    DELETE s FROM orp.SwiftMessages s JOIN @Targets t ON t.MessageId = s.MessageId;

    COMMIT TRANSACTION;
    PRINT 'Deletion committed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
