-- Run manually after stopping the API and importer. Preserves users, roles, directories,
-- routing rules, workflows, and source files. This script does not run at startup.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;
DELETE FROM [orp].[AuditEvents];
DELETE FROM [orp].[Reviews];
DELETE FROM [orp].[Assignments];
DELETE FROM [orp].[MessageDepartments];
DELETE FROM [orp].[Messages];
DELETE FROM [orp].[SwiftMessages];
DELETE FROM [orp].[IgnoredImportFiles];
COMMIT TRANSACTION;
