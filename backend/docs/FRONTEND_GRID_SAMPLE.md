# Message grid sample request

Use `POST /api/messages/grid` to load messages with server paging, filtering and sorting. This example maps the frontend controls `status`, `messageType`, `branch`, `dateFrom`, `dateTo`, `page` and `pageSize` to the API contract.

## Request

Send `Content-Type: application/json` and the application's authentication headers. For local Development, use `X-Debug-User: admin` or a reviewer username.

The following body requests the first page of 20 messages in state `New`, of type `MT199`, in branch `1`, received October 1–9, 2026 in UTC. Results are sorted newest first.

```json
{
  "skip": 0,
  "take": 20,
  "sort": [
    { "field": "receivedAt", "direction": "desc" }
  ],
  "filter": {
    "logic": "and",
    "filters": [
      { "field": "state", "operator": "eq", "value": "New" },
      { "field": "messageType", "operator": "eq", "value": "MT199" },
      { "field": "branchId", "operator": "eq", "value": 1 },
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

Replace branch `1`, message type and dates with values matching your test data. All conditions must match because the group uses `and`; a valid request can return no rows if the data does not match or is outside the user's access.

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

For page 2 with a page size of 20, set `skip: 20`; for page 3, set `skip: 40`. If your grid uses a zero-based page index, use `skip = pageIndex * pageSize`. Reset `skip` to `0` when filters, sorting or page size change.

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
