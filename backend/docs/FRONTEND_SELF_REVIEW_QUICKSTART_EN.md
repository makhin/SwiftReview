# Quick workflow: assign a message to yourself and confirm it

For `SINGLE_CONFIRMATION` and `DOUBLE_CONFIRMATION`, a user can assign a message to themselves and complete all confirmations. This requires access to the message's processing scope, `message.assign`, and the permission for the relevant level (`review.level1`, then `review.level2`). View access alone is insufficient. A user with `message.assign` may assign to themselves or another eligible reviewer. Only the current assignee can confirm, including administrators. The Reviewer role includes `message.assign`.

Both profiles use `Immediate`: the frontend calls `confirm`, without `start` or `approve`. The assignment is retained between levels and cleared on completion.

```text
SINGLE_CONFIRMATION: select → assign to yourself → confirm(1) → Completed
DOUBLE_CONFIRMATION: select → assign to yourself → confirm(1) → confirm(2) → Completed
```

All requests run as the same user. Local API: `http://localhost:5080`. In Development, send `X-Debug-User` with that user's username or ID. For JSON requests, send `Content-Type: application/json`. The IDs below are illustrative.

After updating the backend, apply the `RequireAssignedReviewer` migration. Existing messages do not need to be recreated.

## One-click alternative

For an **Assign to me and confirm** button, replace the separate assign and first
confirm calls with:

```http
POST /api/messages/1001/reviews/assign-and-confirm

{ "level": 1, "comment": "Looks good" }
```

Success: `204`. This assigns an unassigned message to the caller and confirms in one
transaction. If already assigned to the caller, it only confirms; if assigned to
someone else, it returns `409`. Both `message.assign` and the level permission are
required. Any failure rolls back both actions. Only Immediate workflows are supported.
For DOUBLE_CONFIRMATION, reload and explicitly confirm level 2 using either endpoint.
`canConfirm` may be false before assignment; it describes the separate confirm action.
No automatic retries after a conflict or uncertain response.

## 1. Select a message

```http
GET /api/me
```

Save `userId`, for example `21`.

```http
POST /api/messages/grid

{ "status": "New", "page": 1, "pageSize": 20 }
```

Select a message from `items`, for example `id = 1001`, with `currentAssigneeId = null`. Use its `workflowDefinitionId` and `GET /api/workflows` to check that its code is `SINGLE_CONFIRMATION` or `DOUBLE_CONFIRMATION`.

```http
GET /api/messages/1001
GET /api/messages/1001/assignment-candidates
```

Display the `body` and message details. Your own `userId` should appear among the candidates: this means the backend allows the assignment at the current stage.

## 2. Assign the message to yourself

```http
POST /api/messages/1001/assign

{ "assignedTo": 21 }
```

Only `assignedTo` is required. The server checks current permissions, eligibility and state inside the transaction.

Set `assignedTo` to your own `userId` from `/me`. Success: `204 No Content`, state `New → Assigned`.

## 3. Confirm the first level

Fetch `GET /api/messages/1001` again. Check `canConfirm == true` and use the returned `confirmationLevel`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 1,
  "comment": null
}
```

Success: `204`. No version is sent.

- `SINGLE_CONFIRMATION`: state becomes `Completed` and the assignee is cleared. Processing is complete.
- `DOUBLE_CONFIRMATION`: state becomes `WaitingForSecondReview` and the assignee remains unchanged.

## 4. Confirm the second level — DOUBLE_CONFIRMATION only

Fetch `GET /api/messages/1001` again. Check `canConfirm == true` and `confirmationLevel == 2`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 2,
  "comment": null
}
```

Success: `204`, state `Completed`, assignee cleared. No additional assign between levels or separate `/complete` call is needed.

## 5. Refresh the screen and handle errors

After each change, refresh the grid through its DevExtreme CustomStore/DataSource, the open message card, and `GET /api/messages/state-counts`. A `204` response contains no JSON.

Run commands sequentially and prevent duplicate clicks while a request is pending. On 403/409, display the error and reload the data; do not retry confirm automatically. After a network error, check the state and audit trail first: the confirmation may already have been saved.

If the message is already assigned to you, skip assign. If it is assigned to someone else, use the same `/assign` endpoint; permission and candidate checks still apply.

[Full API guide and alternative actions (Russian)](FRONTEND_MESSAGE_WORKFLOW_RU.md).

[Run the interactive TypeScript walkthrough](../../tools/workflow-demo/README.md).
