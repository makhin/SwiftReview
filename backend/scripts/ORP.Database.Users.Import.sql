-- Run in the target ORP database after schema and configuration setup.
-- Paste every result cell from the export between the markers below.
SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run the import outside an existing transaction.', 16, 1);
    RETURN;
END;

SET XACT_ABORT ON;
SET IMPLICIT_TRANSACTIONS OFF;
SET ANSI_WARNINGS ON;

DECLARE @ExpectedPayloadHash varbinary(32);
DECLARE @PayloadChunks TABLE (Ordinal int PRIMARY KEY, Payload varbinary(1000) NOT NULL);

-- BEGIN EXPORT RESULT CELLS

-- END EXPORT RESULT CELLS

DECLARE @bytes varbinary(max);
SELECT @bytes = CONVERT(varbinary(max),
    STRING_AGG(CONVERT(varchar(max), Payload, 2), '') WITHIN GROUP (ORDER BY Ordinal), 2)
FROM @PayloadChunks;

IF @ExpectedPayloadHash IS NULL OR @bytes IS NULL OR HASHBYTES('SHA2_256', @bytes) <> @ExpectedPayloadHash
    THROW 51000, 'The export is missing or incomplete. Paste every result cell from the export.', 1;

DECLARE @json nvarchar(max) = CONVERT(nvarchar(max), @bytes);
IF ISJSON(@json) <> 1
    THROW 51000, 'The ORP users export could not be read.', 1;
IF ISNULL(TRY_CONVERT(int, JSON_VALUE(@json, '$.formatVersion')), 0) <> 1
    THROW 51000, 'Unsupported ORP users export version.', 1;

IF DB_ID() <= 4 OR OBJECT_ID(N'orp.AccessPolicyRules', N'U') IS NULL
    OR OBJECT_ID(N'orp.ExternalIdentities', N'U') IS NULL
    THROW 51000, 'Select the target ORP database with the access-policy schema.', 1;

DECLARE @Users TABLE (
    UserName nvarchar(80) PRIMARY KEY, DisplayName nvarchar(160) NOT NULL, IsGlobalAdministrator bit NOT NULL
);
DECLARE @Identities TABLE (
    Sid nvarchar(184) PRIMARY KEY, UserName nvarchar(80) NOT NULL, Domain nvarchar(256) NOT NULL,
    Account nvarchar(256) NOT NULL, ProfileId nvarchar(256), Email nvarchar(320)
);
DECLARE @Assignments TABLE (
    UserName nvarchar(80), BranchName nvarchar(80), DepartmentName nvarchar(80), RoleName nvarchar(80),
    PolicyCode nvarchar(100), PRIMARY KEY (UserName, BranchName, DepartmentName, RoleName, PolicyCode)
);
DECLARE @Policies TABLE (Code nvarchar(100) PRIMARY KEY, Name nvarchar(160) NOT NULL);
DECLARE @PolicyRules TABLE (
    PolicyCode nvarchar(100), PermissionName nvarchar(80), MessageTypePattern nvarchar(100), Direction int,
    RequiresReference bit NOT NULL, PRIMARY KEY (PolicyCode, PermissionName, MessageTypePattern, Direction)
);
DECLARE @RolePermissions TABLE (RoleName nvarchar(80), PermissionName nvarchar(80), PRIMARY KEY (RoleName, PermissionName));
DECLARE @ReferenceCodes TABLE (Code nvarchar(255) PRIMARY KEY);

