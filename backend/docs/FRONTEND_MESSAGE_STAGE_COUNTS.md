# Frontend message stage counts

Display stage cards or badges using counts of messages accessible to the current user. Load labels and stage metadata separately from the changing counts.

## Load metadata and counts

Call these authenticated endpoints with the same user identity as the grid:

- `GET /api/message-states` returns `{ code, label, description, reviewLevel, phase, assignedDescription }` for each state. Cache metadata for the current session. This endpoint requires message-view permission.
- `GET /api/messages/state-counts` returns `{ state, count }` for every state, including zeros. Reload counts when the component opens and after successful assignment, review, undo or workflow actions; refresh manually or periodically if other users can change messages.

For local Development, use `X-Debug-User: admin` or a reviewer username. Full schemas are available at `/openapi/v1.json` and `/scalar`.

Example counts response:

```json
[
  { "state": "New", "count": 12 },
  { "state": "Assigned", "count": 5 },
  { "state": "FirstReviewInProgress", "count": 2 },
  { "state": "WaitingForSecondReview", "count": 4 },
  { "state": "SecondReviewInProgress", "count": 1 },
  { "state": "WaitingForThirdReview", "count": 3 },
  { "state": "ThirdReviewInProgress", "count": 0 },
  { "state": "Completed", "count": 20 },
  { "state": "Rejected", "count": 1 }
]
```

Join by code, not array position. Render cards in metadata order, with `label` as the title and `description` as the tooltip:

```ts
const countsByState = new Map(counts.map(x => [x.state, x.count]));
const cards = states.map(stage => ({
  ...stage,
  count: countsByState.get(stage.code) ?? 0,
}));
```

`phase` is `Waiting`, `Assigned`, `Reviewing`, `Completed` or `Rejected`. `reviewLevel` is 1, 2 or 3 for review stages and `null` for terminal states. `assignedDescription` is optional row text when a message has an assignee; state counts cannot split a waiting state into assigned/unassigned totals.

## Optional grouping and grid navigation

For one card per review level, sum counts with the same non-null `reviewLevel`: level 1 covers `New`, `Assigned`, `FirstReviewInProgress`; level 2 covers `WaitingForSecondReview`, `SecondReviewInProgress`; level 3 covers `WaitingForThirdReview`, `ThirdReviewInProgress`. Keep `Completed` and `Rejected` as separate cards. The sum of all state counts is the total accessible message count.

On a state-card click, load `POST /api/messages/grid` with `skip: 0` and a state condition:

```json
{
  "take": 20,
  "skip": 0,
  "filter": { "field": "state", "operator": "eq", "value": "WaitingForSecondReview" }
}
```

For a grouped card, use an OR group of its state conditions. Combine the selected stage filter with other grid filters using AND; replace the previous stage selection when another card is clicked. See [Grid integration](FRONTEND_GRID_API.md).

## Scope and display states

Counts cover all accessible messages and accept no grid filters or `assignmentScope`. They do not change when the grid is filtered to `mine`, a date range or a search term. Use the grid response's `totalCount` for the filtered result total; do not calculate overall counts from the current page. Counts and rows are separate requests and can briefly differ while messages change.

Show a loading state initially. On refresh failure, retain previous counts with an error/stale indicator rather than replacing them with zeros. Handle `401` as an authentication issue and `403` as an access issue; clear cached counts and metadata when the user changes, and ignore responses belonging to the previous user.
