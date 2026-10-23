# Import-to-approval backend tests

These tests import a real temporary SAA `.prt` file through `SaaFileImportService`,
then assign and confirm it through the API hosted by `WebApplicationFactory`.
Tests use the existing application database named in the connection string.
They do not create/drop databases, run migrations, or delete database records.
Apply the application's migrations before running them; pending migrations cause
a clear failure before test data is inserted.

Each scenario adds uniquely named E2E branches, departments, workflows, scoped
reviewers and a branch-specific routing rule. A synthetic MX type avoids matches
with existing global routing rules. The imported file path and reference are
unique too; assertions select only that scenario's records. No messages are
inserted by the test setup. Database records deliberately remain after tests,
including failed runs; only temporary files and the test API host are disposed.
Repeated runs can use the same database without cleaning previous E2E data.

Coverage: two independent approvals, reimport after completion, non-assignee
rejection, and rejection of an already approved level. Assertions cover HTTP
details/card responses, persisted reviews and assignments, and business audit.
Authentication uses Development `X-Debug-User`; external authentication, browser
interaction and the importer console process are outside this suite.

From the repository root, run the full local backend suite:

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
uses its catalog as-is. Missing configuration fails explicitly. The project
belongs to the ignored local `ORP.Docker.sln`, not
the versioned `ORP.sln`. In a newly prepared Docker workspace, register it with
`dotnet sln ORP.Docker.sln add tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj`
from `backend`.
