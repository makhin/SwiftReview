# API

The application uses ASP.NET Core Minimal APIs. `MapApiEndpoints` composes the
modules under `/api`. Every application endpoint requires authentication;
access to messages is evaluated against the user's permissions and exact branch
and department scope. Global administrators have administrative access.

OpenAPI is available at `/openapi/v1.json`, and the interactive reference at
`/scalar`. Operations have unique names, summaries, response schemas and tags.

Confirmation behavior is configured per workflow. The backend supports immediate
confirmation, review sessions, and workflows without confirmations. See
[Workflow configuration](WORKFLOW_CONFIGURATION.md) for settings and rollout.
The frontend and its generated TypeScript contract have not yet been updated for
the new confirmation API.

## Messages

| Method | Path | Success |
| --- | --- | --- |
| GET | `/api/messages/{id}` | 200, message details |
| POST | `/api/messages/grid` | 200, `{ "items": [...], "totalCount": 100 }` |
| POST | `/api/messages/search` | 200, paged search result |
| GET | `/api/messages/state-counts` | 200, counts by state |
| PUT | `/api/messages/{id}/workflow` | 204 |
| POST | `/api/messages/{id}/assign` | 204 |
| POST | `/api/messages/{id}/reassign` | 204 |
| GET | `/api/messages/{id}/assignment-candidates` | 200, eligible assignees |
| GET | `/api/messages/{id}/audit` | 200, paged audit events |

Assign requires an unassigned message; reassign requires an assigned message.
Both accept `{ "assignedTo": 12 }`. Workflow accepts `{ "workflowDefinitionId": 1 }`.
Search accepts `skip`, `take`, `sort` and a message `filter` in JSON; `take` is
limited to 500. Audit accepts query parameters `skip` and `take` (defaults 0 and 100).
Manual workflow changes require an active workflow with a matching message type
(or an existing supported MT-series/MX-family pattern), direction, exact set of
message departments, and matching or unrestricted branch. These checks also apply
to administrators. Review levels come from workflow configuration; no department
or message-type names are checked by the workflow engine.

### Message grid

Send a JSON body to `POST /api/messages/grid`, for example:

```json
{
  "search": "REF123",
  "status": "New",
  "messageType": "MT199",
  "branch": "1",
  "dateFrom": "2026-10-01",
  "dateTo": "2026-10-13",
  "page": 1,
  "pageSize": 20,
  "sort": [{ "field": "receivedAt", "direction": "desc" }]
}
```

Empty, null or omitted text filters impose no restriction. Filters combine with
AND. `search` matches external ID or message type; `status` is a technical
`MessageState` name, `messageType` is an exact type, and `branch` is a positive
branch ID encoded as a string. Dates accept UTC `yyyy-MM-dd` values or ISO 8601
timestamps with seconds and a timezone. Date-only `dateTo` includes the entire
UTC day; timestamp bounds are inclusive.

`page` starts at 1 and defaults to 1; `pageSize` defaults to 20 and must be 1–500.
Sorting accepts up to five clauses and defaults to `receivedAt desc, id asc`.
Allowed sort fields are `id`, `externalId`, `direction`, `messageType`, `branchId`,
`state`, `receivedAt`, `currentAssigneeId`, `activeReviewId`,
`activeReviewLevel`, `activeReviewerId` and `workflowDefinitionId`.

Optional `assignmentScope` accepts `mine`, `departments` or `assignable`; the last
requires assignment permission. The response contains complete row `items` with
action flags and `totalCount` after access checks and filters, before paging.
Unknown properties, invalid values and old DevExtreme `skip`/`take`/`filter`
requests return 400. Grouping, summaries and selected-column responses are not
supported. The frontend adapts its `CustomStore` requests to this contract.

### State counts

`GET /api/messages/state-counts` returns every technical state, including zero
counts, with a message-type breakdown:

```json
{
  "items": [
    { "state": "New", "stateName": "New", "count": 2, "breakdown": [{ "messageType": "MT199", "count": 2 }] }
  ],
  "total": { "count": 2, "breakdown": [{ "messageType": "MT199", "count": 2 }] }
}
```

The example shows one entry; the actual response includes all states. Counts
cover accessible messages independently of grid filters and `assignmentScope`.

## Reviews

All review commands use `POST /api/messages/{id}/reviews/{action}`.

| Action | Request | Success |
| --- | --- | --- |
| `confirm` | `{ "level": 1, "expectedVersion": "AAAAAAAAAAE=", "comment": "Checked" }` | 204 |
| `start` | `{ "level": 1 }` | 200, `{ "reviewId": 123 }`, no Location header |
| `approve` | `{ "level": 1, "reviewId": 123, "comment": "Checked" }` | 204 |
| `reject` | `{ "level": 1, "reviewId": 123, "comment": "Reason" }` | 204 |
| `cancel` | `{ "level": 1, "reviewId": 123 }` | 204 |
| `undo` | `{ "reviewId": 123, "comment": "Review again" }` | 204 |

`confirm` is available only for `Immediate` workflows. It creates and approves the
expected review in one transaction, including audit and assignment changes. The
message must already be assigned; the actor is checked against `review.levelN`
and `message.view` in the same permitted branch/department scope. Ownership and
reviewer reuse follow workflow settings. Confirming does not reassign the message
to the actor. Technical InProgress states are not left persisted after success.