INSERT INTO @Users (UserName, DisplayName, IsGlobalAdministrator)
SELECT UserName, DisplayName, IsGlobalAdministrator FROM OPENJSON(JSON_QUERY(@json, 'strict $.users'))
WITH (UserName nvarchar(80), DisplayName nvarchar(160), IsGlobalAdministrator bit);
INSERT INTO @Identities (Sid, UserName, Domain, Account, ProfileId, Email)
SELECT Sid, UserName, Domain, Account, ProfileId, Email FROM OPENJSON(JSON_QUERY(@json, 'strict $.identities'))
WITH (Sid nvarchar(184), UserName nvarchar(80), Domain nvarchar(256), Account nvarchar(256), ProfileId nvarchar(256), Email nvarchar(320));
INSERT INTO @Assignments (UserName, BranchName, DepartmentName, RoleName, PolicyCode)
SELECT UserName, BranchName, DepartmentName, RoleName, PolicyCode FROM OPENJSON(JSON_QUERY(@json, 'strict $.assignments'))
WITH (UserName nvarchar(80), BranchName nvarchar(80), DepartmentName nvarchar(80), RoleName nvarchar(80), PolicyCode nvarchar(100));
INSERT INTO @Policies (Code, Name)
SELECT Code, Name FROM OPENJSON(JSON_QUERY(@json, 'strict $.policies')) WITH (Code nvarchar(100), Name nvarchar(160));
INSERT INTO @PolicyRules (PolicyCode, PermissionName, MessageTypePattern, Direction, RequiresReference)
SELECT PolicyCode, PermissionName, MessageTypePattern, Direction, RequiresReference FROM OPENJSON(JSON_QUERY(@json, 'strict $.policyRules'))
WITH (PolicyCode nvarchar(100), PermissionName nvarchar(80), MessageTypePattern nvarchar(100), Direction int, RequiresReference bit);
INSERT INTO @RolePermissions (RoleName, PermissionName)
SELECT RoleName, PermissionName FROM OPENJSON(JSON_QUERY(@json, 'strict $.rolePermissions'))
WITH (RoleName nvarchar(80), PermissionName nvarchar(80));
INSERT INTO @ReferenceCodes (Code)
SELECT Code FROM OPENJSON(JSON_QUERY(@json, 'strict $.mt999Codes')) WITH (Code nvarchar(255));

