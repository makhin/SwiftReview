# Access administration

`/admin` (Users & access) is available only to global administrators. The API
also enforces this restriction on every `/api/admin` endpoint.

## Access model

- Ordinary users receive business permissions only through roles, with no direct grants. Global administrators bypass permission, branch/department and review-ownership checks even with no role assignments.
- Each assignment is `(user, branch, department, role)`. Multiple roles may be
  assigned to the same exact branch/department pair; their permissions are combined
  only within that pair. Separate branch and department lists are not access grants.
- Viewing a message requires `message.view` in its pair. Assigning, auditing and
  reviewing additionally require the corresponding permission in the same pair.
- `review.level1`, `review.level2` and `review.level3` each allow approval,
  rejection and cancellation at that level, only by the active review's owner. There is no
  separate global rejection permission.
- `Users.IsGlobalAdministrator` controls access administration independently of
  business roles. It grants access to all information and actions, including starting, approving, rejecting, cancelling and undoing another user's review, and self-assignment. The flag comes from server-side identity, never from an action request.
  Set this flag through trusted provisioning/seed data; no UI or API changes it.

## Screens and changes

The message assignment page (`/messages`) requires `message.assign` or `workflow.manage`.
The review queue (`/messages/assigned`) requires `message.view` and shows all visible
messages across their full lifecycle, including unassigned, completed and rejected
messages. There is a single queue; the former My work/My departments tabs are removed.
Old scope links open the full queue. All message visibility remains scoped to the
exact branch and department. Global administrators see all messages.

Review appears only when the server and current scoped rights allow the action at
the current level, including ownership and four-eyes checks. View opens the message
without starting a review. All review-stage rows use the SMBC DESIGN_GUIDE palette: level 1
Chigusa, level 2 Traditional Green, level 3 amber. Stage text and a legend accompany
colour. Assigned pending messages owned by the current reviewer show Ready for review and
the level. Other users' messages and unassigned messages have no personal highlighting
or readiness badge, including for administrators. Administrators retain their broader
action permissions.

The Users tab searches existing users and edits their scoped role assignments.
Removing all scopes removes business access. The Roles tab edits permission sets
of existing roles; changes apply to every assignment of that role. Neither screen
creates users or changes global administrator status.

The original initial migration creates the fixed permission catalog and five
starting business roles (CS Reviewer, TFO Reviewer, DC Reviewer, DC Senior Reviewer,
Operations manager). All five roles include `audit.view` for messages within their
assigned scopes. Their permissions can subsequently be edited in the UI.
Users, branches, departments and global administrators remain provisioning data.
Mock mode supplies demo reference data and an `admin` identity with explicit
business assignments; its global-administrator flag provides the same bypass even if those assignments are removed.

Both message grids expose **Undo** to global administrators and users with
`review.undo` in the message's exact branch/department scope. Ordinary users also
need `message.view` in that scope and access to the message's workflow. The server
returns `undoReviewId` only when the current confirmation is eligible; the API
rechecks permissions, policy and state inside the mutation transaction.
Undo requires confirmation with an optional comment (up to 2,000 characters),
closes the current assignment and refreshes the grid and state counts. The undo
comment is stored in its audit event; the original approval comment is preserved.
The selected review ID is sent explicitly, so retrying an old request cannot undo
an earlier level or a replacement attempt.

### Workflow undo policy

`WorkflowDefinitions` stores three independent enum values:

| Column | Values |
|---|---|
| `UndoApprovalMode` | `0 Disabled`, `1 LatestNonFinal`, `2 Latest` |
| `UndoActorMode` | `0 OriginalReviewer`, `1 AnyAuthorizedUser` |
| `UndoActiveReviewMode` | `0 Block`, `1 Cancel` |

`LatestNonFinal` allows only the latest approved required step, provided it is not
the final required step. Optional steps do not count. A single-step workflow has
no reversible confirmation in this mode; a two-step workflow allows only the
first confirmation before the second is approved. The same rule applies to three
required steps. `OriginalReviewer` additionally requires the original confirmer;
`AnyAuthorizedUser` still requires scoped permissions, not just authentication.