`expectedVersion` is required: use the opaque base64 `version` returned by message
details or the grid. It represents an 8-byte SQL Server rowversion. Malformed
versions return 400; stale versions, repeated confirmation, wrong levels or modes
return 409. After an uncertain network result, reload details and the audit before
another action. Never automatically retry with a newly fetched version.

`start`, `approve`, `reject` and `cancel` are available only for `ReviewSession`
workflows. Start creates or resumes a review; decisions target a specific
`reviewId`. The original reviewer owns the active session (administrators retain
their existing access). Starting ownership and reviewer reuse are configurable.
Reject additionally requires `RejectAllowed`; if disabled, the server rejects the
operation even for administrators. Allowed rejection is final. Cancel keeps the
assignment. Assignment ends after each approval in `PerConfirmation` mode, or only
at workflow completion in `UntilWorkflowCompleted` mode.

Undo requires scoped `message.view` and `review.undo`, access to the workflow, and
an approval eligible under its undo policy. It may cancel the active next review
atomically. Undo retains the current assignment in `UntilWorkflowCompleted` mode;
`PerConfirmation` ends it. It does not recreate an assignment already ended by
final completion. A reopened immediate workflow must be assigned before confirming.
Global administrators retain the existing exception: without an active review,
they may undo the latest approval even when Undo is disabled or the approval was
final (including the single-confirmation route). With an active review, the
workflow's Undo policy applies; administrator actor restrictions remain bypassed.
`undoReviewId` in the grid is the server-authorized candidate; `null` means unavailable.

Details and grid rows expose `version`, `confirmationMode` (`ReviewSession`,
`Immediate`, `None`), `canConfirm`, `confirmationLevel` and `canReview`.
`confirmationLevel` is null when confirmation is unavailable. `canReview` applies
to legacy review sessions; `canConfirm` applies to immediate confirmation. These
flags are advisory snapshots; commands always recheck authorization and state.
Workflows without confirmations expose neither action and do not complete
messages automatically. Comments are optional and limited to 2,000 characters.

The review and workflow-change handlers execute access checks and mutations
through `ITransactionExecutor`.

## Reference data and user context

The following GET operations return 200 with their typed result:

- `/api/workflows`, `/api/users`, `/api/branches`, `/api/departments`
- `/api/message-types`, `/api/message-states`
- `/api/dashboard/summary`
- `/api/me`

`GET /api/dashboard/summary` returns `totalMessages`, `unassigned` (`New`),
`pendingFirstConfirmation` (`Assigned` and `FirstReviewInProgress`),
`pendingSecondConfirmation` (`WaitingForSecondReview` and `SecondReviewInProgress`)
and `completed` (`Completed`). Rejected and third-review messages contribute only
to `totalMessages`, so the other four counts need not sum to the total. These
counts cover accessible messages before grid filters.

## Administration

Every `/api/admin` endpoint requires the `GlobalAdministrator` policy.

| Method | Path | Success |
| --- | --- | --- |
| GET | `/api/admin/catalog` | 200, roles, permissions and scope references |
| GET | `/api/admin/users/{id}/access` | 200, assignments and effective scopes |
| PUT | `/api/admin/users/{id}/access` | 204 |
| PUT | `/api/admin/roles/{id}/permissions` | 204 |
| POST | `/api/admin/users/grid` | 200, `{ "items": [...], "totalCount": 100 }` |

The user grid accepts a JSON body with `skip` (default 0), `take` (default 20,
maximum 100), optional `search`, `sort` and `filter`. Up to three sort clauses are
allowed over `id`, `userName` and `displayName`; the default is `displayName asc,
id asc`. Search matches user name or display name and combines with the filter
using AND.

Filters use `{ "field": "userName", "operator": "contains", "value": "alex" }`
or nested groups such as `{ "logic": "or", "filters": [...] }`. Operators are
`eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `contains`, `startsWith` and `endsWith`, subject
to field type. Groups allow up to eight nesting levels (root depth 0) and 64
conditions. Unknown properties and invalid options return 400. Both grid searches
follow SQL Server collation and limit search text to 100 trimmed characters.

User access PUT replaces the complete set of scoped role assignments. Role
permissions PUT replaces the complete permission set. Changes that would prevent
completion of an active review return 409. See [Access administration](ACCESS_ADMINISTRATION.md).

## Errors and contracts

Errors use `application/problem+json`: 400 for invalid input, 401 for missing
or invalid authentication, 403 for denied access, 404 for a resource that cannot
be returned, and 409 for a business or concurrency conflict. OpenAPI specifies
which responses apply to each operation.

With the backend running on port 5080, run `npm run api:generate` in `frontend`
to generate TypeScript contracts, then `npm run api:check` to verify them.
Generated contracts must not be edited manually. Client regeneration and `api:check`
are deferred to the frontend follow-up for the confirmation API.


## Message direction

Message detail, search and grid rows expose `direction` as `"Incoming"`, `"Outgoing"`,
or `null` when the source has no direction. Workflow summaries expose the same enum,
but never `null`. Incoming/Outgoing are relative to the bank.

The message grid can sort by `direction`. Its current request has no direction
filter or grouping option; `direction` is returned in each row.
Manual workflow changes require a matching direction, including for administrators.
A mismatch or missing message direction returns `409` without changing state or audit.
The type, branch and department compatibility rules described above also apply.
