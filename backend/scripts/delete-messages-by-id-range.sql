-- Run manually in the intended application database after stopping API/importer writers.
-- IDs are orp.Messages.MessageId / orp.SwiftMessages.MessageId; BOTH bounds are inclusive.
-- Set the bounds and review with @DryRun = 1, then set @DryRun = 0 to delete.
-- Ordinary messages: preserves shared users, roles, directories and workflows.
-- E2E messages: also removes fixtures identified by the exact run UUID embedded in
-- branch name, workflow code, source path and reference. Never deletes by prefix alone.
-- Existing external references cause an error and roll back the entire cleanup.
-- @TestRunId is for fixture disposal (including setup without an imported message);
-- Or set @AllE2E = 1 with both bounds and @TestRunId NULL to discover ALL E2E
-- leftovers, including users/workflows/directories with no messages. Discovery uses
-- the fixture's reserved exact names with a lowercase 32-hex UUID, not a broad prefix.
-- Choose exactly one mode. Physical source files are not removed.
-- Removing the import record allows the original file to be imported again.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @FromId bigint = NULL;
DECLARE @ToId bigint = NULL;
DECLARE @DryRun bit = 1;
DECLARE @TestRunId varchar(32) = NULL;
DECLARE @AllE2E bit = 0;

IF @AllE2E IS NULL OR (@AllE2E = 1 AND (@FromId IS NOT NULL OR @ToId IS NOT NULL OR @TestRunId IS NOT NULL))
    THROW 51000, 'Use exactly one mode: ID range, @TestRunId, or @AllE2E = 1.', 1;
IF @AllE2E = 0 AND @TestRunId IS NULL AND (@FromId IS NULL OR @ToId IS NULL OR @FromId <= 0 OR @ToId < @FromId)
    THROW 51000, 'Set a valid inclusive range: 0 < @FromId <= @ToId.', 1;
