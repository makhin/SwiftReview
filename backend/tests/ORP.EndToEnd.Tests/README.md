# Import-to-approval backend tests

These tests import a real temporary SAA `.prt` file through `SaaFileImportService`,
then assign and confirm it through the API hosted by `WebApplicationFactory`.
Tests use the existing application database named in the connection string.
They do not create/drop databases or run migrations.
Apply the application's migrations before running them; pending migrations cause
a clear failure before test data is inserted.

Each scenario adds uniquely named E2E branches, departments, workflows, scoped
reviewers and a branch-specific routing rule. A synthetic MX type avoids matches
with existing global routing rules. The imported file path and reference are
unique too; assertions select only that scenario's records. No messages are
inserted by the test setup. On disposal (including failed tests/setup), the fixture
removes its own messages, audit, reviews, assignments, routing, user-role grants,
role permissions, workflow steps, workflows, users, roles, branches and departments.
Shared permissions and all earlier/unrelated data are preserved. Temporary files
and the test API host are disposed too.

Cleanup uses `scripts/delete-messages-by-id-range.sql` with the exact run UUID.
The same script recognizes fixture ownership from the message metadata when run
manually by ID range. Preview is the default. If another scope references a fixture
object, ownership checks and foreign keys abort and roll back cleanup rather than remove that scope's
links. Interrupted processes can still leave records; use the range script or its
exclusive `@TestRunId` parameter to clean a known run, including setup-only runs.
There is no broad deletion of all names starting with E2E.

For manual cleanup of all E2E leftovers, including runs whose messages have already
been removed, use these declarations in `scripts/delete-messages-by-id-range.sql`:

```sql
DECLARE @FromId bigint = NULL;
DECLARE @ToId bigint = NULL;
DECLARE @DryRun bit = 1;
DECLARE @TestRunId varchar(32) = NULL;
DECLARE @AllE2E bit = 1;
```

Review the selected messages and fixture IDs, then change `@DryRun` to `0`.
Discovery checks all fixture root tables for the reserved exact UUID-bearing
names: `E2E-<32 lowercase hex>` directories/roles, `E2E_<uuid>` workflows,
`e2e.first.<uuid>` / `e2e.second.<uuid>` users, `e2e-<uuid>` routing sources and
`approval-<uuid>.prt` file records. Ordinary preexisting data, shared permissions
and merely similar prefixes are not selected. All-E2E mode also removes leftovers
from older test runs, not only the most recent one. Objects without these ownership
markers cannot safely be inferred to be test data and are left alone.

The three selection modes (range, exact run, all E2E) are mutually exclusive.
External references abort the entire transaction; they are never automatically
removed to make cleanup succeed. Run manual cleanup with API/importers/tests stopped.

Coverage: two independent approvals, reimport after completion, non-assignee
rejection, and rejection of an already approved level. Assertions cover HTTP
details/card responses, persisted reviews and assignments, and business audit.
Authentication uses Development `X-Debug-User`; external authentication, browser
interaction and the importer console process are outside this suite.

E2E tests run only by an explicit project command. They are excluded from both
`ORP.sln` and the local `ORP.Docker.sln`, so the regular backend command does not
run them:

```sh
./test-backend.docker.sh -m:1
```

The project reads `ConnectionStrings:ORP`, as the API and console importer do.
It shares the API's User Secrets store (`SwiftReview-ORP-Api`), so an existing API
connection works in IDE test runners and `dotnet test` without launch profiles.
The SQL login needs normal application read/write access; database creation and
deletion permissions are not required.

Configuration priority, highest first:

1. `ORP_TEST_SQL_SERVER` (explicit test-server override used by the Docker launcher).
2. `ConnectionStrings__ORP` environment variable.
3. `ConnectionStrings:ORP` in User Secrets (`SwiftReview-ORP-Api`, Development only).
4. `appsettings.EndToEnd.local.json` in this project (Git-ignored, Development only).
5. `appsettings.{Environment}.json` (including `appsettings.Development.json`).
6. `appsettings.json`.

Use **Manage User Secrets** on this project or the API to configure the shared
connection. Changes to that secret affect both projects. The project's JSON files
are copied to `EndToEndSettings/` in the build output to avoid collisions with
the referenced API's settings; edit the source files in this project.
Do not commit credentials to the versioned template.

The `Development` launch profile sets `DOTNET_ENVIRONMENT` and
`ASPNETCORE_ENVIRONMENT` to `Development`. Test runners do not apply launch
profiles, so configuration defaults to Development when neither variable is set.
An explicit `DOTNET_ENVIRONMENT` takes priority over `ASPNETCORE_ENVIRONMENT`.
The in-process API always uses Development to enable test authentication.

To run the xUnit executable with the launch profile, run from `backend`:

```sh
dotnet run --project tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj --launch-profile Development -p:ORPLinuxBuild=true
```

To run only this project, run from `backend`:

```sh
dotnet test tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj -p:ORPLinuxBuild=true -m:1
```

The connection must explicitly name an existing application database; an empty
catalog or a SQL Server system database is rejected. `ORP_TEST_SQL_SERVER` also
uses its catalog as-is. Missing configuration fails explicitly. Keep this project
out of the application solutions; use the explicit project command above for
manual E2E runs.

## Department distribution list tests

`DistributionListTests` uses the same connection configuration and an in-process
API against real SQL Server. It requires current migrations and does not apply
them. Each test creates UUID-named branches, departments, users and roles;
cleanup deletes only the exact recorded fixture IDs and their distribution lists
in a transaction, preserving shared permissions and existing data.

Coverage includes CRUD, department and branch scope permissions, administrator
access, validation, database uniqueness and cascade constraints, atomic rollback,
Reviewer migration grants, internal rowversion checks, and synchronized HTTP
updates that return one success and one conflict without retrying the loser.
