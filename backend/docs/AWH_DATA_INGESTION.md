# Swift message ingestion

## Ownership

ORP owns the complete ingestion schema. `ORP.Sync` obtains `SwiftMessage` objects from the approved
Swift library and writes only to `[orp]`; it does not create, read, or update `[swift]` or
`[dbo].[Messages]`.

| Object | Purpose |
|---|---|
| `[orp].[SwiftMessages]` | One row per `WarehouseId`; scalar Swift fields, raw JSON/body, routing result and synchronization metadata |
| `[orp].[SwiftMessageEntries]` | Positional rows for all parallel `List<>` properties except `History` |
| `[orp].[SyncState]` | Successful upper watermark for the Swift query window |
| `[orp].[Messages]` | ORP workflow state linked to `SwiftMessages.MessageId` |
| `[orp].[RegisterNewMessages]` | Idempotently registers routed source messages and creates audit events |

`SwiftMessages.MessageId` is an identity primary key. `WarehouseId` is the immutable unique external
key used to recognize retries. Source rows are retained for at least as long as their ORP workflow.

## Normalized collection rows

All list values at a common zero-based index are stored in one `SwiftMessageEntries` row. Columns
include account, currency, amount, beneficiary and ordering customer fields, sender reference,
settlement/trade/value dates, and unit data owner. The primary key is `(MessageId, Position)`.

The importer uses the greatest returned list length. When list lengths differ, absent values are
stored as `NULL` and a warning is emitted. Order and duplicates are preserved. `History` is not
persisted. Entry rows are written only when their parent message is first inserted.

## Import sequence

1. Read the last successful watermark. With no watermark, query the previous 24 hours.
2. Query from watermark minus the configured overlap (five minutes by default) to current UTC.
3. Normalize all Swift `DateTime` values as UTC and map the message plus positional entries.
4. Resolve Branch/Department using the first matching configured C# routing rule.
5. Insert previously unseen `WarehouseId` values, register eligible messages, and advance the watermark atomically.

Messages without a usable `WarehouseId` are skipped with a sanitized warning. Messages without a
routing match remain in `SwiftMessages` as `Unroutable` and are not registered.

## Routing

Routing rules are ordered configuration entries with this shape:

```text
priority|bicField|bic-or-prefix*|direction(optional)|messageTypes(comma-separated)|branchId|departmentId
```

`bicField` is `OwnBic`, `SenderRequestor`, or `ReceiverResponder`. Matching is trimmed and
case-insensitive; an eight-character BIC matches the corresponding BIC8. Rule priorities must be
unique and all referenced Branch/Department IDs must exist.

Repeated Warehouse IDs are skipped before routing. Existing payload fields, normalized entries,
Branch/Department values, and workflow state are never updated by synchronization.

## Operational guarantees

- The SQL write, collection insertion, registration, audit events, and watermark update use one
  serializable transaction.
- `sp_getapplock` prevents overlapping writers.
- A library or SQL failure returns a non-zero exit code and does not advance the watermark.
- Raw message bodies and credentials are never written to console output.
- A repeated query window is safe because `WarehouseId` is unique and registration is idempotent.

The Swift assembly is resolved at runtime by `SwiftAssemblyName`/`SwiftQueryTypeName`. Deployment
must install the approved private NuGet package so its assembly is present next to `ORP.Sync.exe`.
