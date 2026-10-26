# Approval API: existing endpoints and required requests

These operations already exist. Please use the following endpoints:

| Your proposal | Existing endpoint |
|---|---|
| Approve | `POST /api/messages/{id}/reviews/confirm` |
| Assign / reassign | `POST /api/messages/{id}/assign` |
| Undo approval | `POST /api/messages/{id}/reviews/undo` |
| List reviewers | `GET /api/messages/{id}/assignment-candidates` |
| My message counts | `GET /api/messages/state-counts?assignmentScope=mine` |

**1. Confirm needs more than a comment.**

First call `GET /api/messages/{id}`. Use its `confirmationLevel`:

```json
{
  "level": 1,
  "comment": "Looks good"
}
```

Fetch the message again after every confirmation. Use the returned next level and check `canConfirm`. On `409`, reload; do not retry automatically.

**2. Assignment requires a user ID, not a name.**

Choose an ID from `assignment-candidates`:

```json
{ "assignedTo": 1003 }
```

Use `/assign` for both initial assignment and reassignment. Send only `assignedTo`. The server checks current permissions, eligibility and state. Reload on conflict; do not retry automatically. Self-assignment uses the same scope, level-permission and reviewer-reuse checks as assignment to another user.

**3. Undo requires the approval ID.**

Use `undoReviewId` from the message grid:

```json
{
  "reviewId": 501,
  "comment": "Wrong amount"
}
```

The comment is optional. The body and `reviewId` are required.

**4. Workflow rules matter.**

- SINGLE needs one confirmation. DOUBLE needs two.
- These profiles use `confirm`; do not call `start` or `approve`.
- Only the current assignee with the required scope and level permission can confirm. This also applies to administrators.
- SINGLE does not allow ordinary-user undo. DOUBLE allows undo of the first confirmation before the second is completed.
- Use backend fields such as `canConfirm`, `confirmationLevel` and `undoReviewId` to control the buttons.

**All mutation endpoints listed above return `204 No Content`, not updated message details.** After success, reload the message, grid and counters.

Assignment requires `message.assign` (included in the Reviewer role). You can assign to yourself or another eligible reviewer. Confirmation needs the current level permission. For an unassigned message, it also requires `message.assign` and assigns the caller atomically. Reassignment immediately removes the previous assignee's ability to confirm.

For a one-click **Assign to me and confirm** action, use
`POST /api/messages/{id}/reviews/confirm` with `{ "level": 1, "comment": "Looks good" }`.
It requires the level permission and, only when unassigned, `message.assign`. It assigns
an unassigned message to the caller or confirms an existing own assignment. Another assignee returns
403. Both actions and audit commit or roll back together; success is 204. Immediate
workflows only. `canConfirm` includes automatic self-assignment and `confirmationLevel`
identifies the available level. Refresh after success and do not retry automatically.
