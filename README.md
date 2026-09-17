# Operations Reporting and Processing

Operations Reporting and Processing is a full-stack application for registering, assigning, reviewing, and auditing SWIFT messages. The repository contains an ASP.NET Core backend and a React frontend, with configurable one-, two-, and three-stage review workflows and permission-based data access.

## Repository structure

- [`backend`](backend/README.md) — .NET solution, REST API, domain and application layers, SQL Server persistence, database migrations, and tests.
- [`frontend`](frontend/README.md) — React and TypeScript client built with Vite and DevExtreme.
- [`tools/OpenApiTsContracts`](tools/OpenApiTsContracts/README.md) — deterministic OpenAPI-to-TypeScript data-contract generator.
- [`skills`](skills) — repository-local instructions used by coding agents.

## Main capabilities

- SWIFT message registration and server-side grid loading.
- Assignment and reassignment with branch and department access rules.
- Configurable multi-stage review, approval, rejection, and undo operations.
- Four-eyes controls and protection against invalid workflow transitions.
- Audit history, dashboard summaries, and reference-data endpoints.
- OpenAPI documentation, Problem Details responses, health checks, and OpenTelemetry instrumentation.

## Prerequisites

- .NET SDK 10.0.400, as pinned in `backend/global.json`.
- Node.js 20.19 or any supported newer LTS release (22.12+ or 24+).
- npm.

SQL Server is required to run the API; the versioned unit tests do not require a database. Configure `ConnectionStrings__ORP` in the process environment or .NET user secrets before starting the backend. See [SQL Server setup and sample data](backend/README.md). The launcher scripts inherit the environment; the frontend `.env` file does not configure the API.

## Run locally — Windows and external SQL Server

Open `backend/ORP.sln` in Visual Studio, set `ORP.Api` as the startup project and select
its `SqlServer` launch profile. Docker is not required.

Configure the API connection in **Manage User Secrets** for `ORP.Api`:

```json
{
  "ConnectionStrings": {
    "ORP": "Server=YOUR_SERVER;Database=ORP;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
  }
}
```

Alternatively, set `ConnectionStrings__ORP` in the process environment before starting
Visual Studio or the launcher. Apply database migrations and, for a fresh demo database,
load sample data as described in the [backend instructions](backend/README.md).
The `SqlServer` profile and launchers do not apply migrations automatically unless
`BootstrapDatabase=true` is explicitly configured.

Start the API and frontend together:

```powershell
.\run-dev.ps1
```

Or start the API from Visual Studio and run `npm run dev` in `frontend`.
On Linux/macOS with an external SQL Server, `./run-dev.sh` starts both processes.
Press `Ctrl+C` to stop them. The API listens on <http://localhost:5080>.

The Docker environment is local only: `backend/ORP.Docker.sln`, Compose configuration,
and `*.docker.sh` / `*.docker.ps1` launchers are excluded from Git. Both solutions share
the same API source projects.

Useful backend endpoints:

- Scalar API reference: <http://localhost:5080/scalar>
- OpenAPI document: <http://localhost:5080/openapi/v1.json>
- [API routes and contracts](docs/API.md)
- Health check: <http://localhost:5080/health>

## Configuration

Frontend development settings are read from the repository-root `.env` file. Start from the provided example if custom values are needed:

```bash
cp .env.example .env
```

The most relevant frontend variables are:

- `VITE_API_PROXY_TARGET` — backend address used by the Vite proxy.
- `VITE_DEBUG_USER` — Development user sent in the `X-Debug-User` header.

Backend configuration follows standard ASP.NET Core configuration rules. See the [backend documentation](backend/README.md) for database and migration settings.

## Verification

Azure CI uses separate backend and frontend pipelines. See [Azure Pipelines setup](docs/AZURE_PIPELINES.md) for creation steps, triggers, and PR validation.

Run the backend unit tests without SQL Server or Docker. The main solution contains Domain and Application unit tests:

```bash
dotnet restore backend/ORP.sln --configfile backend/NuGet.Config
dotnet build backend/ORP.sln --no-restore
# Windows, from the repository root:
.\test-backend.ps1
```

Run the frontend checks:

```bash
cd frontend
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

More detailed setup and development notes are available in the backend and frontend README files linked above.
