# Workflow confirmation configuration

Workflow behavior is stored in `orp.WorkflowDefinitions`; required levels are
stored in `orp.WorkflowSteps`. The engine supports levels 1–3 and does not branch
on department names or message types. Existing MT-series/MX-family matching and
branch-specific routing priority are preserved. Multi-department workflows match
the exact set of message departments.

## Settings

| Column | Values | Migration default |
| --- | --- | --- |
| `ConfirmationMode` | 0 `ReviewSession`, 1 `Immediate`, 2 `None` | 0 |
| `ConfirmationActorMode` | 0 `Assignee`, 1 `AnyAuthorizedUser` | 0 |
| `AllowReviewerReuse` | 0 requires distinct approved reviewers, 1 permits reuse | 0 |
| `AssignmentRetentionMode` | 0 `PerConfirmation`, 1 `UntilWorkflowCompleted` | 0 |
| `RejectAllowed` | 0 disabled, 1 allowed for review sessions | 1 |
| `UndoApprovalMode` | 0 `Disabled`, 1 `LatestNonFinal`, 2 `Latest` | 0 |
| `UndoActorMode` | 0 `OriginalReviewer`, 1 `AnyAuthorizedUser` | 0 |
| `UndoActiveReviewMode` | 0 `Block`, 1 `Cancel` the active next review | 0 |

`None` requires zero steps, including optional steps. Other modes require at least
one required level, starting at 1 and ascending in workflow order. `Immediate`
requires assignment before confirmation, including for administrators. The same
message assignee may remain responsible while another authorized user confirms.
`UntilWorkflowCompleted` preserves assignment between approvals and on Undo;
completion closes it. Undo of a final approval does not restore a closed assignment.

The administrator Undo exception is retained deliberately: with no active review,
the latest approval may be undone even if final or disabled by policy. With an
active review, approval/active-review policy applies. Existing administrator
exceptions for reviewer ownership and reuse also remain. Reject disabled by
configuration cannot be bypassed by administrators.

Permissions remain `review.level1`, `review.level2`, `review.level3`, `review.undo`
and `message.view`, scoped to the message. Configuration does not grant permissions
or create role assignments. View and action permission must coexist in at least
one permitted message branch/department scope, never be combined across scopes.

## Agreed demo configuration

| Route | Levels | Mode | Actor / reuse | Assignment | Undo | Reject |
| --- | --- | --- | --- | --- | --- | --- |
| CS/TFO examples, Incoming and Outgoing | 1, 2 | Immediate | Any authorized / yes | Until completion | Latest non-final, any authorized, cancel active next review | Disabled |
| DC MT671 | 1 | Immediate | Any authorized / yes | Until completion | Disabled for ordinary users | Disabled |
| DC MT094 template | none | None, inactive | No confirmation actions | Not activated | Disabled | Disabled |

`backend/scripts/seed-test-data.sql` supplies these examples and asserts the
CS/TFO two-stage limit as a configuration check. It is a **destructive test-data
reset**, not a rollout script. Do not run it against existing application data to
apply settings. The migration changes the schema and preserves existing workflow
behavior and message history. Review3 remains available for separately configured
routes and is tested independently of the demo departments.

MT094 completion after PDF storage, failure status and retry behavior remain
open under QQ-018. The inactive template does not route new messages and must not
be activated as a substitute for deciding that lifecycle. The Scheduler/PDF
implementation is outside this change.

## Applying settings

Apply the `ConfigureConfirmations` EF migration using the existing migration
procedure in `backend/README.md`. Configure only reviewed workflow IDs, together
with their required steps. Keep schema migration separate from business routing
changes. There is no workflow editor or administrative configuration API in this
iteration.

Example for a specifically selected, already valid two-step workflow (replace
the placeholder ID before executing; no department/type matching in this update):

```sql
DECLARE @WorkflowId int = 0; -- replace with the reviewed workflow ID
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM orp.WorkflowDefinitions WITH (UPDLOCK, HOLDLOCK) WHERE Id = @WorkflowId)
    THROW 51000, 'Select an existing reviewed workflow ID.', 1;
IF (SELECT COUNT(*) FROM orp.WorkflowSteps WHERE WorkflowDefinitionId = @WorkflowId) <> 2
   OR NOT EXISTS (SELECT 1 FROM orp.WorkflowSteps WHERE WorkflowDefinitionId = @WorkflowId AND ReviewLevel = 1 AND [Order] = 1 AND Required = 1)
   OR NOT EXISTS (SELECT 1 FROM orp.WorkflowSteps WHERE WorkflowDefinitionId = @WorkflowId AND ReviewLevel = 2 AND [Order] = 2 AND Required = 1)
    THROW 51000, 'Configure exactly two required steps before applying this policy.', 1;
IF EXISTS (SELECT 1 FROM orp.Messages WHERE WorkflowDefinitionId = @WorkflowId)
    THROW 51000, 'This example is for unused routes; review existing messages before changing policy.', 1;
UPDATE orp.WorkflowDefinitions
SET ConfirmationMode = 1, ConfirmationActorMode = 1, AllowReviewerReuse = 1,
    AssignmentRetentionMode = 1, RejectAllowed = 0,
    UndoApprovalMode = 1, UndoActorMode = 1, UndoActiveReviewMode = 1
WHERE Id = @WorkflowId;
COMMIT;
```

The frontend is a separate follow-up. Its existing Start/Approve interface cannot
operate an Immediate workflow; those endpoints return 409 for that mode. Deploy
and validate the new Confirm client before activating these routes for UI users.
Regenerate TypeScript contracts and run frontend checks in that follow-up.

## Audit and concurrency

Confirm writes `ReviewStarted`, `ReviewApproved` and, on final confirmation,
`MessageCompleted`; ending an assignment also writes `MessageUnassigned`.
Undo keeps `ConfirmationUndone` and any `ReviewCancelled` event. The existing event
codes represent the business ConfirmationStarted/Completed/Undone events without
renaming historical data. Every review records its actual actor.

Confirm requires the `version` returned by details/grid as `expectedVersion`.
The server compares SQL rowversion inside its serializable mutation transaction.
A stale request, including a delayed request after Confirm → Undo, returns 409.
The version is not an idempotency key; after uncertain delivery, inspect state and
audit before deciding to submit a new action. Review, assignment and audit changes
commit or roll back together. Undo continues to target its specific `reviewId`.
