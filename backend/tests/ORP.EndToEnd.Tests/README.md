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

To run only this project, set `ORP_TEST_SQL_SERVER` to a SQL Server connection
with permission to create/drop test databases, then run from `backend`:

```sh
dotnet test tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj -p:ORPLinuxBuild=true -m:1
```

The connection's catalog is replaced with a unique `ORP_EndToEndTests_<guid>`
database; its original database is not migrated or seeded. Missing configuration
fails explicitly. The project belongs to the ignored local `ORP.Docker.sln`, not
the versioned `ORP.sln`. In a newly prepared Docker workspace, register it with
`dotnet sln ORP.Docker.sln add tests/ORP.EndToEnd.Tests/ORP.EndToEnd.Tests.csproj`
from `backend`.
