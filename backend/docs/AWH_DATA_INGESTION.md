# Swift message ingestion

## Ownership

ORP owns the ingestion schema in `[orp]`. The repository currently contains no production
importer. Source integration and routing must be implemented separately; the API reads
source records without modifying them. Sample development data can be loaded with
[`seed-test-data.sql`](../scripts/seed-test-data.sql).

| Object | Purpose |
|---|---|
| `[orp].[SwiftMessages]` | One row per `WarehouseId`; scalar Swift fields, raw JSON/body, routing result and synchronization metadata |
| `[orp].[SwiftMessageEntries]` | Positional rows for all parallel `List<>` properties except `History` |
| `[orp].[SyncState]` | Successful upper watermark for the Swift query window |
| `[orp].[Messages]` | ORP workflow state linked to `SwiftMessages.MessageId` |

`SwiftMessages.MessageId` is an identity primary key. `WarehouseId` is the immutable unique external
key used to recognize retries. Source rows are retained for at least as long as their ORP workflow.

## Normalized collection rows

All list values at a common zero-based index are stored in one `SwiftMessageEntries` row. Columns
include account, currency, amount, beneficiary and ordering customer fields, sender reference,
settlement/trade/value dates, and unit data owner. The primary key is `(MessageId, Position)`.

The schema retains source data and synchronization metadata for external ingestion.
The database objects remain managed by the existing migrations. Import scheduling,
source-library configuration, routing and watermark advancement are not implemented
by the current application.
