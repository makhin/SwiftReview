# API

The application uses ASP.NET Core Minimal APIs. `MapApiEndpoints` composes the
modules under `/api`. Every application endpoint requires authentication;
access to messages is evaluated against the user's permissions and exact branch
and department scope. Global administrators have administrative access.

OpenAPI is available at `/openapi/v1.json`, and the interactive reference at
`/scalar`. Operations have unique names, summaries, response schemas and tags.

## Messages

| Method | Path | Success |
| --- | --- | --- |
| GET | `/api/messages/{id}` | 200, message details |
| GET | `/api/messages/grid` | 200, DevExtreme load result |
| POST | `/api/messages/search` | 200, paged search result |
| GET | `/api/messages/state-counts` | 200, counts by state |
| PUT | `/api/messages/{id}/workflow` | 204 |
| POST | `/api/messages/{id}/assign` | 204 |
| POST | `/api/messages/{id}/reassign` | 204 |
| GET | `/api/messages/{id}/assignment-candidates` | 200, eligible assignees |
| GET | `/api/messages/{id}/audit` | 200, paged audit events |

Assign requires an unassigned message; reassign requires an assigned message.
Both accept `{ "assignedTo": 12 }`. Workflow accepts `{ "workflowDefinitionId": 1 }`.
Search accepts paging, sorting and filters in JSON. Grid accepts DevExtreme query
options; the frontend loads it through `CustomStore`. Audit accepts `skip` and `take`.

## Reviews

All review commands use `POST /api/messages/{id}/reviews/{action}`.

| Action | Request | Success |
| --- | --- | --- |
| `start` | `{ "level": 1 }` | 200, `{ "reviewId": 123 }`, no Location header |
| `approve` | `{ "level": 1, "reviewId": 123, "comment": "Checked" }` | 204 |
| `reject` | `{ "level": 1, "reviewId": 123, "comment": "Reason" }` | 204 |
| `cancel` | `{ "level": 1, "reviewId": 123 }` | 204 |
| `undo` | `{ "reviewId": 123, "comment": "Review again" }` | 204 |

Start creates or resumes a review. Decisions target a specific attempt using
`reviewId`. Review ownership, level permissions and four-eyes rules apply.
Undo requires a global administrator and a confirmation eligible for cancellation.
Comments are optional and limited to 2,000 characters.

`MessageMutationTransactionFilter` covers start, approve, reject, cancel and
workflow changes, keeping their access reads and mutations in a transaction.

## Reference data and user context

The following GET operations return 200 with their typed result:

- `/api/workflows`, `/api/users`, `/api/branches`, `/api/departments`
- `/api/message-types`, `/api/message-states`
- `/api/dashboard/summary`
- `/api/me`

## Administration

Every `/api/admin` endpoint requires the `GlobalAdministrator` policy.

| Method | Path | Success |
| --- | --- | --- |
| GET | `/api/admin/catalog` | 200, roles, permissions and scope references |
| GET | `/api/admin/users/{id}/access` | 200, assignments and effective scopes |
| PUT | `/api/admin/users/{id}/access` | 204 |
| PUT | `/api/admin/roles/{id}/permissions` | 204 |
| GET | `/api/admin/users/grid` | 200, user load result |

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
Generated contracts must not be edited manually.
