# Message grid sample request

Use `POST /api/messages/grid` to load messages with server paging, filtering and sorting. This example maps the frontend controls `status`, `messageType`, `branch`, `dateFrom`, `dateTo`, `page` and `pageSize` to the API contract.

## Request

Send `Content-Type: application/json` and the application's authentication headers. For local Development, use `X-Debug-User: admin` or a reviewer username.

The following body matches a freshly loaded `backend/scripts/seed-test-data.sql`: the seed creates 25 `MT199` messages, all initially in state `New`. The date range below covers seed runs on October 6–9, 2026 in UTC. For those runs, with `X-Debug-User: admin`, expect `totalCount: 25` and 20 rows on the first page, sorted newest first, provided the messages have not subsequently been assigned or reviewed. Adjust the date range for seeds generated on other dates.

Start without a branch filter: branch IDs are generated and are not reset on reseeding. Receipt dates are relative to the time the seed script ran; its messages span the 100 hours up to that time.

```json
{
  "skip": 0,
  "take": 20,
  "sort": [
    {
      "field": "receivedAt",
      "direction": "desc"
    }
  ],
  "filter": {
    "logic": "and",
    "filters": [
      {
        "field": "state",
        "operator": "eq",
        "value": "New"
      },
      {
        "field": "messageType",
        "operator": "eq",
        "value": "MT199"
      },
      {
        "field": "receivedAt",
        "operator": "gte",
        "value": "2026-10-01T00:00:00Z"
      },
      {
        "field": "receivedAt",
        "operator": "lt",
        "value": "2026-10-10T00:00:00Z"
      }
    ]
  }
}
```

Save the body as `payload.json`, then test locally:

```bash
curl -X POST http://localhost:5080/api/messages/grid \
  -H 'Content-Type: application/json' \
  -H 'X-Debug-User: admin' \
  --data-binary @payload.json
```

For a smoke test after messages have changed state, remove the `state` condition and keep `messageType eq MT199`. The original 25 messages will still match if their receipt times fall within the selected date range and they have not been deleted or replaced.

## Add a branch filter

Use a `branchId` from one of the returned messages, or look up the current IDs with `GET /api/branches`. Append a condition to the existing `filter.filters` array, replacing the illustrative ID below with an actual seed branch ID:

```json
{ "field": "branchId", "operator": "eq", "value": 1 }
```

All conditions must match because the group uses `and`; a branch filter reduces the total below 25. Do not set `assignmentScope: "mine"` for a fresh-seed test: all seeded messages are unassigned.

## Frontend parameter mapping

| Frontend control | API representation |
|---|---|
| `status` | Condition on `state`, using an enum name such as `New`, `Assigned` or `Completed` |
| `messageType` | Condition on `messageType`, e.g. `MT199`; available values come from `GET /api/message-types` |
| `branch` | Condition on numeric `branchId`; use an ID from `GET /api/branches` |
| `dateFrom` | `receivedAt gte` the start of the selected first day |
| `dateTo` | `receivedAt lt` the start of the day after the selected last day |
| `page` | For one-based pages, `skip = (page - 1) * pageSize` |
| `pageSize` | `take`, from 1 to 500 |

For page 2 with a page size of 20, set `skip: 20` (5 rows for the fresh-seed example); page 3 with `skip: 40` is empty, while `totalCount` remains 25. If your grid uses a zero-based page index, use `skip = pageIndex * pageSize`. Reset `skip` to `0` when filters, sorting or page size change.

The date range is inclusive at the start and exclusive at the end, so it includes the entire selected end date. Dates must contain an explicit timezone (`Z` or an offset such as `+02:00`). For local calendar days, convert each boundary using the selected timezone; offsets can differ across a daylight-saving change.

Remove conditions for controls the user has not selected. If no filters are selected, omit `filter` or set it to `null`. Frontend control names such as `status`, `branch`, `page` and `dateFrom` are not top-level API properties.

## Response and errors

The response contains `items` for the current page and `totalCount` for all accessible messages matching the filters. Bind these to the grid rows and pager. An empty result is:

```json
{
  "items": [],
  "totalCount": 0
}
```

An out-of-range page can also return `items: []` with a nonzero `totalCount`. The server adds `id asc` as a tie-breaker unless sorting already specifies `id`.

A general `search` parameter is not supported by the messages grid. Do not send `search` or `query` in the body: unknown properties return `400`. For a specific text field, an optional condition such as `{ "field": "externalId", "operator": "contains", "value": "TEST" }` is supported.

Handle `400` as invalid request options, `401` as an authentication issue and `403` as insufficient access. Error bodies use Problem Details. For additional fields, operators and assignment scopes, see [Frontend grid integration](FRONTEND_GRID_API.md).
