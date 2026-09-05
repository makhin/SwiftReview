# Operations Reporting and Processing — Backend

The backend implements the Operations Reporting and Processing domain, REST API, persistence, and automated tests. It is a modular .NET solution centred on SWIFT message assignment and multi-stage review workflows.

## Solution structure

- `src/ORP.Domain` — entities, workflow rules, review states, and domain exceptions.
- `src/ORP.Application` — use-case handlers, contracts, validation, and persistence abstractions.
- `src/ORP.Infrastructure` — Entity Framework Core persistence, authorization data, queries, and seed data.
- `src/ORP.Api` — ASP.NET Core endpoints, authentication, authorization, OpenAPI, health checks, and telemetry.
- `src/ORP.Sync` — one-shot .NET Framework 4.7.2 host for registering messages produced by a legacy SWIFT synchronization process.
- `tests` — domain, application, API, and SQL Server integration tests.

Dependencies point inward: the Domain project has no persistence or API dependency, Application depends on Domain contracts, and Infrastructure provides the external implementations used by the API.

## Prerequisites

- .NET SDK 10.0.302, as pinned in `global.json`.
- SQL Server only when using persistent storage or running the opt-in integration suite.
- A Windows environment with .NET Framework 4.7.2 when deploying `ORP.Sync.exe`.

## Run with sample data

The Development configuration enables `UseMockData` and seeds an in-memory database. No connection string is required:

```bash
dotnet run --project src/ORP.Api
```

The default addresses are:

- API: <http://localhost:5080>
- Scalar API reference: <http://localhost:5080/scalar>
- OpenAPI document: <http://localhost:5080/openapi/v1.json>
- Health check: <http://localhost:5080/health>

The in-memory data is recreated whenever the process restarts.

New messages are automatically assigned by the API host. The worker runs immediately at startup
and then every 10 seconds by default. Configure it with `AutoAssignment:Enabled`,
`AutoAssignment:IntervalSeconds`, and `AutoAssignment:BatchSize`.

## Development authentication

The API uses the `X-Debug-User` request header in Development. It accepts either the numeric
user ID or username. Seeded identities include:

- `1` / `amelia.hart`
- `2` / `theo.mercer`
- `3` / `priya.nair`
- `4` / `victor.stone`
- `admin`

For example:

```bash
curl -H 'X-Debug-User: admin' http://localhost:5080/api/me
curl -H 'X-Debug-User: 5' http://localhost:5080/api/me
curl -H 'X-Debug-User: admin' http://localhost:5080/api/dashboard/summary
```

The frontend adds this header to API requests; use `?user=5` or `?user=admin` in its URL to
switch users. The debug authentication scheme is a development facility and must be replaced
by the deployment environment's authentication integration.

## Persistent SQL Server storage

Disable mock data and provide the `ORP` connection string through ASP.NET Core configuration:

```bash
export ConnectionStrings__ORP='Server=localhost,1433;Database=ORP;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=True'
export UseMockData=false
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

All application endpoints are under `/api` and require authentication. The API exposes message search and detail operations, a DevExtreme grid endpoint, assignment and review actions, audit history, dashboard summaries, and reference data.

`GET /api/messages/{id}/audit` returns a newest-first paged history. It accepts `skip` (default `0`) and
`take` (default `100`, maximum `500`) and requires the `audit.view` permission plus access to the message's
branch and department.

`GET /api/messages/grid` accepts DevExtreme remote load options. Data loading remains server-side through `DevExtreme.AspNet.Data`; the frontend consumes it through a DevExtreme `CustomStore`.

Request and response schemas, status codes, and Problem Details payloads are documented in OpenAPI. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to export the configured traces and metrics through OTLP.

## Build and test

```bash
dotnet restore ORP.sln --configfile NuGet.Config
dotnet build ORP.sln --no-restore
dotnet test ORP.sln --no-build --no-restore
```

The SQL Server integration tests are opt-in:

```bash
RUN_INTEGRATION_TESTS=1 dotnet test tests/ORP.IntegrationTests
```

They cover migrations and seed data, idempotent message registration, workflow actions, permissions, SQL filtering and pagination, and concurrent updates.

## Legacy synchronization host

`ORP.Sync` is intended to run once per schedule: it completes the legacy synchronization and then calls `[ORP].[RegisterNewMessages]`. Registration is idempotent and does not reset workflow state for existing messages. Deployment prerequisites and scheduling notes are documented in [`src/ORP.Sync/README.md`](src/ORP.Sync/README.md).
