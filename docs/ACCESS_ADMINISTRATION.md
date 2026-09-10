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

The assignment grid exposes **Undo** only to global administrators. The API also
requires a global administrator for `/api/messages/{id}/undo` at this stage;
`review.undo` alone does not grant access yet. The button undoes the latest approved
level, requires confirmation with an optional comment (up to 2,000 characters), closes the current assignment and refreshes the grid.
The undo comment is stored in its audit event; the original approval comment is preserved.
It is disabled while a review is active or no approval can be undone. The selected
review ID is sent explicitly, so retrying an old request cannot undo an earlier level.

Each saved user/role change appends an `AccessAuditEvents` record containing the
actor, target, timestamp, before/after state and correlation ID in the same database
transaction. Changes that remove the access needed to complete an active review
are rejected with HTTP 409. Finish or cancel the review before revoking that access.
Global administrators do not depend on role permissions, so editing their roles
does not block their active reviews.
Review start and administrative changes use serializable database transactions
to coordinate permission reads with review creation. No row-version field is used.

The initial migration was rewritten, not extended with an upgrade migration.
It is intended for a new database, not one with the previous schema already applied.
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