DECLARE @Branches TABLE (Name nvarchar(80) PRIMARY KEY, Id int NOT NULL);
DECLARE @Departments TABLE (Name nvarchar(80) PRIMARY KEY, Id int NOT NULL);
DECLARE @TargetRolePermissions TABLE (RoleName nvarchar(80), PermissionName nvarchar(80), PRIMARY KEY (RoleName, PermissionName));

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'ORP.Database.Setup', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 0;
    IF @lockResult < 0
        THROW 51000, 'Another ORP database setup is in progress.', 1;

    -- Keep first sign-in from creating an account while this transaction runs.
    IF EXISTS (SELECT 1 FROM orp.Users WITH (TABLOCKX, HOLDLOCK))
        THROW 51000, 'The target ORP database must have an empty Users table.', 1;

    INSERT INTO @Branches (Name, Id)
    SELECT b.Name, MIN(b.Id) FROM orp.Branches b WITH (HOLDLOCK)
    WHERE EXISTS (SELECT 1 FROM @Assignments a WHERE a.BranchName = b.Name)
    GROUP BY b.Name HAVING COUNT(*) = 1;

    INSERT INTO @Departments (Name, Id)
    SELECT d.Name, MIN(d.Id) FROM orp.Departments d WITH (HOLDLOCK)
    WHERE EXISTS (SELECT 1 FROM @Assignments a WHERE a.DepartmentName = d.Name)
    GROUP BY d.Name HAVING COUNT(*) = 1;

    IF EXISTS (
        SELECT 1 FROM @Assignments a
        LEFT JOIN @Users u ON u.UserName = a.UserName
        LEFT JOIN @Branches b ON b.Name = a.BranchName
        LEFT JOIN @Departments d ON d.Name = a.DepartmentName
        LEFT JOIN orp.Roles r WITH (HOLDLOCK) ON r.Name = a.RoleName
        LEFT JOIN @Policies p ON p.Code = a.PolicyCode
        WHERE u.UserName IS NULL OR b.Id IS NULL OR d.Id IS NULL OR r.Id IS NULL OR p.Code IS NULL
    )
        THROW 51000, 'An assignment refers to a missing user, policy, role, or a missing or ambiguous branch or department.', 1;

    IF EXISTS (SELECT UserName FROM @Identities EXCEPT SELECT UserName FROM @Users)
        THROW 51000, 'A Windows SID refers to a user outside this export.', 1;
    IF EXISTS (SELECT UserName FROM @Users EXCEPT SELECT UserName FROM @Identities)
        THROW 51000, 'An exported user is missing a Windows SID binding.', 1;

    IF EXISTS (
        SELECT 1 FROM @PolicyRules pr
        LEFT JOIN @Policies p ON p.Code = pr.PolicyCode
        LEFT JOIN orp.Permissions pm WITH (HOLDLOCK) ON pm.Name = pr.PermissionName
        WHERE p.Code IS NULL OR pm.Id IS NULL
    )
        THROW 51000, 'A policy rule refers to a missing policy or permission.', 1;

    INSERT INTO @TargetRolePermissions (RoleName, PermissionName)
    SELECT r.Name, pm.Name
    FROM orp.Roles r WITH (HOLDLOCK)
    JOIN orp.RolePermissions rp WITH (HOLDLOCK) ON rp.RoleId = r.Id
    JOIN orp.Permissions pm WITH (HOLDLOCK) ON pm.Id = rp.PermissionId
    WHERE EXISTS (SELECT 1 FROM @Assignments a WHERE a.RoleName = r.Name);

    IF EXISTS (SELECT RoleName, PermissionName FROM @RolePermissions EXCEPT SELECT RoleName, PermissionName FROM @TargetRolePermissions)
        OR EXISTS (SELECT RoleName, PermissionName FROM @TargetRolePermissions EXCEPT SELECT RoleName, PermissionName FROM @RolePermissions)
        THROW 51000, 'The target role permissions differ from the source. Align the role catalog and retry.', 1;

    IF EXISTS (SELECT 1 FROM @PolicyRules WHERE RequiresReference = 1)
        AND (
            EXISTS (SELECT Code FROM @ReferenceCodes EXCEPT SELECT Code FROM orp.Mt999ReferenceCodes WITH (HOLDLOCK))
            OR EXISTS (SELECT Code FROM orp.Mt999ReferenceCodes WITH (HOLDLOCK) EXCEPT SELECT Code FROM @ReferenceCodes)
        )
        THROW 51000, 'A policy uses MT999 reference codes and the target codes differ. Align the codes and retry.', 1;

    UPDATE target SET Name = source.Name
    FROM orp.AccessPolicies target JOIN @Policies source ON source.Code = target.Code;

    INSERT INTO orp.AccessPolicies (Code, Name)
    SELECT source.Code, source.Name FROM @Policies source
    WHERE NOT EXISTS (SELECT 1 FROM orp.AccessPolicies target WHERE target.Code = source.Code);

    -- Policy codes shared with the seed take the exact set of rules from the source.
    DELETE rules FROM orp.AccessPolicyRules rules
    JOIN orp.AccessPolicies target ON target.Id = rules.AccessPolicyId
    JOIN @Policies source ON source.Code = target.Code;

    INSERT INTO orp.AccessPolicyRules (AccessPolicyId, PermissionId, MessageTypePattern, Direction, RequiresReference)
    SELECT p.Id, pm.Id, source.MessageTypePattern, source.Direction, source.RequiresReference
    FROM @PolicyRules source
    JOIN orp.AccessPolicies p ON p.Code = source.PolicyCode
    JOIN orp.Permissions pm ON pm.Name = source.PermissionName;

    INSERT INTO orp.Users (UserName, DisplayName, IsGlobalAdministrator)
    SELECT UserName, DisplayName, IsGlobalAdministrator FROM @Users;

    INSERT INTO orp.UserRoles (UserId, BranchId, DepartmentId, RoleId, AccessPolicyId)
    SELECT u.Id, b.Id, d.Id, r.Id, p.Id FROM @Assignments a
    JOIN orp.Users u ON u.UserName = a.UserName
    JOIN @Branches b ON b.Name = a.BranchName
    JOIN @Departments d ON d.Name = a.DepartmentName
    JOIN orp.Roles r ON r.Name = a.RoleName
    JOIN orp.AccessPolicies p ON p.Code = a.PolicyCode;

    INSERT INTO orp.ExternalIdentities (UserId, Sid, Domain, Account, ProfileId, Email, Version)
    SELECT u.Id, e.Sid, e.Domain, e.Account, e.ProfileId, e.Email, NEWID()
    FROM @Identities e JOIN orp.Users u ON u.UserName = e.UserName;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT (SELECT COUNT(*) FROM @Users) AS ImportedUsers,
    (SELECT COUNT(*) FROM @Assignments) AS ImportedAssignments,
    (SELECT COUNT(*) FROM @Identities) AS ImportedWindowsBindings,
    (SELECT COUNT(*) FROM @Policies) AS ImportedPolicies;
