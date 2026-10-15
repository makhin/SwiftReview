# Operations Reporting and Processing — Backend

The backend implements the Operations Reporting and Processing domain, REST API, persistence, and automated tests. It is a modular .NET solution centred on SWIFT message assignment and multi-stage review workflows.

## Solution structure

- `src/ORP.Domain` — entities, workflow rules, review states, and domain exceptions.
- `src/ORP.Application` — use-case handlers, contracts, validation, and persistence abstractions.
- `src/ORP.Infrastructure` — Entity Framework Core persistence, authorization data, and SQL Server queries.
- `src/ORP.Importer` — portable SAA file parser (`netstandard2.0`).
- `src/ORP.Importer.Console` — one-pass recursive import of `.prt` files into SQL Server.
- `src/ORP.Scheduler` — standalone .NET Framework 4.7.2 library for preformatted text-to-PDF conversion.
- `src/ORP.Api` — ASP.NET Core endpoints, authentication, authorization, OpenAPI, health checks, and telemetry.
- `tests` — domain, application, and database-free Scheduler tests.

Dependencies point inward: the Domain project has no persistence or API dependency, Application depends on Domain contracts, and Infrastructure provides the external implementations used by the API.

## Prerequisites

- .NET SDK 10.0.400, as pinned in `global.json`.
- SQL Server to run the API. Unit tests in `ORP.sln` do not require a database.

## Run with sample data

Configure the SQL Server connection through the process environment or .NET user secrets. Development applies migrations when `BootstrapDatabase=true`; sample data is loaded explicitly using the script below:

```bash
export ConnectionStrings__ORP='Server=localhost,1433;Database=ORP;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=True'
dotnet run --project src/ORP.Api
```

The default addresses are:

- API: <http://localhost:5080>
- Scalar API reference: <http://localhost:5080/scalar>
- OpenAPI document: <http://localhost:5080/openapi/v1.json>
- Health check: <http://localhost:5080/health>

SQL data persists across process restarts. New messages remain unassigned
until a user with the `message.assign` permission assigns an eligible reviewer.

## Import SAA files

Apply the `ImportSaaMessages` migration before running the importer. This migration deletes
existing messages and their review, assignment and audit history because the previous
warehouse rows have no SAA file identity. It preserves users, reference data and workflows.

Set `ConnectionStrings__ORP` for the target database and configure the temporary routing
rules through `appsettings.json` next to the console executable or environment variables:

- `Importer__AllowedMessageTypes__0`, `Importer__AllowedMessageTypes__1`, ...: explicit
  full type names such as `MT199` or `MXpain.001.001.09`.
- `Importer__DepartmentId`: an existing department ID. The current mock department resolver
  assigns this same department to every accepted file; replace it with business rules later.

Both settings are required; their defaults are deliberately empty. Branch names are loaded
from `orp.Branches`. An active matching workflow with valid review steps must already exist.
Run from `backend`:

```bash
dotnet run --project src/ORP.Importer.Console -p:ORPLinuxBuild=true -- /path/to/SAAMessages
```

On Windows, omit `-p:ORPLinuxBuild=true` and pass a Windows or UNC directory path.

The console scans subdirectories once. It stores the parsed header, body from line four,
file name, path relative to the chosen root, and SHA-256 of the original bytes. A second
run skips unchanged paths; changed content at an existing path is reported as a conflict.
After routing succeeds, it atomically inserts `SwiftMessages`, a linked `Messages` row and
one `MessageRegistered` audit event. New messages have state `New`, no assignee, the resolved
branch/department IDs and a matching workflow. `ReceivedAt` is the UTC registration time;
`SwiftMessages.Time` retains the original timezone-free header timestamp. The API can read
new messages immediately through its existing access checks.

Workflow selection matches the full allowed type (ignoring case), direction and department.
A workflow scoped to the resolved branch takes precedence over an unscoped workflow. No
match, multiple matches at the preferred scope, missing direction or invalid reference IDs
fail the file without saving partial data. Each run reports import counts and returns a
nonzero exit code if any file has an error or conflict. A changed file never updates an
existing message; an unchanged file never adds another message or registration audit event.
CS and TFO cannot use workflows containing review level 3, including optional third steps.