IF @TestRunId IS NOT NULL AND (@FromId IS NOT NULL OR @ToId IS NOT NULL OR
    LEN(@TestRunId) <> 32 OR @TestRunId COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%')
    THROW 51000, 'Use either an ID range or an exact lowercase 32-character E2E run ID.', 1;
IF @DryRun IS NULL
    THROW 51000, '@DryRun must be 0 or 1.', 1;
IF @@TRANCOUNT <> 0
    THROW 51000, 'Run this script outside an existing transaction.', 1;

DECLARE @Targets TABLE
(
    MessageId bigint PRIMARY KEY,
    FilePath nvarchar(450) COLLATE Latin1_General_100_CI_AS NOT NULL
);

DECLARE @Runs TABLE (RunId varchar(32) PRIMARY KEY);
DECLARE @ImportPaths TABLE (FilePath nvarchar(450) COLLATE Latin1_General_100_CI_AS PRIMARY KEY);
DECLARE @Fixtures TABLE
(
    RunId varchar(32) PRIMARY KEY,
    BranchId int NULL, DepartmentId int NULL, RoleId int NULL,
    WorkflowId int NULL, FirstUserId int NULL, SecondUserId int NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO @Targets (MessageId, FilePath)
    SELECT MessageId, FilePath
    FROM orp.SwiftMessages WITH (XLOCK, HOLDLOCK)
    WHERE (@TestRunId IS NULL AND MessageId BETWEEN @FromId AND @ToId)
       OR (@TestRunId IS NOT NULL AND FilePath = CONCAT('approval-', @TestRunId, '.prt')
           AND ReferenceId = CONCAT('E2E-', @TestRunId));

    IF @AllE2E = 1
    BEGIN
        -- Every fixture-owned root can identify a run on its own, even after
        -- messages or some other fixture objects have already been removed.
        ;WITH Candidates AS
        (
            SELECT SUBSTRING(Name, 5, 32) AS RunId FROM orp.Branches
            WHERE LEN(Name) = 36 AND LEFT(Name, 4) = 'E2E-'
            UNION ALL SELECT SUBSTRING(Name, 5, 32) FROM orp.Departments
            WHERE LEN(Name) = 36 AND LEFT(Name, 4) = 'E2E-'
            UNION ALL SELECT SUBSTRING(Name, 5, 32) FROM orp.Roles
            WHERE LEN(Name) = 36 AND LEFT(Name, 4) = 'E2E-'
            UNION ALL SELECT SUBSTRING(Code, 5, 32) FROM orp.WorkflowDefinitions
            WHERE LEN(Code) = 36 AND LEFT(Code, 4) = 'E2E_'
            UNION ALL SELECT SUBSTRING(UserName, 11, 32) FROM orp.Users
            WHERE LEN(UserName) = 42 AND LEFT(UserName, 10) = 'e2e.first.'
            UNION ALL SELECT SUBSTRING(UserName, 12, 32) FROM orp.Users
            WHERE LEN(UserName) = 43 AND LEFT(UserName, 11) = 'e2e.second.'
            UNION ALL SELECT SUBSTRING(SourceFile, 5, 32) FROM orp.DepartmentRoutingRules
            WHERE LEN(SourceFile) = 36 AND LEFT(SourceFile, 4) = 'e2e-'
            UNION ALL SELECT SUBSTRING(FilePath, 10, 32) COLLATE DATABASE_DEFAULT FROM orp.IgnoredImportFiles
            WHERE LEN(FilePath) = 45 AND LEFT(FilePath, 9) = 'approval-' AND RIGHT(FilePath, 4) = '.prt'
            UNION ALL SELECT SUBSTRING(FilePath, 10, 32) COLLATE DATABASE_DEFAULT FROM orp.SwiftMessages
            WHERE LEN(FilePath) = 45 AND LEFT(FilePath, 9) = 'approval-' AND RIGHT(FilePath, 4) = '.prt'
              AND ReferenceId = CONCAT('E2E-', SUBSTRING(FilePath, 10, 32)) COLLATE DATABASE_DEFAULT
        )
        INSERT INTO @Runs
        SELECT DISTINCT RunId FROM Candidates
        WHERE LEN(RunId) = 32 AND RunId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%';

        INSERT INTO @Targets (MessageId, FilePath)
        SELECT s.MessageId, s.FilePath FROM orp.SwiftMessages s WITH (XLOCK, HOLDLOCK)
        JOIN @Runs r ON s.FilePath = CONCAT('approval-', r.RunId, '.prt') COLLATE Latin1_General_100_CI_AS
            AND s.ReferenceId = CONCAT('E2E-', r.RunId);
    END
    ELSE IF @TestRunId IS NOT NULL
        INSERT INTO @Runs VALUES (@TestRunId);
    ELSE
        INSERT INTO @Runs
        SELECT DISTINCT SUBSTRING(b.Name, 5, 32)
        FROM @Targets t
        JOIN orp.SwiftMessages s ON s.MessageId = t.MessageId
        JOIN orp.Messages m ON m.MessageId = t.MessageId
        JOIN orp.Branches b ON b.Id = m.BranchId
        JOIN orp.WorkflowDefinitions w ON w.Id = m.WorkflowDefinitionId
        WHERE LEN(b.Name) = 36 AND LEFT(b.Name, 4) = 'E2E-'
          AND SUBSTRING(b.Name, 5, 32) COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%'
          AND w.Code = CONCAT('E2E_', SUBSTRING(b.Name, 5, 32))
          AND s.ReferenceId = b.Name
          AND s.FilePath = CONCAT('approval-', SUBSTRING(b.Name, 5, 32), '.prt') COLLATE Latin1_General_100_CI_AS;

    -- Scalar lookups deliberately fail on ambiguous names rather than deleting
    -- multiple directory entries. These exact names are allocated by the E2E fixture.
    INSERT INTO @Fixtures
    SELECT r.RunId,
        (SELECT Id FROM orp.Branches WITH (UPDLOCK, HOLDLOCK) WHERE Name = CONCAT('E2E-', r.RunId)),
        (SELECT Id FROM orp.Departments WITH (UPDLOCK, HOLDLOCK) WHERE Name = CONCAT('E2E-', r.RunId)),
        (SELECT Id FROM orp.Roles WITH (UPDLOCK, HOLDLOCK) WHERE Name = CONCAT('E2E-', r.RunId)),
        (SELECT Id FROM orp.WorkflowDefinitions WITH (UPDLOCK, HOLDLOCK) WHERE Code = CONCAT('E2E_', r.RunId)),
        (SELECT Id FROM orp.Users WITH (UPDLOCK, HOLDLOCK) WHERE UserName = CONCAT('e2e.first.', r.RunId)),
        (SELECT Id FROM orp.Users WITH (UPDLOCK, HOLDLOCK) WHERE UserName = CONCAT('e2e.second.', r.RunId))
    FROM @Runs r;

    INSERT INTO @ImportPaths
    SELECT FilePath FROM @Targets
    UNION
    SELECT CONCAT('approval-', RunId, '.prt') COLLATE Latin1_General_100_CI_AS FROM @Runs;

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
    UNION ALL SELECT 'IgnoredImportFiles', COUNT_BIG(*) FROM orp.IgnoredImportFiles i JOIN @ImportPaths t ON t.FilePath = i.FilePath;

    -- Extra objects to remove for recognized E2E runs. Empty for ordinary messages.
    SELECT * FROM @Fixtures ORDER BY RunId;

    IF @DryRun = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'Preview only. No data deleted.';
        RETURN;
    END;

    -- User/Role -> UserRoles uses CASCADE in the application schema. Guard
    -- explicitly against removing grants outside this fixture before deleting parents.
    IF EXISTS (
        SELECT 1 FROM orp.UserRoles ur WITH (UPDLOCK, HOLDLOCK)
        JOIN @Fixtures f ON ur.RoleId = f.RoleId OR ur.UserId IN (f.FirstUserId, f.SecondUserId)
        WHERE NOT (ur.BranchId = ISNULL(f.BranchId, -1)
            AND ur.DepartmentId = ISNULL(f.DepartmentId, -1)
            AND ur.RoleId = ISNULL(f.RoleId, -1)
            AND ur.UserId IN (ISNULL(f.FirstUserId, -1), ISNULL(f.SecondUserId, -1)))
    )
        THROW 51002, 'An E2E user or role is used outside its fixture. No data deleted.', 1;

    DELETE a FROM orp.AuditEvents a JOIN @Targets t ON t.MessageId = a.MessageId;
    DELETE r FROM orp.Reviews r JOIN @Targets t ON t.MessageId = r.MessageId;
    DELETE a FROM orp.Assignments a JOIN @Targets t ON t.MessageId = a.MessageId;
    DELETE d FROM orp.MessageDepartments d JOIN @Targets t ON t.MessageId = d.MessageId;
    DELETE m FROM orp.Messages m JOIN @Targets t ON t.MessageId = m.MessageId;
    DELETE i FROM orp.IgnoredImportFiles i JOIN @ImportPaths t ON t.FilePath = i.FilePath;
    DELETE s FROM orp.SwiftMessages s JOIN @Targets t ON t.MessageId = s.MessageId;

    -- Remove only the test's own links, never links from another scope/user/run.
    -- Any remaining external reference prevents parent deletion via FK constraints.
    DELETE r FROM orp.DepartmentRoutingRules r JOIN @Fixtures f
        ON r.BranchId = f.BranchId AND r.DepartmentId = f.DepartmentId
        AND r.WorkflowDefinitionId = f.WorkflowId AND r.SourceFile = CONCAT('e2e-', f.RunId);
    DELETE ur FROM orp.UserRoles ur JOIN @Fixtures f
        ON ur.BranchId = f.BranchId AND ur.DepartmentId = f.DepartmentId AND ur.RoleId = f.RoleId
        AND ur.UserId IN (f.FirstUserId, f.SecondUserId);
    DELETE rp FROM orp.RolePermissions rp JOIN @Fixtures f ON rp.RoleId = f.RoleId;
    DELETE ws FROM orp.WorkflowSteps ws JOIN @Fixtures f ON ws.WorkflowDefinitionId = f.WorkflowId;
    DELETE w FROM orp.WorkflowDefinitions w JOIN @Fixtures f ON w.Id = f.WorkflowId;
    DELETE u FROM orp.Users u JOIN @Fixtures f ON u.Id IN (f.FirstUserId, f.SecondUserId);
    DELETE r FROM orp.Roles r JOIN @Fixtures f ON r.Id = f.RoleId;
    DELETE d FROM orp.Departments d JOIN @Fixtures f ON d.Id = f.DepartmentId;
    DELETE b FROM orp.Branches b JOIN @Fixtures f ON b.Id = f.BranchId;

    COMMIT TRANSACTION;
    PRINT 'Deletion committed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
