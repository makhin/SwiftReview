# ORP.Sync

`ORP.Sync` is a one-shot .NET Framework 4.7.2 executable intended for Windows Task Scheduler. A run
loads `SwiftMessage` objects, routes them in C#, inserts new rows into `[orp].[SwiftMessages]` and
`[orp].[SwiftMessageEntries]`, registers eligible workflows, and advances `[orp].[SyncState]`.

## Required deployment inputs

- Supply the approved private package at restore/build time with
  `/p:SwiftPackageId=... /p:SwiftPackageVersion=...` and configure its NuGet feed. Its assembly must
  contain `Swift.SwiftQuery` or the configured equivalent.
- Set the protected `ORP` SQL Server connection string in `ORP.Sync.exe.config`.
- Configure `RoutingRules` with production BIC/message-type mappings.
- Ensure the runtime identity can select/insert in the message synchronization tables, update
  `[orp].[SyncState]`, read routing/workflow data, execute `[orp].[RegisterNewMessages]`, and acquire
  an app lock.

## Configuration

| Key | Default | Meaning |
|---|---:|---|
| `UseMockSwiftQuery` | `true` | Temporary local `PerformQuery()` mock; set to `false` in production |
| `SwiftAssemblyName` | `Swift` | Assembly containing the private library |
| `SwiftQueryTypeName` | `Swift.SwiftQuery` | Fully qualified query type |
| `InitialLookbackHours` | `24` | First-run query window |
| `OverlapMinutes` | `5` | Overlap before the last successful watermark |
| `CommandTimeoutSeconds` | `300` | SQL command timeout |
| `RoutingRules` | empty | Semicolon-separated ordered rules |

Rule format:

```text
priority|bicField|bic-or-prefix*|direction(optional)|messageTypes(comma-separated)|branchId|departmentId
```

Example with placeholder IDs/BICs:

```xml
<add key="RoutingRules"
     value="10|OwnBic|AAAAUS33*|IN|MT199,199|1|1;20|ReceiverResponder|BBBBGB22||MT700,700|2|3" />
```

Do not deploy the example unchanged. Empty rules are valid but make every imported message
`Unroutable`.

With `UseMockSwiftQuery=true`, the executable does not load the private Swift assembly. Its temporary
`PerformQuery()` implementation returns one fixed MT103 message with two normalized entry positions.
The fixed `WarehouseId` makes the first run insert the message and subsequent runs exercise duplicate
skipping. Set the option to `false` when the approved package is available.

The process exits `0` only after persistence, registration, and watermark advancement succeed.
Standard output contains counts and the correlation ID; errors are written to standard error.
Messages whose `WarehouseId` already exists are counted as skipped and are not modified.
