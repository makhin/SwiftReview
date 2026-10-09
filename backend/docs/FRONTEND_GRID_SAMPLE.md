# Message grid sample request

Send the frontend controls directly as JSON to `POST /api/messages/grid` with authentication and `Content-Type: application/json`. No conversion to nested filters or skip/take is needed.

```json
{
  "search": "",
  "status": "New",
  "messageType": "MT199",
  "branch": "1",
  "dateFrom": "2026-10-01",
  "dateTo": "2026-10-09",
  "page": 1,
  "pageSize": 20
}
```

For local Development, use `X-Debug-User: admin`. First call `GET /api/branches`, find London, and replace the illustrative `branch: "1"` with its ID encoded as a string. IDs are generated and may differ.

The SQL seed creates 25 New MT199 messages, including 9 in London. Their receipt times span the 100 hours before the seed ran. The sample dates cover seed runs on October 6–9, 2026; adjust the range for another seed date. With the correct London ID and unchanged seeded messages, expect 9 rows and `totalCount: 9`. Clear `status` if messages have changed state; clear `branch` to include all branches. Omit `assignmentScope` for fresh unassigned seed data.

Save the body as `payload.json`:

```bash
curl -X POST http://localhost:5080/api/messages/grid \
  -H 'Content-Type: application/json' \
  -H 'X-Debug-User: admin' \
  --data-binary @payload.json
```

## Send frontend state directly

```ts
const controls = {
  search: "",
  status: "New",
  messageType: "MT199",
  branch: String(londonBranchId), // ID from GET /api/branches
  dateFrom: "2026-10-01",
  dateTo: "2026-10-09",
  page: 1,
  pageSize: 20,
};

const response = await fetch("/api/messages/grid", {
  method: "POST",
  credentials: "include",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(controls),
});
if (!response.ok) throw new Error(`Grid request failed: ${response.status}`);
const { items, totalCount } = await response.json();
// Bind items to rows and totalCount to the pager.
```

Use the application's existing authentication setup. The development curl example uses a debug header; production requests use the application's configured authentication.

## Control values and paging

All text controls may be omitted, null or empty to clear them. Filters combine with AND. `search` accepts up to 100 trimmed characters and performs contains matching on external ID or message type using SQL Server collation. `status` is a state name, `messageType` is an exact type, and `branch` is a positive numeric ID encoded as a string from the branch lookup, not a branch name. Send `page` and `pageSize` as JSON numbers.

Pages are one-based: defaults `page: 1`, `pageSize: 20`; pageSize accepts 1–500. Reset page to 1 when controls change. With 9 matches and pageSize 5, page 1 has 5 rows and page 2 has 4. `totalCount` remains 9; an out-of-range page has empty items.

Date-only values use UTC; both selected days are included. Explicit timestamps such as `2026-10-09T18:00:00+02:00` are also accepted and compared inclusively. Timestamps must include seconds and a timezone. `dateFrom` must not exceed `dateTo`.

Optional sorting: `"sort": [{"field":"receivedAt","direction":"desc"}]`. The default is receivedAt descending with id ascending as a stable tie-breaker. Optional assignment scopes and user-grid details: [Grid integration](FRONTEND_GRID_API.md).

Bind response `{ "items": [], "totalCount": 0 }` to rows and the pager. Invalid values and old `skip`, `take`, `filter` properties return 400 ProblemDetails. Handle 401 for authentication and 403 for insufficient access.
