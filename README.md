# Operations Reporting and Processing

Operations Reporting and Processing is a full-stack application for registering, assigning, reviewing, and auditing SWIFT messages. The repository contains an ASP.NET Core backend and a React frontend, with configurable one-, two-, and three-stage review workflows and permission-based data access.

## Repository structure

- [`backend`](backend/README.md) — .NET solution, REST API, domain and application layers, SQL Server persistence, database migrations, and tests.
- [`frontend`](frontend/README.md) — React and TypeScript client built with Vite and DevExtreme.
- [`skills`](skills) — repository-local instructions used by coding agents.

## Main capabilities

- SWIFT message registration and server-side grid loading.
- Assignment and reassignment with branch and department access rules.
- Configurable multi-stage review, approval, rejection, and undo operations.
- Four-eyes controls and protection against invalid workflow transitions.
- Audit history, dashboard summaries, and reference-data endpoints.
- OpenAPI documentation, Problem Details responses, health checks, and OpenTelemetry instrumentation.

## Prerequisites

- .NET SDK 10.0.302, as pinned in `backend/global.json`.
- Node.js 20.19 or any supported newer LTS release (22.12+ or 24+).
- npm.

SQL Server is only required when the backend is run with persistent storage. The default Development configuration uses an in-memory database populated with deterministic sample data.

## Run locally

Start the backend from the repository root:

```bash
dotnet run --project backend/src/ORP.Api
```

The API is available at <http://localhost:5080>. In another terminal, start the frontend:

```bash
cd frontend
npm ci
npm run dev
```

Open the URL printed by Vite, normally <http://localhost:5173>. During local development, Vite proxies `/api` requests to the backend and sends the `supervisor` debug identity by default.

Useful backend endpoints:

- Scalar API reference: <http://localhost:5080/scalar>
- OpenAPI document: <http://localhost:5080/openapi/v1.json>
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

Run the backend checks:

```bash
cd backend
dotnet restore ORP.sln --configfile NuGet.Config
dotnet build ORP.sln --no-restore
dotnet test ORP.sln --no-build --no-restore
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
