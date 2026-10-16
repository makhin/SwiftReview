# Workflow and routing configuration

`orp.WorkflowDefinitions` contains reusable behavior identified by a unique `Code`.
`orp.WorkflowSteps` contains required levels. Definitions no longer contain message
type, direction, branch or departments. The engine supports levels 1–3 and does not
branch on department names or message types.

`orp.DepartmentRoutingRules.WorkflowDefinitionId` links each applicability rule to
its workflow. Rules retain source file, sheet and row. Excel is not read at runtime.

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

## Approved matrix

| Workflow code | Rules | Confirmation / Undo |
| --- | ---: | --- |
| VIEW_ONLY | 49 | None; imported in ViewOnly |
| SINGLE_CONFIRMATION | 96 | Immediate level 1; ordinary Undo disabled |
| DOUBLE_CONFIRMATION | 170 | Immediate levels 1, 2; any authorized actor can undo first before second completes |

Both confirmation profiles allow the same actor, preserve assignment until completion,
and disable Reject. Administrator Undo exceptions remain. No PDF or notification
behavior is implied by any profile.

- CS: 28 distinct Incoming MT types/MX families from Customer Services, all branches,
  two confirmations. Duplicate rows and spaces in identifiers are normalized.
- TFO Dubai: 48 listed Incoming types, two confirmations, including MT101 and pacs.008.
  The agreed choice gives the Yes columns priority over contradictory notes for these
  two types. Outgoing MT7xx uses one confirmation.
- TFO Dusseldorf and Frankfurt: 47 listed types per branch in each direction;
  Incoming uses two confirmations, Outgoing one.
- TFO London, SMBC London, Brussels, Paris, Dublin and Milan: MT4xx, MT7xx, MT999
  and MXpacs.008 in both directions, view only (48 rules).
- DC: Incoming MT094 is active and view only; MT671 requires one confirmation.
  Additional checks in the workbook occur outside ORP. The example MT094 row is
  excluded. PDF storage success/failure, retry and QQ-018 remain separate work.

Each workbook defines only its own department. Source attribution is stored on all
315 rules in `backend/scripts/seed-department-routing-rules.sql`.

## Resolution and access

Within each department, prefer a matching branch-specific rule over an all-branch
rule, then an Exact rule over an MT-series or MX-family rule. Equal-priority rules
with different workflow IDs are invalid. Full MX versions match exactly; abbreviated
families match valid versions. Explicit MT lists are not widened to whole series.

All selected departments can see the message according to their roles. If every
rule selects a no-confirmation workflow, use that workflow. Otherwise all processing
rules must select the same workflow; view-only departments do not add confirmations.
Conflicting processing workflows fail registration instead of choosing the largest
number of stages. No rule keeps the existing NoDepartmentRule/ignored-file behavior.
A matched rule with no workflow or an inactive workflow fails registration.

The importer stores one workflow ID and `MessageDepartments.CanProcess` per department
atomically. Subsequent rule edits affect new imports only. Ordinary mutations require
view and action permission in a saved processing department in the message branch.
Assignment candidates, action flags and mutation checks use the same scope. Users
whose department is view only cannot Assign, Reassign, Confirm, Undo or change workflow.
The administrator permission exceptions remain, but ViewOnly messages cannot be changed.

MT999 Frankfurt and pacs.008 Dubai share one two-confirmation cycle between CS and TFO.
For MT999/pacs.008 London, CS processes and TFO only views. The runtime has no special
case for these names: the rules and saved processing scope determine behavior.

ViewOnly is separate from New and Completed. It contributes to Total and its own
`viewOnly` count in the business dashboard, and appears separately in technical state counts and filters.
Registration writes MessageRegistered with NewState=ViewOnly, not MessageCompleted.

## Applying configuration

1. Apply the ReusableWorkflowRouting EF migration using `backend/README.md`.
2. Ensure the CS, TFO and DC departments exist, then execute
   `backend/scripts/seed-department-routing-rules.sql`. It creates missing branch
   entries, the three profiles and their rules transactionally. It is repeatable and
   does not delete messages or audit. Conflicting pre-existing common profile policies
   fail instead of silently rewriting them.
3. Deactivate obsolete sample-only routing rules before importing business messages;
   they are not an alternative routing mechanism and may conflict with the matrix.
4. Restart the importer to reload its branch/department routing snapshot.

For an explicitly requested development message reset, stop the API/importer and run
`backend/scripts/clear-imported-messages.sql` after migration. This removes messages,
assignments, reviews, message audit and ignored-file records, preserving users, roles
and configuration. Then import fresh source files with the console importer. Do not
use `seed-test-data.sql` when users and roles must be preserved.

The migration keeps existing workflow IDs, message rows and audit, gives previous
workflows LEGACY_<Id> codes, and removes old workflow scope columns/relationships.
It does not migrate old routing policies or reassign messages. Existing message
processing flags default to false; compatibility with old test workflows is out of
scope. Existing routing rules initially have a null workflow reference until configured.
Do not use the old route-in-definition model as a fallback.

`seed-test-data.sql` is a destructive, explicit demo reset; it uses three common
profiles and four illustrative routes with 100 messages. It is not the business
routing rollout script. The optional `seed-dummy-import-rules.sql` reuses
DOUBLE_CONFIRMATION for sample-only routes and never creates per-type workflows.

There is no workflow editor or configuration API. This change is backend only:
frontend, TypeScript generation and client contract checking are deferred.

## Audit and concurrency

Confirm writes ReviewStarted, ReviewApproved and, on the final confirmation,
MessageCompleted. Closing an assignment also writes MessageUnassigned. Undo retains
ConfirmationUndone and any ReviewCancelled event. Reviews record the actual actor.

Confirm requires the base64 SQL rowversion returned by details/grid. The server
checks it inside the serializable mutation transaction. Stale requests, including
Confirm → Undo → delayed Confirm, return 409. State, review, assignment and audit
changes commit or roll back together. Undo continues to target its specific reviewId.
