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

## Local iText NuGet packages

`packages/pdf/` contains the `.nupkg` archives for `itext`, `itext.commons` and
`itext.bouncy-castle-adapter`, all version 9.7.0, including upstream licenses and
notices. Keep these archives in Git.

`NuGet.Config` maps these three package IDs exclusively to this local directory.
Normal `dotnet restore` and Visual Studio restore use it automatically. All other
packages, including BouncyCastle.Cryptography, PdfPig, Scheduler's iTextSharp and
other transitive dependencies, use nuget.org. A fresh restore still requires
network access for those dependencies and NuGet vulnerability auditing.

`packages/pdf/SHA256SUMS` records the archive hashes. To check them on macOS/Linux:

```bash
(cd packages/pdf && shasum -a 256 -c SHA256SUMS)
```

When upgrading iText, update its three archives, the central package versions and
`SHA256SUMS` together. Verify restore with a fresh package cache and confirm that
the iText packages are restored from the repository's local source.

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

Apply migrations before importing. The historical `ImportSaaMessages` migration deletes
previous warehouse messages and their review, assignment and audit history because they
have no SAA file identity. The `MultiDepartmentRouting` migration moves existing message
and workflow assignments into relationship tables and removes routing priority. To start
message imports afresh, stop the API and importer, then run `scripts/clear-imported-messages.sql`
manually after the migration. It keeps users, roles, branches, Departments, rules, and workflows.

For Development, configure `ConnectionStrings:ORP` in the importer's
`appsettings.Development.json` or ignored `appsettings.Importer.local.json` and set
`DOTNET_ENVIRONMENT=Development`. Keep credentials in the ignored local file.
The default connection string is empty; the shared `appsettings.json` has no connection string.
An explicit `ConnectionStrings__ORP` environment variable overrides configuration in any environment. Run
`scripts/seed-department-routing-rules.sql` against that database to add the required
branches and initial Department rules. The script is transactional and safe to rerun.
If you reload demo data with `scripts/seed-test-data.sql`, run the routing script again
afterward: demo data resets the routing rules and ignored-file list along with its other data.

There is no allowed-message-type list. A non-empty `MessageType` from the header is matched
against all active Department rules, ignoring case. Every matched Department receives the
same message; repeated rules for one Department do not duplicate it. Empty or invalid active
rule sets stop the importer at startup. If no rule matches, the file is ignored as before.

MX rule patterns use the full SAA header identifiers: `MXpacs.008` for a family or
`MXcamt.055.001.08` for a specific version. The initial seed adds `MX` to the workbook
identifiers. For a database loaded with the earlier unprefixed rules, run
`scripts/fix-department-routing-mx-prefix.sql` before starting the updated importer.
This repeatable upgrade preserves rule IDs and settings. Use `--retry-ignored` to
reconsider previously ignored files afterward.

Rule validation accepts exact `MTnnn` or MX identifiers, MT series such as `MT7xx`,
and MX families/versions with a four-letter name, three-digit message number and optional
three-digit/two-digit version suffix. Invalid patterns, including unsupported wildcards,
fail startup instead of causing unmatched messages to be ignored.

Workflow definitions now contain reusable behavior only. Apply `ReusableWorkflowRouting`
and run `scripts/seed-department-routing-rules.sql` to link the 315 matrix rules to
VIEW_ONLY (49), SINGLE_CONFIRMATION (96) and DOUBLE_CONFIRMATION (170).
Within each Department, branch-specific rules win, followed by exact types over
series/families. Equally preferred conflicting policies fail. All selected Departments
retain visibility; processing Departments must agree on one confirmation workflow.
View-only Departments do not add stages. The engine supports Review3 without checking
Department names. See [Workflow configuration](../docs/WORKFLOW_CONFIGURATION.md).

The migration preserves existing message/workflow IDs and audit, but does not migrate
legacy test routing or reassign old messages. Old definitions receive LEGACY_<Id> codes;
existing message processing flags default to false. New routing rules require a workflow
reference. Restart the importer after configuration changes. Do not leave conflicting
legacy/test rules active alongside the approved matrix.

