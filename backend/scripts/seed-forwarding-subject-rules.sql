-- Run after migrations and department setup. Existing/customized rules are preserved.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM orp.Departments WHERE Name = N'TFO')
BEGIN
    ROLLBACK;
    THROW 50001, 'TFO department is missing. Create the department before installing its forwarding subject rule.', 1;
END;
INSERT INTO orp.ForwardingSubjectRules (Name, IsActive, Priority, MessageType, BranchId, DepartmentId, Template)
SELECT N'TFO forwarding subject', 1, 100, NULL, NULL, d.Id, N'FW: {MessageType} - {Field21}'
FROM orp.Departments d
WHERE d.Name = N'TFO'
  AND NOT EXISTS (SELECT 1 FROM orp.ForwardingSubjectRules WITH (UPDLOCK, HOLDLOCK) WHERE Name = N'TFO forwarding subject');
COMMIT;
