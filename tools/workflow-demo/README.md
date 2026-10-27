# Workflow API demo

A small TypeScript CLI for trying workflow actions as different users. It prints
requests and responses and pauses before each request. All message reads use
`GET /api/messages/{id}/view`; the plain message-details endpoint is not used.

Requires **Node.js 24+**, a Development API from the matching branch and test
messages. No npm install or build is needed.

This is a teaching example: settings, IDs and commands are assumed to be correct.
It does not validate input or command order. The API checks permissions and
workflow rules. Any runtime or HTTP error ends the demo.

```bash
npm --prefix tools/workflow-demo start
```

## First run, step by step

1. Start the Development API and run the command above.
2. Press Enter before each HTTP request. The script prints the request first,
   then sends it and prints the response.
3. The script reads your user (`/me`), the workflows (`/workflows`) and one page
   of messages (`/messages/grid`). Enter a message ID from the displayed table.
4. The script reads `/messages/{id}/view`. This response tells you what the
   current user can do with this message.
5. At `Action:`, enter `confirm` to confirm one level. The default workflow uses
   this command. The script sends the confirmation and reads `/view` again.
6. Enter `quit` to show the counters and exit.

There are two ways to confirm a message, depending on its workflow:

| Workflow mode | Commands to enter, one at a time | What happens |
|---|---|---|
| `Immediate` | `confirm` | Confirm one level. The server also assigns it to you if it is unassigned. |
| `ReviewSession` | `assign`, then `start`, then `approve` | Assign yourself, open a review session, then approve that session. |

`complete` repeats `confirm` while `/view.canConfirm` is true. When it becomes
false, the command ends normally: all levels may be approved, or the next level
may need another reviewer. Read the printed `/view` response to see which.
A workflow with mode `None` is read-only.

## How to read the code

Open [workflow-demo.ts](workflow-demo.ts) and start with `main()`. Its numbered
steps follow the same order as the first run above:

1. Find the user and workflow.
2. Load and display the messages.
3. Select a message and read `/view`.
4. Read a command and execute its `case` in the `switch`.
5. Read the counters and finish.

Each command's `case` shows its HTTP requests and the data sent to the API.
`confirm` and `complete` share `confirmMessage()`: it sends one confirmation and
reads the updated `/view`. It gets the next level from the server.

The three pieces of changing data are `currentUserDetails` (the caller),
`message` (the latest `/view` response) and `activeReview` (the session returned
by `start`). Switching users reloads the caller and message. Run `start` as the
new user before using a review-session command.

The `request()` helper at the top only prints, pauses and sends HTTP requests.
It throws an error for unsuccessful responses; it never retries a request.

## Settings

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
Choose an ID from that page, or set MESSAGE_ID to an already known message ID.
To run without prompts, set MESSAGE_ID, ACTIONS and `DEMO_PAUSE=false`.

## Commands

| Command | Action |
|---|---|
| `confirm` | Send `confirmationLevel` from `/view` to the API. The server checks whether confirmation is allowed and assigns an unassigned level to the caller. |
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

`/view.status` is a display label. The demo gets each confirmation level from
`confirmationLevel` and the review-session level from the pending entry in
`approvals`. It does not assume consecutive levels such as 1, 2, 3.

Both interactive and scripted runs stop with a nonzero exit code on the first
error. No request is retried automatically. Successful earlier commands remain
saved. Ctrl+C stops the demo.

Run CLI tests against a local HTTP stub (no database changes):

```bash
npm --prefix tools/workflow-demo test
```

[Short API guide](../../backend/docs/FRONTEND_SELF_REVIEW_QUICKSTART_EN.md).
