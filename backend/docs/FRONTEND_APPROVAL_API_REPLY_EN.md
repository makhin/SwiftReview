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

First call `GET /api/messages/{id}`. Use its `confirmationLevel` and `version`:

```json
{
  "level": 1,
  "expectedVersion": "<version from GET>",
  "comment": "Looks good"
}
```

Fetch the message again after every confirmation. The next confirmation requires the **new version**. On `409`, reload; do not retry automatically.

**2. Assignment requires a user ID, not a name.**

Choose an ID from `assignment-candidates`:

```json
{ "assignedTo": 1003, "expectedVersion": "AAAAAAAAAAE=" }
```

Use `/assign` for both initial assignment and reassignment. Pass the latest message `version` as mandatory `expectedVersion` (the value above is illustrative). Missing or invalid versions return 400; stale versions return 409. Reload on conflict; do not retry automatically. Self-assignment is allowed only when the workflow policy and user permissions allow it.

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
- They allow any user with the required scope and level permission to confirm—not only the assignee. An assignment must still exist.
- SINGLE does not allow ordinary-user undo. DOUBLE allows undo of the first confirmation before the second is completed.
- Use backend fields such as `canConfirm`, `confirmationLevel` and `undoReviewId` to control the buttons.

**All mutation endpoints listed above return `204 No Content`, not updated message details.** After success, reload the message, grid and counters.
