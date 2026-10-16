-- Optional TEST DATA, not part of the approved Excel matrix or automatic startup.
-- Temporary CS routing for the sample files supplied on 2026-10-16.
-- Apply after migrations and seed-department-routing-rules.sql. Safe to rerun.
-- Existing routing rules/workflows are preserved. No directory entries are created.
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

    DECLARE @Targets TABLE (Ordinal int PRIMARY KEY, BranchName nvarchar(80), MessageType nvarchar(20), NeedsRouting bit);
    INSERT INTO @Targets VALUES
        (1,N'Dubai',N'MT081',1),
        (2,N'Paris',N'MXcamt.053.001.08',1),
        (3,N'Paris',N'MXpain.001.001.09',1),
        (4,N'Brussels',N'MXcamt.053.001.08',1),
        (5,N'Brussels',N'MXcamt.054.001.14',1),
        (6,N'Brussels',N'MXcamt.053.001.14',1),
        (7,N'Milan',N'MXcamt.053.001.08',1),
        (8,N'Milan',N'MXcamt.052.001.08',1),
        (9,N'London',N'MXcamt.055.001.08',0);

    IF (SELECT COUNT(*) FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS = N'CS') <> 1
        THROW 51000, 'Dummy import seed requires exactly one CS department.', 1;
    IF EXISTS (SELECT 1 FROM @Targets t OUTER APPLY (
        SELECT COUNT(*) AS Cnt FROM orp.Branches b
        WHERE b.Name COLLATE Latin1_General_100_CI_AS = t.BranchName COLLATE Latin1_General_100_CI_AS
    ) m WHERE m.Cnt <> 1)
        THROW 51000, 'Dummy import seed requires unique Dubai, Paris, Brussels, Milan and London branches; run the approved routing seed first.', 1;

    DECLARE @departmentId int = (SELECT Id FROM orp.Departments WHERE Name COLLATE Latin1_General_100_CI_AS = N'CS');
    DECLARE @SourceFile nvarchar(255) = N'dummy-import-testing-2026-10-16';
    DECLARE @Resolved TABLE (Ordinal int, BranchId int, MessageType nvarchar(20), Direction int, NeedsRouting bit);
    INSERT INTO @Resolved
    SELECT t.Ordinal,b.Id,t.MessageType,d.Direction,t.NeedsRouting FROM @Targets t
    JOIN orp.Branches b ON b.Name COLLATE Latin1_General_100_CI_AS = t.BranchName COLLATE Latin1_General_100_CI_AS
    CROSS JOIN (VALUES (1),(2)) d(Direction)
    WHERE t.NeedsRouting=1 OR d.Direction=1;

    IF EXISTS (
        SELECT 1 FROM @Resolved t JOIN orp.DepartmentRoutingRules r
          ON r.IsActive=1 AND r.DepartmentId<>@departmentId AND r.Direction=t.Direction
          AND (r.BranchId IS NULL OR r.BranchId=t.BranchId)
          AND (r.MatchKind=1 AND r.MessageTypePattern COLLATE Latin1_General_100_CI_AS=t.MessageType COLLATE Latin1_General_100_CI_AS
            OR r.MatchKind=2 AND LEFT(r.MessageTypePattern,3) COLLATE Latin1_General_100_CI_AS=LEFT(t.MessageType,3) COLLATE Latin1_General_100_CI_AS
            OR r.MatchKind=3 AND (r.MessageTypePattern COLLATE Latin1_General_100_CI_AS=t.MessageType COLLATE Latin1_General_100_CI_AS
              OR t.MessageType COLLATE Latin1_General_100_CI_AS LIKE (r.MessageTypePattern+N'.%') COLLATE Latin1_General_100_CI_AS))
        WHERE t.NeedsRouting=1
    ) THROW 51000, 'Dummy routing overlaps another department; configure a shared workflow instead.', 1;

    INSERT INTO orp.DepartmentRoutingRules
        (DepartmentId,BranchId,Direction,MatchKind,MessageTypePattern,IsActive,SourceFile,SourceSheet,SourceRow)
    SELECT @departmentId,t.BranchId,t.Direction,1,t.MessageType,1,@SourceFile,N'Dummy sample matrix',t.Ordinal
    FROM @Resolved t WHERE t.NeedsRouting=1 AND NOT EXISTS (
        SELECT 1 FROM orp.DepartmentRoutingRules r WHERE r.SourceFile=@SourceFile
        AND r.DepartmentId=@departmentId AND r.BranchId=t.BranchId AND r.Direction=t.Direction
        AND r.MatchKind=1 AND r.MessageTypePattern=t.MessageType
    );
    DECLARE @rulesAdded int = @@ROWCOUNT;

    DECLARE @NewWorkflows TABLE (Id int PRIMARY KEY);
    INSERT INTO orp.WorkflowDefinitions
        (Name,MessageType,BranchId,IsActive,Direction,UndoApprovalMode,UndoActorMode,UndoActiveReviewMode)
    OUTPUT inserted.Id INTO @NewWorkflows
    SELECT N'DUMMY import - '+b.Name+N' - '+t.MessageType+CASE t.Direction WHEN 1 THEN N' - IN' ELSE N' - OUT' END,
        t.MessageType,t.BranchId,1,t.Direction,0,0,0
    FROM @Resolved t JOIN orp.Branches b ON b.Id=t.BranchId
    WHERE NOT EXISTS (
        SELECT 1 FROM orp.WorkflowDefinitions w JOIN orp.WorkflowDepartments wd ON wd.WorkflowDefinitionId=w.Id
        WHERE w.IsActive=1 AND wd.DepartmentId=@departmentId
        AND NOT EXISTS (SELECT 1 FROM orp.WorkflowDepartments other
            WHERE other.WorkflowDefinitionId=w.Id AND other.DepartmentId<>@departmentId)
        AND w.Direction=t.Direction AND (w.BranchId=t.BranchId OR w.BranchId IS NULL)
        AND (
            w.MessageType COLLATE Latin1_General_100_CI_AS = t.MessageType COLLATE Latin1_General_100_CI_AS
            OR (LEN(w.MessageType)=10 AND w.MessageType COLLATE Latin1_General_100_CI_AS LIKE N'MX[a-z][a-z][a-z][a-z].[0-9][0-9][0-9]'
                AND LEFT(t.MessageType,11) COLLATE Latin1_General_100_CI_AS = (w.MessageType+N'.') COLLATE Latin1_General_100_CI_AS)
            OR (w.MessageType COLLATE Latin1_General_100_CI_AS LIKE N'MT[0-9]xx'
                AND t.MessageType COLLATE Latin1_General_100_CI_AS LIKE N'MT[0-9][0-9][0-9]'
                AND LEFT(w.MessageType,3) COLLATE Latin1_General_100_CI_AS = LEFT(t.MessageType,3) COLLATE Latin1_General_100_CI_AS)
        )
    );
    INSERT INTO orp.WorkflowDepartments (WorkflowDefinitionId,DepartmentId)
    SELECT Id,@departmentId FROM @NewWorkflows;
    INSERT INTO orp.WorkflowSteps (WorkflowDefinitionId,[Order],ReviewLevel,Required)
    SELECT w.Id,s.Level,s.Level,1 FROM @NewWorkflows w CROSS JOIN (VALUES (1),(2)) s(Level);

    COMMIT TRANSACTION;
    SELECT @rulesAdded AS DummyRulesAdded,(SELECT COUNT(*) FROM @NewWorkflows) AS DummyWorkflowsAdded;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
