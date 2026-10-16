# Frontend message stage counts

Display stage cards or badges using counts of messages accessible to the current user. Load labels and stage metadata separately from the changing counts.

## Load metadata and counts

Call these authenticated endpoints with the same user identity as the grid:

- `GET /api/message-states` returns `{ code, label, description, reviewLevel, phase, assignedDescription }` for each state. Cache metadata for the current session. This endpoint requires message-view permission.
- `GET /api/messages/state-counts` returns `{ items, total }`, where `items` contains `{ state, stateName, count, breakdown }` only for states with a positive count, and `total` contains `{ count, breakdown }`. `total.count` is the sum of all state counts; `total.breakdown` sums each message type across all states. Each state breakdown entry is `{ messageType, count }`; its counts sum to the state total. Reload counts when the component opens and after successful assignment, review, undo or workflow actions; refresh manually or periodically if other users can change messages.

For local Development, use `X-Debug-User: admin` or a reviewer username. Full schemas are available at `/openapi/v1.json` and `/scalar`.

Example counts response:

```json
{
  "total": {
    "count": 61,
    "breakdown": [{"messageType": "M123", "count": 46}, {"messageType": "M456", "count": 15}]
  },
  "items": [
    {"state": "New", "stateName": "New", "count": 25, "breakdown": [{"messageType": "M123", "count": 10}, {"messageType": "M456", "count": 15}]},
    {"state": "Assigned", "stateName": "Assigned", "count": 5, "breakdown": [{"messageType": "M123", "count": 5}]},
    {"state": "FirstReviewInProgress", "stateName": "First review in progress", "count": 2, "breakdown": [{"messageType": "M123", "count": 2}]},
    {"state": "WaitingForSecondReview", "stateName": "Waiting for second review", "count": 4, "breakdown": [{"messageType": "M123", "count": 4}]},
    {"state": "SecondReviewInProgress", "stateName": "Second review in progress", "count": 1, "breakdown": [{"messageType": "M123", "count": 1}]},
    {"state": "WaitingForThirdReview", "stateName": "Waiting for third review", "count": 3, "breakdown": [{"messageType": "M123", "count": 3}]},
    {"state": "Completed", "stateName": "Completed", "count": 20, "breakdown": [{"messageType": "M123", "count": 20}]},
    {"state": "Rejected", "stateName": "Rejected", "count": 1, "breakdown": [{"messageType": "M123", "count": 1}]}
  ]
}
```

Use `breakdown` for a card tooltip or list of message types. Entries are sorted by message type; types with no messages in that state are omitted, and zero-count states are omitted from `items`. An absent state means zero accessible messages; with no accessible messages the response is `{ "items": [], "total": { "count": 0, "breakdown": [] } }`. On a type-entry click, set `status` and `messageType` together in the grid request.

Join by code, not array position. Render cards in metadata order, with `label` as the title and `description` as the tooltip:

```ts
const countsByState = new Map(counts.items.map(x => [x.state, x.count]));
const cards = states.map(stage => ({
  ...stage,
  count: countsByState.get(stage.code) ?? 0,
}));
```

`phase` is `Waiting`, `Assigned`, `Reviewing`, `Completed` or `Rejected`. `reviewLevel` is 1, 2 or 3 for review stages and `null` for terminal states. `assignedDescription` is optional row text when a message has an assignee; state counts cannot split a waiting state into assigned/unassigned totals.

## Optional grouping and grid navigation

For one card per review level, sum counts with the same non-null `reviewLevel`: level 1 covers `New`, `Assigned`, `FirstReviewInProgress`; level 2 covers `WaitingForSecondReview`, `SecondReviewInProgress`; level 3 covers `WaitingForThirdReview`, `ThirdReviewInProgress`. Keep `Completed` and `Rejected` as separate cards. The sum of all state counts is the total accessible message count.

On a state-card click, load `POST /api/messages/grid` with `page: 1` and the selected status:

```json
{
  "pageSize": 20,
  "page": 1,
  "status": "WaitingForSecondReview"
}
```

For a grouped card, let the user select a concrete state before opening the grid; status accepts one state. Other flat controls combine with status using AND. Replace status when another card is clicked. See [Grid integration](FRONTEND_GRID_API.md).

## Scope and display states

Counts cover all accessible messages and accept no grid filters or `assignmentScope`. They do not change when the grid is filtered to `mine`, a date range or a search term. Use the grid response's `totalCount` for the filtered result total; do not calculate overall counts from the current page. Counts and rows are separate requests and can briefly differ while messages change.

Show a loading state initially. On refresh failure, retain previous counts with an error/stale indicator rather than replacing them with zeros. Handle `401` as an authentication issue and `403` as an access issue; clear cached counts and metadata when the user changes, and ignore responses belonging to the previous user.
