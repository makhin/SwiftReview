-- Upgrade existing routing rules to the MX-prefixed identifiers used by SAA headers.
-- Idempotent; preserves rule IDs, priorities, activation and provenance.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE [orp].[DepartmentRoutingRules]
    SET [MessageTypePattern] = N'MX' + [MessageTypePattern]
    WHERE [MatchKind] IN (1, 3) AND (
        LOWER([MessageTypePattern]) COLLATE Latin1_General_100_BIN2 LIKE N'[a-z][a-z][a-z][a-z].[0-9][0-9][0-9]'
        OR LOWER([MessageTypePattern]) COLLATE Latin1_General_100_BIN2 LIKE N'[a-z][a-z][a-z][a-z].[0-9][0-9][0-9].[0-9][0-9][0-9].[0-9][0-9]');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