`Cancel` permits undo while the next required review is active. The transaction
marks that attempt `Cancelled`, marks the selected approval `Undone`, closes its
assignment, and reopens the selected level for a new manual assignment. History
is retained; `ReviewCancelled`, `ConfirmationUndone` and, when assigned,
`MessageUnassigned` record the actual actor and correlation ID. Review cancellation
and confirmation undo are parts of the same state transition. Stale decisions for
the cancelled attempt fail. `Block` requires ending the active review first.
Rejected messages cannot be reopened by Undo under either policy.

Global administrators retain an exception: without an active review they may undo
the latest approval, including the final one, even if ordinary undo is disabled.
With an active review they must satisfy the workflow's approval and cancellation
policy, but bypass scope and original-reviewer restrictions.

The `AddWorkflowUndoPolicy` migration defaults all three values to `0`, preserving
existing administrator-only behavior. Apply the migration before starting the
updated application. The existing
[`seed-test-data.sql`](../backend/scripts/seed-test-data.sql) explicitly configures the
demo workflow policies and grants `review.undo` to the demo Reviewer role. Its workflow
table is the single place to edit the sample scenarios; it also prints a readable summary.
The seed replaces all application data and is for test databases only. It is not an
upgrade/configuration procedure for an existing business database.

For the two-stage CS/TFO requirements, use `1 / 1 / 1`; the two-confirmation seed
scenario demonstrates this policy with reviewer permissions already enabled. For an
existing business database, policy changes and scoped `review.undo` grants are a
separate provisioning task. Neither department names nor role
names are hard-coded in the policy. This configuration does not change step counts,
four-eyes rules, rejection behavior, or policies of other workflows. There is no
workflow policy editor/API in this version.

Each saved user/role change appends an `AccessAuditEvents` record containing the
actor, target, timestamp, before/after state and correlation ID in the same database
transaction. Changes that remove the access needed to complete an active review
are rejected with HTTP 409. Finish or cancel the review before revoking that access.
Global administrators do not depend on role permissions, so editing their roles
does not block their active reviews.
Review start and administrative changes use serializable database transactions
to coordinate permission reads with review creation. No row-version field is used.

The initial migration was originally rewritten and is intended for a new database,
not one with the previous initial schema already applied. The subsequent
`AddWorkflowUndoPolicy` migration upgrades databases using the current initial schema.
Sync is outside this feature's scope.

### Changing a message workflow

The assignments page is available with `message.assign` or `workflow.manage`. Changing a workflow requires `workflow.manage` in the message's branch and department, with message visibility; global administrators bypass permission checks. Assignment itself still requires `message.assign`.

The Change workflow action selects an accessible active workflow as a manual override and preserves the message's branch, department and assignment. It is allowed before review, or after every review attempt has been Cancelled or Undone. Active, Approved or Rejected reviews prevent the change, including for global administrators. A tooltip on the disabled action explains this restriction, and server refusals appear in the dialog. History is preserved; MessageWorkflowChanged records the previous and new workflow IDs and actual actor.

Review-level row colours apply to all messages at that stage, independently of the
viewer, permissions or assignment. A separate Traditional Green leading stripe and
Assigned to you badge identify the current user's assignments without changing the
stage colour. Ready for review remains a personal action cue. Completed, rejected
and new messages have no review-level fill.

Both message grids share the GridStateCards radio filter above the grid. All is
selected initially. Selecting a state resets pagination and applies a server-side
filter; All removes this card filter while preserving column filters. Counts cover
all accessible messages before grid filters and paging, refresh after actions and
manual grid refresh, and periodically refresh while the screen is open. Counts are
never calculated from the current page. The API state catalog and zero-inclusive
counts are generated from MessageState, so new states appear automatically.
Cards reuse the row review-level colour tokens; unknown stages use neutral colours.