`Branch`, `MessageType` and `Time` must have non-empty valid values. Invalid headers
raise `FormatException`; file errors include the path and header line number.
The parser allows other header values to be empty; registration additionally requires Direction.

Logging uses `Microsoft.Extensions.Logging` and source-generated `LoggerMessage` events,
as in the API and Infrastructure projects. The console loads `Logging:LogLevel` from its
`appsettings.json` next to the executable and environment variables. For example,
`Logging__LogLevel__ORP.Importer=Debug` enables parser diagnostics. Import outcomes and
scan totals are logged at Information, invalid files and content conflicts at Warning,
and fatal database or directory errors at Error. Logs contain file paths and hashes,
but do not dump message headers, bodies or connection strings. No file logging provider
is configured. Libraries accept optional typed loggers and use `NullLogger` by default;
the consuming application controls providers and filtering.

The console uses `SaaMessageProcessor`, which validates the file before resolving routing
fields. To enable department rules, pass an `ISaaMessageDepartmentResolver` to the
processor constructor. The resolver receives the complete `SaaMessageDocument` and must
return a positive department ID; an unresolved department raises `SaaMessageFormatException`.
At that same stage, pass an `ISaaMessageBranchResolver` to map the header's `Branch` to
`BranchId`. `SaaMessageBranchResolver` matches complete branch names, ignoring case and
surrounding whitespace. Unknown names and ambiguous names matching different IDs fail
with `SaaMessageFormatException` containing the file path and header line number. Branch
resolution runs before department rules; failed branch resolution stops processing.

An optional `ISaaMessageTypeResolver` checks the parsed `MessageType` against allowed
types at the same routing stage, before branch and department resolution. The generic
`SaaMessageTypeResolver` accepts an injected list of strings, which can be a test double
until the business source is specified. It matches full names ignoring case and surrounding
whitespace and returns the spelling from the allowed list in `SaaProcessedMessage.MessageType`.
Prefixes (`MT`/`MX`) and MX versions are preserved and must match in full. Unsupported types
raise `SaaMessageFormatException` with file path and header line number; an empty allowed
list rejects all types. The parsed header is unchanged. No production allowed list is
hard-coded, and no types are inferred from existing messages or workflows. The console
supplies the explicit configured mock list.

`SwiftMessageImporter.LoadBranchResolverAsync()` loads a snapshot of `orp.Branches`
(`Name` -> `Id`). Another application can supply its own mappings without EF dependencies
in `ORP.Importer`. Configure all three resolvers for registration:

```csharp
var branches = await importer.LoadBranchResolverAsync();
var allowedTypes = new SaaMessageTypeResolver(allowedMessageTypes); // Inject a mock list for now.
var processor = new SaaMessageProcessor(parser, departmentResolver,
    branchResolver: branches, messageTypeResolver: allowedTypes);
var processed = processor.ParseFile(path);
// processed.MessageType, processed.BranchId and processed.DepartmentId are routing results.
await importer.SaveAsync(processed, rootDirectory);
```

`SaaProcessedMessage` carries the original document and the resolved `MessageType`, `BranchId`
and `DepartmentId`. `SwiftMessageImporter.SaveAsync(processed, rootDirectory)` requires positive
routing IDs, a checked allowed type and a direction; passing output from a processor with
routing disabled is rejected before database access. The console enables all three resolvers.
Raw-only import and backfilling historical source rows are not supported. Concurrent attempts
to import the same path resolve to one committed source/message/audit, with the other attempt
reporting a skip or a content conflict.

## Load sample data into SQL Server

See [test data and named-user walkthroughs](../docs/TEST_DATA_SCENARIOS_RU.md) for
step-by-step assignment, confirmation, Undo and access-control scenarios.

Apply migrations separately, then open [scripts/seed-test-data.sql](scripts/seed-test-data.sql)
in SSMS, select the target database, and execute the whole script.

