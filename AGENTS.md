# Repository Agent Instructions

Before writing, reviewing, or refactoring code, read and follow the repository-local
`karpathy-guidelines` skill at `skills/karpathy-guidelines/SKILL.md`.

For server-backed DevExtreme grids, keep data loading in `CustomStore` backed by
`DevExtreme.AspNet.Data`; do not use TanStack Query for grid load operations.

## Environment selection

There are two solutions using the same application projects, with different test sets:

- **Current Linux workspace / Docker:** `backend/ORP.Docker.sln`. SQL Server runs in
  Docker; API and frontend run on the host. This solution includes local-only API/SQL
  integration tests in `backend/tests/ORP.Api.Tests/`.
- **Windows / Visual Studio / external SQL Server:** `backend/ORP.sln`. This is the
  versioned solution, including Domain and Application unit tests.
  It does not include API/SQL integration tests. Its tests need no SQL connection
  or CREATE/DROP DATABASE permissions. Docker is not required
  and must not be invoked by the external-environment launchers.

Use the matching environment without asking the user to choose again. Follow the
SDK pinned in `backend/global.json`; execute solution build/test commands from
`backend` so that SDK selection uses this file.

The PDF converter uses iText and must not restore DevExpress PDF or SkiaSharp packages.
The ignored `backend/Directory.Build.local.props` selects `ORP.Docker.sln` and keeps
Linux outputs in `bin/linux/` and `obj/linux/`. For direct project build/test/run
commands in this Linux workspace, pass `-p:ORPLinuxBuild=true`; local Docker launchers
set it for API startup. Keep this local props file out of Git.

`ORP.Docker.sln`, `compose.sql.yml`, `README.Docker.local.md`, Docker launchers and
local database helpers are intentionally ignored by Git. Keep them local; never
force-add them. `backend/tests/ORP.Api.Tests/` is also temporarily local-only and
ignored by Git; retain it in the Docker solution but do not restore it to `ORP.sln`
or force-add it without an explicit user request. Application code and unit tests
remain versioned. A Git clone intentionally does not contain the SQL integration suite.

`backend/ORP-with-IntegrationTests.sln` and `backend/tests/ORP.IntegrationTests/`
are obsolete, ignored local artifacts. Do not use them for builds or validation.

## Start the application

Commands below are run from the repository root unless stated otherwise.

### Local Docker environment

1. Obtain `MSSQL_SA_PASSWORD` from the process environment. If absent, read only
   that key from the ignored `backend/.env` and pass it to the child process.
   Do not print credentials, commit them, or execute a dotenv file as shell code.
   Reuse available local settings rather than asking for credentials again.
   If neither source contains the password, report the missing setting.
2. Run `./run-dev.docker.sh` (PowerShell equivalent: `./run-dev.docker.ps1`).
   It starts SQL Server, waits for readiness and supplies the connection settings.
3. Defaults: Compose project `swiftreview-docker`, SQL port `14333`, database
   `SwiftReviewDev`, API port `5080`, frontend port `5173`. Optional SQL port
   override: `ORP_DOCKER_SQL_PORT`. Docker startup applies dev migrations by
   default; `BootstrapDatabase=false` disables them.
4. Sample data is loaded explicitly with `backend/scripts/seed-test-data.sql`.
   See the ignored `backend/README.Docker.local.md` for the exact command.
   Do not reset the persistent dev database to prepare automated tests.

Stopping the launcher stops the application processes, not SQL Server or its volume.

### Windows / external SQL Server

- Open `backend/ORP.sln`, select `ORP.Api` as startup project and the `SqlServer`
  launch profile. Set `ConnectionStrings:ORP` through **Manage User Secrets**
  (project ID `SwiftReview-ORP-Api`) or `ConnectionStrings__ORP` in the environment.
- `./run-dev.ps1` starts API and frontend together. `./run-dev.sh` provides the same
  external-SQL launch on Linux/macOS. Neither launcher starts Docker.
- External launchers and the Visual Studio profile disable automatic migrations
  by default. Apply migrations explicitly using `backend/README.md`, or set
  `BootstrapDatabase=true` deliberately for a development database.
- The frontend dotenv file does not configure the API. For CLI migrations, pass
  `ConnectionStrings__ORP`; the design-time DbContext factory reads environment
  settings rather than the API's user secrets.

## Required verification

After modifying code, run the relevant automated tests before finishing. Changes
spanning backend and frontend require both test suites. Explicitly report failed
or skipped tests; do not claim completion while relevant tests are failing.

- **Backend, local Docker:** `./test-backend.docker.sh -m:1`. It waits for SQL Server,
  sets `ORP_TEST_SQL_SERVER`, and builds/tests `ORP.Docker.sln`. Do not pass
  `--no-build` after code changes unless the same changes were already built.
- **Backend, Windows:** run `./test-backend.ps1`. It builds/tests `ORP.sln` with
  Domain and Application unit tests. Neither this launcher nor Visual Studio
  unit-test runs require `ORP_TEST_SQL_SERVER`, Docker or a database connection.
- **Frontend:** `npm --prefix frontend test`, `npm --prefix frontend run typecheck`,
  and `npm --prefix frontend run build`. Run lint when changing frontend code.
- **HTTP contracts changed:** with the current API on port 5080, run
  `npm --prefix frontend run api:check`; regenerate contracts with `api:generate`
  if the API change requires it, then rerun frontend verification.
- **Only documentation changed:** check referenced commands/paths and run
  `git diff --check`; there is no need to repeat unaffected test suites.

The local-only API tests use real SQL Server. `ORP_TEST_SQL_SERVER` needs permission to
create and delete databases. The fixture replaces the input catalog with its own
`ORP_Tests_<Guid>`, applies migrations, loads the SQL seed and deletes that database
on disposal or failed initialization. Missing configuration must fail clearly;
do not restore EF Core InMemory or silently skip SQL tests.

In the prepared local Docker workspace, for transaction/concurrency/grid changes,
retain and run
`SqlServerTransactionTests`, `SqlServerHttpTransactionTests`,
`SqlServerMessageGridTests` and `SqlServerAdministrationTests`. Targeted tests may
be used during development; finish with the relevant full suite. Concurrent tests
must synchronize requests explicitly and verify persisted state, not merely launch
several tasks and assume they overlapped. If the ignored integration suite is absent
(e.g. a Windows Git clone), run the versioned unit tests and explicitly report SQL
integration tests as unavailable; do not ask for database creation permissions.
