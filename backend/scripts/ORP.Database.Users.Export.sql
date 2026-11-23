-- Run in the source ORP database. Copy the result cells into the import file.
-- Only accounts with a Windows identity binding are exported.
SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run the export outside an existing transaction.', 16, 1);
    RETURN;
END;

SET XACT_ABORT ON;
SET IMPLICIT_TRANSACTIONS OFF;

IF OBJECT_ID(N'orp.AccessPolicyRules', N'U') IS NULL
    OR OBJECT_ID(N'orp.ExternalIdentities', N'U') IS NULL
    THROW 51000, 'Select an ORP database with the access-policy schema.', 1;

DECLARE @payload nvarchar(max);
DECLARE @exportedUsers TABLE (Id int PRIMARY KEY);
DECLARE @usedPolicies TABLE (Id int PRIMARY KEY);
DECLARE @usedRoles TABLE (Id int PRIMARY KEY);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO @exportedUsers (Id)
    SELECT DISTINCT UserId FROM orp.ExternalIdentities WITH (HOLDLOCK);

    INSERT INTO @usedPolicies (Id)
    SELECT DISTINCT ur.AccessPolicyId
    FROM @exportedUsers e JOIN orp.UserRoles ur WITH (HOLDLOCK) ON ur.UserId = e.Id;

    INSERT INTO @usedRoles (Id)
    SELECT DISTINCT ur.RoleId
    FROM @exportedUsers e JOIN orp.UserRoles ur WITH (HOLDLOCK) ON ur.UserId = e.Id;

    SELECT @payload = (
        SELECT 1 AS formatVersion,
            JSON_QUERY(ISNULL((
                SELECT u.UserName, u.DisplayName, u.IsGlobalAdministrator
                FROM @exportedUsers ids JOIN orp.Users u WITH (HOLDLOCK) ON u.Id = ids.Id
                ORDER BY u.UserName FOR JSON PATH
            ), N'[]')) AS users,
            JSON_QUERY(ISNULL((
                SELECT u.UserName, e.Sid, e.Domain, e.Account, e.ProfileId, e.Email
                FROM orp.ExternalIdentities e WITH (HOLDLOCK)
                JOIN orp.Users u WITH (HOLDLOCK) ON u.Id = e.UserId
                ORDER BY e.Sid FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS identities,
            JSON_QUERY(ISNULL((
                SELECT u.UserName, b.Name AS BranchName, d.Name AS DepartmentName,
                    r.Name AS RoleName, p.Code AS PolicyCode
                FROM @exportedUsers ids
                JOIN orp.UserRoles ur WITH (HOLDLOCK) ON ur.UserId = ids.Id
                JOIN orp.Users u WITH (HOLDLOCK) ON u.Id = ur.UserId
                JOIN orp.Branches b WITH (HOLDLOCK) ON b.Id = ur.BranchId
                JOIN orp.Departments d WITH (HOLDLOCK) ON d.Id = ur.DepartmentId
                JOIN orp.Roles r WITH (HOLDLOCK) ON r.Id = ur.RoleId
                JOIN orp.AccessPolicies p WITH (HOLDLOCK) ON p.Id = ur.AccessPolicyId
                ORDER BY u.UserName, b.Name, d.Name, r.Name, p.Code FOR JSON PATH
            ), N'[]')) AS assignments,
            JSON_QUERY(ISNULL((
                SELECT p.Code, p.Name
                FROM @usedPolicies ids JOIN orp.AccessPolicies p WITH (HOLDLOCK) ON p.Id = ids.Id
                ORDER BY p.Code FOR JSON PATH
            ), N'[]')) AS policies,
            JSON_QUERY(ISNULL((
                SELECT p.Code AS PolicyCode, pm.Name AS PermissionName,
                    pr.MessageTypePattern, pr.Direction, pr.RequiresReference
                FROM @usedPolicies ids
                JOIN orp.AccessPolicies p WITH (HOLDLOCK) ON p.Id = ids.Id
                JOIN orp.AccessPolicyRules pr WITH (HOLDLOCK) ON pr.AccessPolicyId = ids.Id
                JOIN orp.Permissions pm WITH (HOLDLOCK) ON pm.Id = pr.PermissionId
                ORDER BY p.Code, pm.Name, pr.MessageTypePattern, pr.Direction FOR JSON PATH
            ), N'[]')) AS policyRules,
            JSON_QUERY(ISNULL((
                SELECT r.Name AS RoleName, pm.Name AS PermissionName
                FROM @usedRoles ids
                JOIN orp.Roles r WITH (HOLDLOCK) ON r.Id = ids.Id
                JOIN orp.RolePermissions rp WITH (HOLDLOCK) ON rp.RoleId = ids.Id
                JOIN orp.Permissions pm WITH (HOLDLOCK) ON pm.Id = rp.PermissionId
                ORDER BY r.Name, pm.Name FOR JSON PATH
            ), N'[]')) AS rolePermissions,
            JSON_QUERY(ISNULL((
                SELECT Code FROM orp.Mt999ReferenceCodes WITH (HOLDLOCK) ORDER BY Code FOR JSON PATH
            ), N'[]')) AS mt999Codes
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Emit portable, grid-sized SQL lines. Hex preserves Unicode and quotes when copied.
DECLARE @bytes varbinary(max) = CONVERT(varbinary(max), @payload);
DECLARE @chunkSize int = 1000;
DECLARE @offset bigint = 1;
DECLARE @ordinal int = 1;
DECLARE @lines TABLE (Ordinal int PRIMARY KEY, ImportSql varchar(2100) NOT NULL);

INSERT INTO @lines VALUES (0, CONCAT('SET @ExpectedPayloadHash = ',
    CONVERT(varchar(66), HASHBYTES('SHA2_256', @bytes), 1), ';'));

WHILE @offset <= DATALENGTH(@bytes)
BEGIN
    INSERT INTO @lines VALUES (@ordinal, CONCAT('INSERT INTO @PayloadChunks VALUES (', @ordinal, ', ',
        CONVERT(varchar(2002), SUBSTRING(@bytes, @offset, @chunkSize), 1), ');'));
    SET @offset += @chunkSize;
    SET @ordinal += 1;
END;

SELECT ImportSql FROM @lines ORDER BY Ordinal;
