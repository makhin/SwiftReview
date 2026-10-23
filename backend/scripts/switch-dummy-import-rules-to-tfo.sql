-- Optional TEST DATA. Run manually in the target ORP database after the routing seeds.
-- Copies the existing dummy CS conditions to TFO and disables only those CS rules.
-- Incoming (1): DOUBLE_CONFIRMATION; Outgoing (2): SINGLE_CONFIRMATION.
-- Safe to rerun. Existing messages and shared workflow definitions are not changed.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock @Resource=N'orp-dummy-import-testing',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @lockResult < 0 THROW 51000, 'Cannot acquire dummy import seed lock.', 1;

    DECLARE @source nvarchar(255)=N'dummy-import-testing-2026-10-16';
    DECLARE @target nvarchar(255)=N'dummy-import-testing-tfo-2026-10-23';
    IF (SELECT COUNT(*) FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS=N'CS')<>1
        OR (SELECT COUNT(*) FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS=N'TFO')<>1
        THROW 51000, 'Exactly one CS and one TFO department are required.', 1;
    DECLARE @cs int=(SELECT Id FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS=N'CS');
    DECLARE @tfo int=(SELECT Id FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS=N'TFO');
    DECLARE @double int=(SELECT Id FROM orp.WorkflowDefinitions WHERE Code=N'DOUBLE_CONFIRMATION' AND IsActive=1);
    DECLARE @single int=(SELECT Id FROM orp.WorkflowDefinitions WHERE Code=N'SINGLE_CONFIRMATION' AND IsActive=1);
    IF @double IS NULL OR @single IS NULL
        THROW 51000, 'Active SINGLE_CONFIRMATION and DOUBLE_CONFIRMATION workflows are required.', 1;

    -- Include inactive originals so a second execution reuses the same conditions.
    SELECT DISTINCT BranchId,Direction,MatchKind,MessageTypePattern,SourceSheet,SourceRow,
        CASE Direction WHEN 1 THEN @double ELSE @single END AS WorkflowDefinitionId
    INTO #DummyTfoRules
    FROM orp.DepartmentRoutingRules WITH (UPDLOCK,HOLDLOCK)
    WHERE SourceFile=@source AND DepartmentId=@cs;
    IF NOT EXISTS (SELECT 1 FROM #DummyTfoRules)
        THROW 51000, 'No original dummy CS rules found; run seed-dummy-import-rules.sql first.', 1;
    IF EXISTS (SELECT 1 FROM #DummyTfoRules WHERE MatchKind<>1 OR BranchId IS NULL OR Direction NOT IN (1,2))
        THROW 51000, 'Expected branch-specific Exact dummy CS rules.', 1;

    UPDATE orp.DepartmentRoutingRules SET IsActive=0
    WHERE SourceFile=@source AND DepartmentId=@cs AND IsActive=1;
    DECLARE @disabled int=@@ROWCOUNT;

    -- Other departments would still receive these messages. Do not silently alter their rules.
    IF EXISTS (
        SELECT 1 FROM #DummyTfoRules t JOIN orp.DepartmentRoutingRules r
          ON r.IsActive=1 AND r.DepartmentId<>@tfo AND r.Direction=t.Direction
          AND (r.BranchId IS NULL OR r.BranchId=t.BranchId)
          AND (r.MatchKind=1 AND r.MessageTypePattern COLLATE Latin1_General_100_CI_AS=t.MessageTypePattern COLLATE Latin1_General_100_CI_AS
            OR r.MatchKind=2 AND LEFT(r.MessageTypePattern,3) COLLATE Latin1_General_100_CI_AS=LEFT(t.MessageTypePattern,3) COLLATE Latin1_General_100_CI_AS
            OR r.MatchKind=3 AND (r.MessageTypePattern COLLATE Latin1_General_100_CI_AS=t.MessageTypePattern COLLATE Latin1_General_100_CI_AS
              OR t.MessageTypePattern COLLATE Latin1_General_100_CI_AS LIKE (r.MessageTypePattern+N'.%') COLLATE Latin1_General_100_CI_AS))
    ) THROW 51000, 'Other active departments overlap dummy messages; switch rolled back.', 1;
    IF EXISTS (
        SELECT 1 FROM #DummyTfoRules t JOIN orp.DepartmentRoutingRules r
          ON r.DepartmentId=@tfo AND r.BranchId=t.BranchId AND r.Direction=t.Direction
          AND r.MatchKind=1 AND r.MessageTypePattern COLLATE Latin1_General_100_CI_AS=t.MessageTypePattern COLLATE Latin1_General_100_CI_AS
        WHERE r.IsActive=1 AND r.SourceFile<>@target
          AND (r.WorkflowDefinitionId IS NULL OR r.WorkflowDefinitionId<>t.WorkflowDefinitionId)
    ) THROW 51000, 'Existing TFO exact rules select a different workflow; switch rolled back.', 1;

    UPDATE r SET IsActive=1,WorkflowDefinitionId=t.WorkflowDefinitionId
    FROM orp.DepartmentRoutingRules r JOIN #DummyTfoRules t
      ON r.BranchId=t.BranchId AND r.Direction=t.Direction
      AND r.MatchKind=t.MatchKind AND r.MessageTypePattern=t.MessageTypePattern
    WHERE r.DepartmentId=@tfo AND r.SourceFile=@target;

    INSERT INTO orp.DepartmentRoutingRules
        (DepartmentId,BranchId,Direction,MatchKind,MessageTypePattern,IsActive,SourceFile,SourceSheet,SourceRow,WorkflowDefinitionId)
    SELECT @tfo,t.BranchId,t.Direction,t.MatchKind,t.MessageTypePattern,1,@target,t.SourceSheet,t.SourceRow,t.WorkflowDefinitionId
    FROM #DummyTfoRules t WHERE NOT EXISTS (
        SELECT 1 FROM orp.DepartmentRoutingRules r
        WHERE r.SourceFile=@target AND r.DepartmentId=@tfo AND r.BranchId=t.BranchId
          AND r.Direction=t.Direction AND r.MatchKind=t.MatchKind AND r.MessageTypePattern=t.MessageTypePattern
    );
    DECLARE @added int=@@ROWCOUNT;
    DROP TABLE #DummyTfoRules;
    COMMIT TRANSACTION;
    SELECT @disabled AS DummyCsRulesDisabled,@added AS DummyTfoRulesAdded;
    SELECT r.Id,b.Name AS Branch,r.MessageTypePattern,
        CASE r.Direction WHEN 1 THEN N'Incoming' ELSE N'Outgoing' END AS Direction,w.Code AS Workflow,r.IsActive
    FROM orp.DepartmentRoutingRules r
    JOIN orp.Branches b ON b.Id=r.BranchId
    JOIN orp.WorkflowDefinitions w ON w.Id=r.WorkflowDefinitionId
    WHERE r.SourceFile=@target AND r.DepartmentId=@tfo
    ORDER BY b.Name,r.MessageTypePattern,r.Direction;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
