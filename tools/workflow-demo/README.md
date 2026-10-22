# Workflow API demo

A small TypeScript script showing the calls needed to assign a message to yourself
and confirm it. It prints each request and response, and waits for Enter before
sending a request. Stop with Ctrl+C.

Requires **Node.js 24+**, a running Development API and a fresh test message.
No npm install or build is needed.

```bash
npm --prefix tools/workflow-demo start
```

Change the constants at the top of [workflow-demo.ts](workflow-demo.ts):

| Constant | Environment variable | Default |
|---|---|---|
| API_URL | DEMO_API_URL | `http://localhost:5080/api` |
| USER | DEMO_USER | `dc.reviewer1` (username or user ID) |
| WORKFLOW | DEMO_WORKFLOW | `SINGLE_CONFIRMATION` (code, exact name or ID) |
| MESSAGE_ID | DEMO_MESSAGE_ID | Empty: enter an ID from the displayed list |
| COMMENT | DEMO_COMMENT | `Looks good` |
| PAUSE | DEMO_PAUSE | Pauses enabled; `false` disables them |
| PAGE | DEMO_PAGE | `1` |
| PAGE_SIZE | DEMO_PAGE_SIZE | `20` (backend maximum: 500) |

For example, to demonstrate two confirmations:

```bash
DEMO_USER=cs.reviewer1 DEMO_WORKFLOW=DOUBLE_CONFIRMATION \
  npm --prefix tools/workflow-demo start
```

In PowerShell:

```powershell
$env:DEMO_USER = 'cs.reviewer1'
$env:DEMO_WORKFLOW = 'DOUBLE_CONFIRMATION'
npm --prefix tools/workflow-demo start
```

The script reads **one page** of New messages and lists those matching WORKFLOW.
If the page has no matching message, change PAGE or PAGE_SIZE. MESSAGE_ID must also
refer to a message on this page. For a non-interactive run, set both
`DEMO_PAUSE=false` and `DEMO_MESSAGE_ID`.

The sequence is visible directly in `main()`:

```text
GET /me
GET /workflows
POST /messages/grid
GET /messages/{id}
GET /messages/{id}/assignment-candidates
POST /messages/{id}/assign             ← me.userId
GET /messages/{id}
POST /messages/{id}/reviews/confirm    ← confirmationLevel + comment
GET /messages/{id}                    ← new state and available action
(repeat until Completed)
GET /messages/{id}/view
GET /messages/state-counts
```

Use an ordinary user and an Immediate workflow,
message.assign, the required review-level permissions and audit.view. The backend
checks eligibility, current assignment, level and permissions inside a transaction.
No client version is sent; RowVersion remains an internal database concurrency check. The script stops on an HTTP error without
retrying. It changes the selected test message; previous successful commands remain
saved if you stop. SINGLE_CONFIRMATION needs one confirm, DOUBLE_CONFIRMATION two.

Run against the API from the matching branch. A 405 from POST /messages/grid can
mean the running API still uses the old GET contract. Change API_URL if needed.

Run the HTTP tests with:

```bash
npm --prefix tools/workflow-demo test
```

[Short API guide](../../backend/docs/FRONTEND_SELF_REVIEW_QUICKSTART_EN.md).
