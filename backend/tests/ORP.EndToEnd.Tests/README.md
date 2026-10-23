# Import-to-approval backend tests

These tests import a real temporary SAA `.prt` file through `SaaFileImportService`,
then assign and confirm it through the API hosted by `WebApplicationFactory`.
Each test creates its own SQL Server database, applies migrations, and seeds only
workflow/routing configuration and scoped reviewers. No messages are inserted by
the test setup. The database and temporary files are deleted on disposal.

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
The SQL login must have permission to create/drop test databases.

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

The connection's catalog is replaced with a unique `ORP_EndToEndTests_<guid>`
database; its original database is not migrated or seeded. Missing configuration
fails explicitly. The project belongs to the ignored local `ORP.Docker.sln`, not
the versioned `ORP.sln`. In a newly prepared Docker workspace, register it with
`dotnet sln ORP.Docker.sln add tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj`
from `backend`.
