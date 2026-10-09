# Frontend grid integration

Use server paging, filtering and sorting for any grid component. Both endpoints accept JSON and return `{ items, totalCount }`; the old GET grid routes are removed.

| Endpoint | Rows | Access |
|---|---|---|
| `POST /api/messages/grid` | Messages and available action flags | Current user's message access |
| `POST /api/admin/users/grid` | `id`, `userName`, `displayName` | Global administrator |

Send `Content-Type: application/json` and the application's authentication headers. For local Development, use `X-Debug-User: admin` or a reviewer username. Full schemas are available at `/openapi/v1.json` and `/scalar`.

For a complete status, type, branch, date range and paging example, see [Message grid sample request](FRONTEND_GRID_SAMPLE.md).

## Load a page

Messages use the flat controls described in [Message grid sample request](FRONTEND_GRID_SAMPLE.md):
`search`, `status`, `messageType`, `branch`, `dateFrom`, `dateTo`, `page`, `pageSize`.
Empty text controls mean no filter. Page numbers start at 1; pageSize defaults to 20 and accepts 1–500.
Optional `sort` and `assignmentScope` remain supported. The old skip/take/filter message body is rejected.

Bind `items` to rows and `totalCount` to the pager. Reset page to 1 when controls change.
Cancel superseded requests and refresh after message actions. Use id as the row key; action flags and review levels remain in the response.

Users still use `{ "skip": 0, "take": 20, "search": "Amy", "sort": [{"field":"displayName","direction":"asc"}] }`.
User skip is non-negative, take is 1–100; reset skip to 0 when filters change.

## User filters and shared sorting

For the user grid only, a filter is a condition `{ field, operator, value }` or a non-empty group `{ logic: "and" | "or", filters: [...] }`. Groups may nest; do not mix condition and group properties. Omit `filter` or set it to `null` to clear filtering.

| Operators | Values |
|---|---|
| `eq`, `ne` | Any allowed column; `null` for nullable columns and strings |
| `gt`, `gte`, `lt`, `lte` | JSON integers on `id` |
| `contains`, `startsWith`, `endsWith` | Non-null strings on string columns |

Use OR groups for multiple selected values. User fields are numeric `id` and string `userName`/`displayName`; numeric strings are rejected for `id`. Text matching follows SQL Server collation.

Message sort fields: `id`, `externalId`, `direction`, `messageType`, `branchId`, `departmentId`, `state`, `receivedAt`, `currentAssigneeId`, `activeReviewId`, `activeReviewLevel`, `activeReviewerId`, `workflowDefinitionId`. User fields: `id`, `userName`, `displayName`. Action flags and `requiredReviewLevels` are response-only.

Sort clauses are applied in array order. Empty/omitted sorting defaults to `receivedAt desc` for messages and `displayName asc` for users; the server appends `id asc` unless `id` is already specified.

Limits: messages `pageSize: 1–500`, up to 5 sorts; users `take: 1–100`, up to 3 sorts. The user grid allows up to 64 filter conditions and nesting depth 8 (root depth 0). Field names, operators, sort directions and group logic are case-insensitive.

## Optional scope and errors

Messages support `assignmentScope`: omit it for all accessible messages; use `mine` for the current assignee or active reviewer, `departments` for assigned messages within access (administrators also see unassigned rows), or `assignable` for eligible waiting states with assignment permission. Users support `search`, a trimmed string of up to 100 characters matching username or display name; search and filter are combined with AND.

Handle `400` as invalid options (`ProblemDetails.detail`), `401` as missing/invalid authentication, and `403` as insufficient access. Show an error state for request failures. Grouping, summaries, selected-column responses and unknown request/filter properties are unsupported.

For stage cards linked to the grid, see [Message stage counts](FRONTEND_MESSAGE_STAGE_COUNTS.md).
