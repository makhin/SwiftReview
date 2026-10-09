# Frontend grid integration

Use server paging, filtering and sorting for any grid component. Both endpoints accept JSON and return `{ items, totalCount }`; the old GET grid routes are removed.

| Endpoint | Rows | Access |
|---|---|---|
| `POST /api/messages/grid` | Messages and available action flags | Current user's message access |
| `POST /api/admin/users/grid` | `id`, `userName`, `displayName` | Global administrator |

Send `Content-Type: application/json` and the application's authentication headers. For local Development, use `X-Debug-User: admin` or a reviewer username. Full schemas are available at `/openapi/v1.json` and `/scalar`.

For a complete status, type, branch, date range and paging example, see [Message grid sample request](FRONTEND_GRID_SAMPLE.md).

## Load a page

Example body for `POST /api/messages/grid`:

```json
{
  "skip": 0,
  "take": 20,
  "sort": [{ "field": "receivedAt", "direction": "desc" }],
  "filter": {
    "logic": "and",
    "filters": [
      { "field": "state", "operator": "eq", "value": "Assigned" },
      { "field": "externalId", "operator": "contains", "value": "TEST" }
    ]
  }
}
```

Bind `items` to grid rows and `totalCount` to the pager. The count includes all authorized rows matching the filter, before paging. An empty or out-of-range page returns `items: []`.

- Calculate `skip = pageIndex * pageSize`, with a zero-based page index. Defaults are `skip: 0`, `take: 20`.
- Reset `skip` to `0` when filters, sorting, page size or assignment scope change. Reload from the server rather than filtering the loaded page.
- Cancel superseded requests or ignore stale responses; debounce text filters. Refresh rows after successful message actions.
- Use `id` as the row key. Message rows include `canReview`, `canChangeWorkflow`, `undoReviewId` and `requiredReviewLevels` for displaying actions and review steps. The server still checks every action.

## Build filters and sorts

A filter is a condition `{ field, operator, value }` or a non-empty group `{ logic: "and" | "or", filters: [...] }`. Groups may nest; do not mix condition and group properties. Omit `filter` or set it to `null` to clear filtering.

| Operators | Values |
|---|---|
| `eq`, `ne` | Any allowed column; `null` for nullable columns and strings |
| `gt`, `gte`, `lt`, `lte` | JSON integers or date timestamps |
| `contains`, `startsWith`, `endsWith` | Non-null strings on string columns |

Use OR groups for multiple selected values. Send enum names such as `Assigned` or `Incoming`, and dates such as `2026-10-06T08:00:00Z` with an explicit timezone. Numeric strings are rejected. Text matching follows SQL Server collation.

Message filter/sort fields: `id`, `externalId`, `direction`, `messageType`, `branchId`, `departmentId`, `state`, `receivedAt`, `currentAssigneeId`, `activeReviewId`, `activeReviewLevel`, `activeReviewerId`, `workflowDefinitionId`. User fields: `id`, `userName`, `displayName`. Action flags and `requiredReviewLevels` are response-only.

Sort clauses are applied in array order. Empty/omitted sorting defaults to `receivedAt desc` for messages and `displayName asc` for users; the server appends `id asc` unless `id` is already specified.

Limits: messages `take: 1–500`, up to 5 sorts; users `take: 1–100`, up to 3 sorts. Both allow up to 64 filter conditions and nesting depth 8 (root depth 0). Field names, operators, sort directions and group logic are case-insensitive.

## Optional scope and errors

Messages support `assignmentScope`: omit it for all accessible messages; use `mine` for the current assignee or active reviewer, `departments` for assigned messages within access (administrators also see unassigned rows), or `assignable` for eligible waiting states with assignment permission. Users support `search`, a trimmed string of up to 100 characters matching username or display name; search and filter are combined with AND.

Handle `400` as invalid options (`ProblemDetails.detail`), `401` as missing/invalid authentication, and `403` as insufficient access. Show an error state for request failures. Grouping, summaries, selected-column responses and unknown request/filter properties are unsupported.

For stage cards linked to the grid, see [Message stage counts](FRONTEND_MESSAGE_STAGE_COUNTS.md).
