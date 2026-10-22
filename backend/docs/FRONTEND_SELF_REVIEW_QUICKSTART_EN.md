# Quick workflow: assign a message to yourself and confirm it

For `SINGLE_CONFIRMATION` and `DOUBLE_CONFIRMATION`, a user can assign a message to themselves and complete all confirmations. This requires access to the message's processing scope, `message.assign`, and the permission for the relevant level (`review.level1`, then `review.level2`). View access alone is insufficient. Self-assignment is controlled by the database setting `AllowSelfAssignment`, independently of the workflow code. The migration and seed scripts enable it for these two profiles; it can be enabled separately for other workflows.

Both profiles use `Immediate`: the frontend calls `confirm`, without `start` or `approve`. The assignment is retained between levels and cleared on completion.

```text
SINGLE_CONFIRMATION: select → assign to yourself → confirm(1) → Completed
DOUBLE_CONFIRMATION: select → assign to yourself → confirm(1) → confirm(2) → Completed
```

All requests run as the same user. Local API: `http://localhost:5080`. In Development, send `X-Debug-User` with that user's username or ID. For JSON requests, send `Content-Type: application/json`. The IDs below are illustrative.

After updating the backend, apply the `AddWorkflowSelfAssignment` migration. Existing messages do not need to be recreated.

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

{ "assignedTo": 21, "expectedVersion": "AAAAAAAAAAE=" }
```

Set mandatory `expectedVersion` to the latest message `version`; the example value is illustrative. Missing or invalid versions return 400; stale versions return 409. Reload on conflict; do not retry automatically.

Set `assignedTo` to your own `userId` from `/me`. Success: `204 No Content`, state `New → Assigned`.

## 3. Confirm the first level

Fetch `GET /api/messages/1001` again. Check `canConfirm == true` and use the returned `confirmationLevel` and fresh `version`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 1,
  "expectedVersion": "AAAAAAAAB9E=",
  "comment": null
}
```

Replace `expectedVersion` with `version` from the latest response; the example value is illustrative. Success: `204`.

- `SINGLE_CONFIRMATION`: state becomes `Completed` and the assignee is cleared. Processing is complete.
- `DOUBLE_CONFIRMATION`: state becomes `WaitingForSecondReview` and the assignee remains unchanged.

## 4. Confirm the second level — DOUBLE_CONFIRMATION only

Fetch `GET /api/messages/1001` again. Check `canConfirm == true` and `confirmationLevel == 2`, and use the **new** `version`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 2,
  "expectedVersion": "AAAAAAAAB9I=",
  "comment": null
}
```

Substitute the actual new version. Success: `204`, state `Completed`, assignee cleared. No additional assign between levels or separate `/complete` call is needed.

## 5. Refresh the screen and handle errors

After each change, refresh the grid through its DevExtreme CustomStore/DataSource, the open message card, and `GET /api/messages/state-counts`. A `204` response contains no JSON.

Run commands sequentially and prevent duplicate clicks while a request is pending. On 403/409, display the error and reload the data; do not retry confirm automatically. After a network error, check the state and audit trail first: the confirmation may already have been saved.

If the message is already assigned to you, skip assign. If it is assigned to someone else, use the same `/assign` endpoint with the latest message `version` as `expectedVersion`; permission and candidate checks still apply.

[Full API guide and alternative actions (Russian)](FRONTEND_MESSAGE_WORKFLOW_RU.md).

[Run the interactive TypeScript walkthrough](../../tools/workflow-demo/README.md).