For testing the additional sample types outside the approved matrix, optionally run
`scripts/seed-dummy-import-rules.sql` after the approved routing seed. This manual,
transactional script adds eight branch/type combinations in both directions with
temporary CS routing and source `dummy-import-testing-2026-10-16`.
It reuses DOUBLE_CONFIRMATION without creating more definitions. Incoming
`MXcamt.055.001.08` for London already uses the approved matrix. Directory entries must already exist and
must be unambiguous. Rerunning the script adds no duplicates. These are dummy test
assignments, not approved Department rules; the script is not run by migrations or startup.
Run the importer with `--retry-ignored` after applying it to reconsider cached ignored files.

Each file must begin with the import metadata line
`Branch:...|MessageType:...|Reference#:...|DIRECTION:...|SENDER:...|RECEIVER:...|Host:...|Time:...`,
followed by the SAA print timestamp and separator. Raw SAA print reports starting directly
with a print timestamp are rejected with `no import metadata header`; branch and routing
metadata are not inferred from printer codes or message text.

`Importer__DefaultDirection` optionally supplies `Incoming` or `Outgoing` for empty header
directions and never overrides an explicit direction. The original header stays unchanged.
An ignored `src/ORP.Importer.Console/appsettings.Importer.local.json` can hold local Development settings.
It is loaded only in Development and overrides `appsettings.Development.json` next to the executable;
environment variables override both.
Keep credentials in the ignored local file or the process environment. Legacy `AllowedMessageTypes` and `DepartmentId`
settings are no longer used.

Run from `backend` (on Windows omit `-p:ORPLinuxBuild=true`):

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project src/ORP.Importer.Console -p:ORPLinuxBuild=true -- /path/to/SAAMessages
# Explicitly reconsider unchanged files previously ignored by Department rules:
DOTNET_ENVIRONMENT=Development dotnet run --project src/ORP.Importer.Console -p:ORPLinuxBuild=true -- /path/to/SAAMessages --retry-ignored
```

The console scans `.prt` files recursively once. One logical source directory is supported
per database: identity is the normalized path relative to the scan root, using the existing
case-insensitive SQL path collation. Moving an identical file to a different relative path
creates a different source identity.

For each file, the reader captures its bytes and SHA-256 once. The store checks registered
files first: the same path/hash returns `AlreadyImported`, a different hash returns `Conflict`.
Next, an unchanged ignored path/hash returns `AlreadyIgnored`. These checks happen before
parsing. Reading and hashing are still necessary to detect changed content.

A new or changed file is parsed, then routed by branch, effective direction and Department:

- `Matched`: select a Workflow and atomically insert `SwiftMessages`, `Messages` and one
  `MessageRegistered` audit event; remove any prior ignored mark in that transaction.
- `NoDepartmentRule`: return `Ignored` and save the path, hash, UTC timestamp and reason
  `NoDepartmentRule` in `orp.IgnoredImportFiles`. The message body is not stored.
- Invalid file, unknown/ambiguous branch, missing direction, conflicting Department rules,
  or missing/ambiguous Workflow: report an error without creating an ignored mark.

Ignored entries survive restarts and changes to rules. Changed bytes are checked automatically;
`--retry-ignored` explicitly rechecks unchanged ignored files with the current rules. If a retry
fails, its previous ignored entry remains. If it still has no rule, it remains ignored. Once
registered, changed content never overwrites an existing message. Both registration and ignoring
use a transaction-owned `sp_getapplock` for the path and recheck registration after acquiring it.
This prevents competing runs from creating duplicate dispositions; registered messages take precedence.

New processing messages have state `New`; no-confirmation messages have state
`ViewOnly`. Both start without an assignee and with UTC `ReceivedAt`; the original
header's `Time` remains timezone-free. Workflow and per-Department `CanProcess` flags
are stored atomically and are not recalculated when routing rules change.

The final scan reports `Imported`, `AlreadyImported`, `Ignored`, `AlreadyIgnored` and errors.
Ignoring is a normal outcome: it does not increment errors or cause a nonzero exit code.
Per-file errors/conflicts return exit code 1; configuration and fatal database errors return 2.
Logs contain paths, hashes, routing IDs, message type, effective direction and outcomes,
without message bodies or credentials. Parser and per-file failures expose a structured
`Reason`; unmatched rules log the routing fields at Information level. Scan startup records
whether `--retry-ignored` was used.
Console events include UTC timestamps by default; `Logging__Console__FormatterName=json`
selects structured JSON output for a captured run.
`Logging__LogLevel__ORP.Importer=Debug` enables parser diagnostics.

### Service responsibilities

| Component | Responsibility |
|---|---|
| Console | Configuration, enumeration, per-file invocation, counters and exit code. |
| `SaaSourceFileReader` | File bytes, normalized relative path and SHA-256 snapshot. |
| `SaaMessageParser` | UTF-8/SAA validation and document construction from the snapshot. `ParseFile` is a reader/parser compatibility wrapper. |
| `SaaMessageProcessor` | Route an already parsed document; return `Matched` or `NoDepartmentRule`. No file or database access. |
| `SaaDepartmentRuleResolver` | Match type, branch and direction; resolve priorities. |
| `SaaFileImportService` | Coordinate read → stored-state check → parse → route → persist through `ISaaImportStore`. |
| `SaaRoutingCatalog` | Load branches and active Department rules once at startup. |
| `SqlSaaImportStore` | State lookup, ignored-file persistence, Workflow selection and atomic registration with audit and concurrency protection. |

```csharp
var catalog = new SaaRoutingCatalog(connectionString);
var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(),
    await catalog.LoadBranchResolverAsync(), defaultDirection: defaultDirection);