**Every run deletes all ORP application data, including users, roles, messages and audit
history, and replaces it with samples. Stop the application and other writers first.**

The script creates three branches, three departments, four reviewers and one global
administrator (`admin`), four workflows and 100
synthetic Swift messages with registration audit events. Messages start in `New` state
without an assignee. The single `Reviewer` role grants message viewing, Assign, review
levels 1–3, Undo and audit viewing. Three reviewers have access to all nine branch/department
pairs; Amelia is scoped to London/CS for access-control examples. `Operations manager`
retains all permissions; reviewers do not receive workflow management or global
administrator access. Three distinct reviewers can complete
a three-level workflow and assign messages to one another.

The seed explicitly configures four demo workflows, including their Undo policies:

| Workflow | Direction and sample scope | Confirmations | Ordinary Undo |
|---|---|---|---|
| Single confirmation - no undo | Incoming / MT199 / CS | 1 | Disabled |
| Two confirmations - undo first | Incoming / MT299 / TFO | 1, 2 | Any authorised reviewer can undo the first before the second is approved; an active second attempt is cancelled |
| Three reviews - owner undo | Incoming / MT671 / DC | 1, 2, 3 | Original reviewer can undo the latest approval, including the final one, while no review is active |
| Outgoing confirmation - no undo | Outgoing / MT299 / TFO | 1 | Disabled |

These are synthetic examples of supported policies, not the production routing or
confirmation matrix. The 100 messages (25 per scenario) cover all nine branch/department
pairs, with receipt times in the past 100 hours. Their bodies include the scenario name.
`TEST-00001` demonstrates single confirmation in London, `TEST-00002` two confirmations
in Dublin, and `TEST-00003` three reviews in Singapore. All start `New` and unassigned.
`TEST-00001`–`TEST-00075` are Incoming; `TEST-00076`–`TEST-00100` are Outgoing.
The script finishes with readable workflow and user summaries. No separate policy script
or manual permission change is needed to try Undo; schema migrations must be applied first.

