# Workflow API demo

A small TypeScript CLI for trying workflow actions as different users. It prints
requests and responses and pauses before each request. All message reads use
`GET /api/messages/{id}/view`; the plain message-details endpoint is not used.

Requires **Node.js 24+**, a Development API from the matching branch and test
messages. No npm install or build is needed.

```bash
npm --prefix tools/workflow-demo start
```

Change the constants at the top of [workflow-demo.ts](workflow-demo.ts), or use:

| Environment variable | Default | Meaning |
|---|---|---|
| DEMO_API_URL | `http://localhost:5080/api` | API base URL |
| DEMO_USER | `dc.reviewer1` | Initial username or user ID |
| DEMO_WORKFLOW | `SINGLE_CONFIRMATION` | Workflow code, exact name or ID |
| DEMO_MESSAGE_ID | Empty | Choose an ID from the displayed page |
| DEMO_COMMENT | `Looks good` | Confirmation/review comment |
| DEMO_PAUSE | `true` | `false` disables pauses before HTTP requests |
| DEMO_PAGE | `1` | Grid page |
| DEMO_PAGE_SIZE | `20` | Page size, maximum 500 |
| DEMO_STATUS | Empty | All states; use `New` to show only new messages |
| DEMO_ACTIONS | Empty | Interactive menu; otherwise comma-separated commands |

The script reads one grid page and displays messages matching the workflow.
MESSAGE_ID must be on that page. Change PAGE, PAGE_SIZE or STATUS if necessary.
To run without prompts, set MESSAGE_ID, ACTIONS and `DEMO_PAUSE=false`.

## Commands

| Command | Action |
|---|---|
| `confirm` | Confirm `confirmationLevel` from `/view`, if `canConfirm` is true. Automatically assigns an unassigned level to the caller. |
| `confirm:1` | Send an explicit level to test repeated, wrong-level or forbidden requests. |
| `complete` | Confirm all available levels. Stops if a different reviewer or permission is needed. |
| `candidates` | Show eligible assignees for the current level. |
| `assign` | Assign the current level to the current user. |
| `assign:21` | Assign it to another user ID. This is an explicit reassignment when already assigned. |
| `user:username` | Switch `X-Debug-User`, reload `/me` and `/view`. A numeric user ID also works. |
| `start` | Start/resume a `ReviewSession` using the pending approval's level; remember the returned `reviewId`. |
| `approve`, `reject`, `cancel` | Act on the session returned by `start`. Reject requires the workflow to allow it. |
| `refresh` | Reload `/view`. |
| `quit` | Stop and show state counts. |

After switching users or restarting the CLI, use `start` to resume an existing
session before approving, rejecting or cancelling it. The script never uses an
audit entry ID as a review ID. `Immediate` uses `confirm`; `ReviewSession` uses
`start` followed by a decision. `None` permits reading only.

## Scenarios

Single confirmation without a separate assign:

```bash
DEMO_MESSAGE_ID=1001 DEMO_ACTIONS=confirm DEMO_PAUSE=false \
  npm --prefix tools/workflow-demo start
```

Confirm all levels as the same user (requires reviewer reuse and level permissions):

```bash
DEMO_WORKFLOW=DOUBLE_CONFIRMATION DEMO_MESSAGE_ID=1002 \
  DEMO_ACTIONS=complete DEMO_PAUSE=false npm --prefix tools/workflow-demo start
```

Two different reviewers; the second explicitly takes assignment if the workflow
retains the first reviewer's assignment:

```bash
DEMO_USER=cs.reviewer1 DEMO_WORKFLOW=DOUBLE_CONFIRMATION DEMO_MESSAGE_ID=1003 \
  DEMO_ACTIONS='confirm,user:cs.reviewer2,assign,confirm' DEMO_PAUSE=false \
  npm --prefix tools/workflow-demo start
```

Other sequences to try on suitable test messages:

```text
candidates,assign,confirm          Explicit assignment to yourself
assign:21,confirm:1                Expect 403 if user 21 is someone else
confirm,confirm:1                  Expect a conflict for the repeated first level
assign,start,approve               ReviewSession approval
assign,start,cancel,start,reject   Cancel, restart, then reject a ReviewSession
refresh,quit                      Read without changing the message
```

In PowerShell:

```powershell
$env:DEMO_USER = 'cs.reviewer1'
$env:DEMO_MESSAGE_ID = '1001'
$env:DEMO_ACTIONS = 'confirm'
$env:DEMO_PAUSE = 'false'
npm --prefix tools/workflow-demo start
```

The user needs access to the message and `audit.view` for `/view`. Mutations also
require their own permissions: `message.assign` for assignment and `review.levelN`
for confirmation. Confirming an existing own assignment does not require
`message.assign`. The backend enforces ownership and workflow policies.

`/view.status` is a display label. The demo uses `canConfirm`, `confirmationLevel`,
`canReview` and `approvals` for actions; completion means all required approvals
are `approved`. It does not assume consecutive levels such as 1, 2, 3.

Interactive errors return to the menu: use `refresh` or switch user before the next
action. Scripted sequences stop with a nonzero exit code on the first error, even
when the scenario is intended to demonstrate a denial. No request is retried
automatically. Successful earlier commands remain saved if you stop or encounter
an error. After an uncertain response, inspect the refreshed view and audit before
choosing another mutation. Ctrl+C stops the demo.

Run CLI tests against a local HTTP stub (no database changes):

```bash
npm --prefix tools/workflow-demo test
```

[Short API guide](../../backend/docs/FRONTEND_SELF_REVIEW_QUICKSTART_EN.md).