var importer = new SaaFileImportService(new SaaSourceFileReader(), new SaaMessageParser(),
    processor, new SqlSaaImportStore(connectionString));
var result = await importer.ImportAsync(path, rootDirectory, retryIgnored: false);
```

`ORP.Importer` targets `netstandard2.0` and has no EF dependency. `ISaaImportStore` provides
state lookup, ignored-file recording and routed-message registration; SQL implementations
live in Infrastructure. `SaaProcessedMessage` preserves the parsed document and exposes the
routing status and resolved IDs. There is no allowlist validation flag.

The versioned `tests/ORP.Importer.Tests` suite requires no database. The separate
`tests/ORP.Importer.SqlTests` suite requires `ORP_TEST_SQL_SERVER` and permission to create/drop
its own `ORP_ImporterTests_<Guid>` databases. It fails clearly when configuration is absent.
Add this project only to the local Docker solution; it is excluded from `ORP.sln` so Windows
unit tests continue to require no SQL connection. The prepared local solution includes it;
run `./test-backend.docker.sh -m:1` from the repository root. Concurrent tests hold a SQL
application lock until both writers are observed waiting, then verify persisted rows.

## Load sample data into SQL Server

See [test data and named-user walkthroughs](../docs/TEST_DATA_SCENARIOS_RU.md) for
step-by-step assignment, confirmation, Undo and access-control scenarios.

Apply migrations separately, then open [scripts/seed-test-data.sql](scripts/seed-test-data.sql)
in SSMS, select the target database, and execute the whole script.

**Every run deletes all ORP application data, including users, roles, messages and audit
history, and replaces it with samples. Stop the application and other writers first.**

The script creates three branches, three departments, four reviewers and one global
administrator (`admin`), three common workflows and 100
synthetic Swift messages with registration audit events. Messages start in `New` state
without an assignee. The single `Reviewer` role grants message viewing, Assign, review
levels 1–3, bulk approve, Undo and audit viewing. Three reviewers have access to all nine branch/department
pairs; Amelia is scoped to London/CS for access-control examples. `Operations manager`
retains all permissions; reviewers do not receive workflow management or global
administrator access.

The demo has four synthetic routes using the common profiles:

| Route | Confirmations | Ordinary Undo |
| --- | --- | --- |
| Incoming MT199 / CS | 1, 2 | First only, before second completes |
| Incoming MT299 / TFO | 1, 2 | First only, before second completes |
| Incoming MT671 / DC | 1 | Disabled |
| Outgoing MT299 / TFO | 1 | Disabled |

These are demo routes, not the business matrix. All 100 demo messages start New.
TEST-00001 and TEST-00002 demonstrate two confirmations; TEST-00003 demonstrates
one. TEST-00001–TEST-00075 are Incoming; TEST-00076–TEST-00100 are Outgoing.
The actual routing matrix is seeded separately. Confirmation is immediate via the
backend Confirm API; the existing frontend flow needs a separate update.

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
It has no Docker project or Docker startup dependency. Open `ORP.sln`, select the shared
`Development` launch profile in the Visual Studio toolbar and press F5 to start the API
and run the importer once. `Development - API` and `Development - Importer` start either
project separately. Shared solution profiles require Visual Studio 2022 17.11 or later
with Multi-Project Launch Profiles enabled (Tools > Options > Preview Features).

Both executable projects also have their own `Development` profile. The existing API
`SqlServer` profile remains available. API profiles use port 5080; `Development` explicitly
disables automatic migrations. Database preparation is explicit.
Configure the API's `ConnectionStrings:ORP` through **Manage User Secrets** or
`ConnectionStrings__ORP` in the environment. For the importer, use its ignored
`appsettings.Importer.local.json` or `ConnectionStrings__ORP`.

The importer's Development profile scans `backend/docs/SAAMessages` recursively.
Place source `.prt` files there; the directory contents are ignored by Git. The importer
runs once on each launch and uses the existing imported/ignored-file tracking. Its
working directory is the project directory, so the path is independent of the checkout
location. A different source directory or `--retry-ignored` can be supplied in the
project's debug properties. No machine-specific paths or credentials are stored in profiles.

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

Frontend integration guides: [Message workflow and API examples (RU)](docs/FRONTEND_MESSAGE_WORKFLOW_RU.md),
[Grid API](docs/FRONTEND_GRID_API.md) and [Message stage counts](docs/FRONTEND_MESSAGE_STAGE_COUNTS.md).

`GET /api/messages/state-counts` returns `{ items, total }`.
`items` contains all states as `{ state, count, breakdown }`.
`total` contains `{ count, breakdown }`: `count` sums all state counts, and `breakdown` sums each message type across all states.
Each state's `breakdown` contains `{ messageType, count }` entries for accessible messages in that state;
its counts sum to the state total. Empty states return `count: 0` and `breakdown: []`.
These counts do not apply grid filters or assignment scopes.

### Assignments by confirmation level

`POST /api/messages/{id}/assign` accepts `{ "assignedTo": 42, "level": 2 }`.
Each request assigns or replaces one required, unapproved workflow level. Future
levels can be assigned before earlier approvals, including while an earlier review
session is running. Omitting `level` selects the current stage for compatibility.
`GET /api/messages/{id}/assignment-candidates?level=2` uses the same eligibility
rules. A level already being reviewed cannot be reassigned.

`GET /api/messages/{id}` includes `assignments`, ordered by level, with `level`,
`assignedTo` and `assigneeName`. The `/view` approval rows also show future assignees.
`currentAssigneeId` remains the current stage's executor. `assignmentScope=mine`
includes all open level assignments in both grid and state counts, within the
user's existing message access scope. Future assignees cannot approve early.

Approval closes the completed level's assignment and activates the next level's
preassignment. Without a preassignment, `UntilWorkflowCompleted` retains the
previous executor on the next level; `PerConfirmation` waits for a new assignment.
Permissions and reviewer-reuse policy still apply to confirmation. When reviewer
reuse is disabled, candidates also exclude users reserved for other levels
(with the existing global-administrator exception).

Undo preserves future assignments; closed assignments are not automatically
reopened. The retention policy still applies. Cancelling a review keeps its
assignment; rejection, completion, or an actual workflow/processing-scope change
closes remaining assignments. Assignment audit events include `reviewLevel`.
Assignment changes advance the message version even for future levels.
Message details are reread if that version changes during loading; repeated concurrent
changes return HTTP 409 instead of combining state and assignments from different versions.

Apply migration `AssignmentsByLevel` before running this version. It maps existing
open assignments to the current stage and leaves historical levels unknown.
Rollback refuses messages with multiple open assignments rather than losing them.

### Bulk approve

`POST /api/messages/reviews/bulk-approve` assigns messages to the authenticated user
and approves **all remaining required levels** in one request. Only active
`Immediate` workflows are supported. Previously approved levels are preserved.

```json
{
  "messageIds": [101, 102],
  "comment": "Checked"
}
```

Supply 1–100 unique positive IDs and an optional comment of at most 2000 characters.
Invalid input returns HTTP 400 before any message is processed. The user needs
`review.bulk-approve`, `message.assign` and `review.levelN` for **every remaining
level**, with the existing branch/processing-department access checks. The new
bulk permission allows the same reviewer to approve multiple levels even when
`AllowReviewerReuse=false`; ordinary confirmation endpoints keep their existing
rules. The permission migration grants it to an existing `Reviewer` role;
GlobalAdministrator retains its existing permission override.

Messages are processed sequentially, each in its own transaction. Another user's
assignment on the current or any remaining level is a conflict, including for an
administrator. Own assignments are reused. Completed, rejected, view-only,
in-progress and non-Immediate messages return conflicts without new approvals.
On success the message reaches `Completed`, assignments end according to the
existing workflow lifecycle, and the normal per-level review and assignment
audit records retain the actual actor, comment and request correlation ID.

HTTP 200 contains one result per input ID in input order, including failures:

```json
{
  "results": [
    { "messageId": 101, "status": 200, "approvedLevels": [1, 2] },
    {
      "messageId": 102,
      "status": 409,
      "error": {
        "code": "domain_conflict",
        "detail": "The message or a remaining level is assigned to another reviewer."
      }
    }
  ]
}
```

Per-message error codes are `forbidden` (403), `not_found` (404),
`domain_conflict` / `concurrent_update` (409), and sanitized `unexpected_error`
or `commit_outcome_unknown` (500). Failure before commit rolls back that message's
assignments, approvals and audit, while other messages continue. An unknown commit
outcome is never replayed automatically: reload that message before retrying.
Repeating approval for a completed message returns 409 without new review/audit
records. Cancellation stops further processing; earlier commits remain persisted.

### Grid API

`POST /api/messages/grid` accepts flat JSON controls: `search`, `status`, `messageType`, `direction`, `branch`,
`dateFrom`, `dateTo`, `page` (one-based, default 1), `pageSize` (default 20, maximum 500).
Empty/null text controls impose no filter. Optional sorting and assignment scopes remain supported.
`direction` accepts `Incoming` or `Outgoing` (case-insensitive, trimmed); invalid values return 400.
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

Request-shape validation for message mutations runs before the transaction. Permission, ownership
and workflow checks still run inside it. Assign-and-confirm shares one loaded message, workflow,
review list and assignment list within an attempt, while checking both required permissions.
Assignment and confirmation retain their separate saves inside the same outer transaction.

Review start/resume and undo availability use the same domain rules as mutations. The grid evaluates
these rules for the loaded page; undo policy is not duplicated in a SQL predicate. Its fixed message
filters use typed LINQ predicates, while the administrative user grid retains its dynamic filters.

The message card (`GET /api/messages/{id}/view`) checks the message version after reading its
approvals and audit history, retrying the whole card up to three times and returning 409 if it keeps
changing. This prevents mixed message/assignment/review versions. Audit-only additions such as PDF
generation do not advance the message version: the card includes the history read by its audit query,
not a transaction-wide snapshot of every audit addition or current user-role change.

`BusinessActionCommitted` is emitted only after the outer transaction commit is acknowledged.
Events from failed attempts are discarded. If commit throws (including cancellation or connection
loss), the outcome is unknown: no success event is emitted and the mutation is not automatically
replayed. The request fails; reload persisted state before deciding whether to submit it again.
This does not provide exactly-once logging: a process failure after commit can leave a committed
SQL audit event without a corresponding application log entry. See
[EF Core commit failures](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue).