To try two-step Undo, sign in as `theo.mercer`, assign `TEST-00002` to `priya.nair`,
and approve its first step as Priya. Theo can then undo Priya's approval from Message
Review. To test cancellation of an active next attempt, assign the second step to
`lucas.bennett` and start it before Theo performs Undo. To test the final-approval lock,
complete both steps instead. Global administrators retain their exception when no review
is active; see [Undo policy](../docs/ACCESS_ADMINISTRATION.md#workflow-undo-policy).

The reviewers are `amelia.hart`, `theo.mercer`, `priya.nair` and `lucas.bennett`.
Branches are London, Dublin and Singapore; departments are CS, TFO and DC.

Cleanup and loading share one transaction and roll back together on failure. Repeating
the script resets the samples, including review and assignment history. Identity IDs are
generated by SQL Server and are not reset. The script preserves schema, constraints and
migration history; it does not create the database, apply migrations or modify objects
outside `orp`. It requires SELECT/INSERT/DELETE permissions on the ORP application tables
and permission to acquire an application lock.

To subsequently run the API against these persisted samples:

```bash
dotnet run --project src/ORP.Api -- --BootstrapDatabase=false
```

## Windows / Visual Studio

`ORP.sln` is the versioned Windows/Visual Studio solution for an external SQL Server.
Its Domain and Application unit tests require no SQL connection or database creation permissions.
It has no Docker project or Docker startup dependency. Select the API's `SqlServer`
launch profile and configure `ConnectionStrings:ORP` through **Manage User Secrets**
or `ConnectionStrings__ORP` in the environment. The launch profile disables automatic
migrations; database preparation is explicit.

## Development authentication

The API uses the `X-Debug-User` request header in Development. It accepts either the numeric
user ID or username. Seeded identities include:

- `amelia.hart`
- `theo.mercer`
- `priya.nair`
- `lucas.bennett`
- `admin`

For example:

```bash
curl -H 'X-Debug-User: admin' http://localhost:5080/api/me
curl -H 'X-Debug-User: amelia.hart' http://localhost:5080/api/me
curl -H 'X-Debug-User: admin' http://localhost:5080/api/dashboard/summary
```

The frontend adds this header to API requests; use `?user=5` or `?user=admin` in its URL to
switch users. The debug authentication scheme is a development facility and must be replaced
by the deployment environment's authentication integration.

## Persistent SQL Server storage

Provide the `ORP` connection string through ASP.NET Core configuration:

```bash
export ConnectionStrings__ORP='Server=localhost,1433;Database=ORP;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=True'
export BootstrapDatabase=true
dotnet run --project src/ORP.Api
```

`BootstrapDatabase=true` applies Entity Framework Core migrations at startup only in Development. Apply migrations explicitly in other environments.

To update a database manually:

```bash
dotnet tool restore
dotnet ef database update \
  --project src/ORP.Infrastructure \
  --startup-project src/ORP.Api
```

To create a migration:

```bash
dotnet ef migrations add MigrationName \
  --project src/ORP.Infrastructure \
  --startup-project src/ORP.Api \
  --output-dir Persistence/Migrations
```

## API notes

All application endpoints are under `/api` and require authentication. The API exposes message search and detail operations, independent message and user grid endpoints, assignment and review actions, audit history, dashboard summaries, and reference data.

`GET /api/messages/{id}/audit` returns a newest-first paged history. It accepts `skip` (default `0`) and
`take` (default `100`, maximum `500`) and requires the `audit.view` permission plus access to the message's
branch and department.

Frontend integration guides: [Grid API](docs/FRONTEND_GRID_API.md) and
[Message stage counts](docs/FRONTEND_MESSAGE_STAGE_COUNTS.md).

`GET /api/messages/state-counts` returns `{ items, total }`.
`items` contains all states as `{ state, count, breakdown }`.
`total` contains `{ count, breakdown }`: `count` sums all state counts, and `breakdown` sums each message type across all states.
Each state's `breakdown` contains `{ messageType, count }` entries for accessible messages in that state;
its counts sum to the state total. Empty states return `count: 0` and `breakdown: []`.
These counts do not apply grid filters or assignment scopes.

### Grid API

`POST /api/messages/grid` accepts flat JSON controls: `search`, `status`, `messageType`, `branch`,
`dateFrom`, `dateTo`, `page` (one-based, default 1), `pageSize` (default 20, maximum 500).
Empty/null text controls impose no filter. Optional sorting and assignment scopes remain supported.
The response is `{ items, totalCount }`; authorization and filters precede counting and paging.
Old skip/take/filter message requests are rejected. Frontend adaptation is required; generated contracts are unchanged.

`POST /api/admin/users/grid` retains skip/take, sort, nested filter and optional search,
with GlobalAdministrator authorization. User take is limited to 100.

See [frontend grid integration](docs/FRONTEND_GRID_API.md),
[flat message sample and date semantics](docs/FRONTEND_GRID_SAMPLE.md), and
[stage counts](docs/FRONTEND_MESSAGE_STAGE_COUNTS.md) for contracts, limits and examples.

Request and response schemas and Problem Details are documented in OpenAPI. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to export traces and metrics through OTLP.

## Build and test

The versioned solution runs Domain and Application unit tests. No SQL Server
or Docker is required. From the repository root on
Windows, `./test-backend.ps1` runs this suite. Alternatively, from `backend`:

```bash
dotnet restore ORP.sln --configfile NuGet.Config
dotnet build ORP.sln --no-restore
dotnet test ORP.sln --no-build --no-restore
```

Unit tests can be run without SQL Server:

```bash
dotnet test tests/ORP.Domain.Tests/ORP.Domain.Tests.csproj
dotnet test tests/ORP.Application.Tests/ORP.Application.Tests.csproj
```

### Preformatted text to PDF

Open `ORP.Scheduler.sln` to work on the separate PDF library and its tests. It contains
only `ORP.Scheduler` and `ORP.Sheduler.Tests`; neither is included in `ORP.sln`.

Reference `src/ORP.Scheduler/ORP.Scheduler.csproj` from a .NET Framework 4.7.2
application. All PDF code, the `ITextToPdfConverter` interface, `TextPdfOptions`, and
the bundled font live in `ORP.Scheduler.Documents`. The library has no dependency
on the .NET 10 backend projects; the API no longer registers a PDF service.
The test project is named `ORP.Sheduler.Tests` (spelling intentional).

```csharp
using ORP.Scheduler.Documents;
using System.IO;
using System.Text;

ITextToPdfConverter converter = new ITextTextToPdfConverter();
var text = File.ReadAllText(inputPath, Encoding.UTF8);
var pdf = converter.Convert(text);
File.WriteAllBytes(outputPath, pdf);
```

Defaults are A4 portrait, 36-point margins, 10-point embedded DejaVu Sans Mono,
14-point line spacing, and eight-column tab stops. `TextPdfOptions` overrides page
dimensions, margin, font size, line spacing, and tab size; all dimensions use points
(72 per inch). The widest line determines a uniform font reduction when needed;
lines never wrap. Very wide inputs can therefore become small: choose a larger or
landscape page for readability. Spaces, blank lines, CR/LF/CRLF, and form feeds are
preserved. A trailing newline or form feed does not create an extra page. Empty text
produces one blank page. Line spacing is increased to the font's measured height if
needed to avoid overlap. The converter enables iText 5's process-wide high-precision
number formatting to keep fitted lines within the margins. Input should use characters supported by DejaVu Sans Mono;
this service targets fixed-column reports, not emoji/CJK or complex-script layout.

The converter uses NuGet package `iTextSharp` 5.5.13.6 from the iText 5.5 branch.
The package also references `itext.commons` 9.7.0 for its version/licensing metadata;
this assembly reference is missing from the upstream iTextSharp NuGet manifest, so
Scheduler declares it explicitly. PDF generation itself uses iTextSharp 5.5 APIs.
It writes text directly into PDF using the embedded font, without DevExpress,
SkiaSharp, GDI+, or an Office installation. The font and its redistribution license
are bundled in `src/ORP.Scheduler/Documents/Fonts`. Both Scheduler projects target
`net472`; `Microsoft.NETFramework.ReferenceAssemblies` supplies the build reference
assemblies. Running the tests on Windows requires .NET Framework 4.7.2 or later.
The rest of the backend continues to target .NET 10.

iText is offered under [AGPLv3 or a commercial license](https://itextpdf.com/how-buy).

`ORP.Sheduler.Tests` converts four checked-in text fixtures and reads the PDFs
with PdfPig to verify content, column coordinates, blank lines, tab stops, wide-line
fitting, and page breaks. It runs in `ORP.Scheduler.sln` without SQL Server.

From `backend`:

```powershell
dotnet build ORP.Scheduler.sln -m:1
dotnet test ORP.Scheduler.sln -m:1
```

### Transaction boundaries

Message mutation handlers execute through `ITransactionExecutor`, implemented by the scoped
`TransactionExecutor` in Infrastructure. Authorization reads and writes run together in a
Serializable transaction, including when handlers are called without HTTP. Administrative access
and role updates use the same executor. Before commit, EF Core retries the entire operation and clears tracked
state before each attempt, so access is read again. Callers must load mutable state inside the
operation; the executor owns the transaction and handlers explicitly save their changes.
Authorization checks project only the administrator flag and the two scoped permission checks in one SQL
statement. Development authentication loads identity fields only. Full access snapshots remain available
for `/me` and access-management screens. Authorization returns the loaded message, source and reviews
to the handler, so they are reused within that attempt and reloaded on retry.

`BusinessActionCommitted` is emitted only after the outer transaction commit is acknowledged.
Events from failed attempts are discarded. If commit throws (including cancellation or connection
loss), the outcome is unknown: no success event is emitted and the mutation is not automatically
replayed. The request fails; reload persisted state before deciding whether to submit it again.
This does not provide exactly-once logging: a process failure after commit can leave a committed
SQL audit event without a corresponding application log entry. See
[EF Core commit failures](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue).
