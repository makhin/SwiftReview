# Interactive API workflow demo

A TypeScript walkthrough for frontend developers. It prints the HTTP method, URL,
request JSON, status and response, and waits for Enter before each request. Grid
responses show only the fields needed for selection; other responses are printed
in full. All assignment and confirmation calls use the same ordinary user.

Requires **Node.js 24+**, the running Development API and an existing test message.
Node runs the TypeScript directly; no npm install or build is needed.

```bash
npm --prefix tools/workflow-demo start
```

Edit the `CONFIG` block at the top of [workflow-demo.ts](workflow-demo.ts), or use
environment variables:

| Setting | Environment variable | Default / meaning |
|---|---|---|
| API URL | `DEMO_API_URL` | `http://localhost:5080/api` |
| User | `DEMO_USER` | `dc.reviewer1`; username or numeric ID |
| Workflow | `DEMO_WORKFLOW` | `SINGLE_CONFIRMATION`; code, exact name or ID |
| Message | `DEMO_MESSAGE_ID` | Empty: list matching New messages and prompt for ID |
| Comment | `DEMO_COMMENT` | Text saved with each confirmation |
| Pause | `DEMO_PAUSE` | `true`; set `false` to run without pauses, with an explicit message ID |

Examples from the repository root:

```bash
DEMO_USER=dc.reviewer1 DEMO_WORKFLOW=SINGLE_CONFIRMATION \
  npm --prefix tools/workflow-demo start

DEMO_USER=cs.reviewer1 DEMO_WORKFLOW=DOUBLE_CONFIRMATION \
  npm --prefix tools/workflow-demo start
```

In PowerShell, set variables before starting:

```powershell
$env:DEMO_USER = 'cs.reviewer1'
$env:DEMO_WORKFLOW = 'DOUBLE_CONFIRMATION'
npm --prefix tools/workflow-demo start
```

The program changes the selected message. Enter `q` at a request prompt to stop;
previously saved actions remain committed. It does not create, reset, reject, undo
or reassign messages belonging to other users. The current user must have the
necessary scope and assignment/review permissions, and the workflow must allow
self-assignment. Final audit display also requires `audit.view`.

## What it demonstrates

1. `/me` resolves the user ID and checks that this is not an administrator.
2. `/workflows` resolves the selected workflow and its required levels.
3. `POST /messages/grid` reads accessible pages. Because this API has no workflow
   filter, the demo matches `workflowDefinitionId` locally.
4. `/messages/{id}` displays the message and its current capabilities.
5. `/assignment-candidates` checks self-assignment eligibility; `/assign` uses the
   ID from `/me`. If already assigned to this user, assignment is skipped.
6. A fresh GET supplies `confirmationLevel` and `version` for `/reviews/confirm`.
7. Repeat using fresh backend values until `Completed`; never increment the level
   locally or reuse a previous version. If the policy clears assignment between
   levels, the demo checks candidates and assigns again.
8. `/view` displays approvals and audit, then `/messages/state-counts` refreshes counters.

The demo supports active **Immediate** workflows with any code, name or ID. It does
not depend on SINGLE/DOUBLE codes or assume two consecutive levels. ReviewSession
and ViewOnly are detected and rejected before mutation. For ReviewSession, see the
[full workflow guide](../../backend/docs/FRONTEND_MESSAGE_WORKFLOW_RU.md).

A `405` from `POST /messages/grid` means the running API does not expose the current
branch's grid contract. Start the matching backend build or set `DEMO_API_URL` to it.

Requests are sequential and never retried automatically. A 403/409 or network error
stops execution. Inspect the saved message and audit before restarting: a request
may have succeeded even when its response was lost. A completed message can be
inspected by supplying its ID, but cannot be used for another fresh workflow run.

This is an HTTP teaching example, not a replacement for the frontend's DevExtreme
CustomStore. The server-backed UI grid should retain its existing loading mechanism.
The `X-Debug-User` header is for Development only.

## Tests

```bash
npm --prefix tools/workflow-demo test
```

Tests use a local HTTP fixture, not the dev database. They verify single/double and
nonconsecutive levels, fresh versions, paged selection, ordinary-user enforcement,
ineligible self-assignment, another assignee, stopping on conflict and manual cancellation.

[Short English API guide](../../backend/docs/FRONTEND_SELF_REVIEW_QUICKSTART_EN.md).
