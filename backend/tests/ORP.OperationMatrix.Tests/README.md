# Full operation matrix

This project contains the exhaustive assignment/review/undo HTTP operation matrix
and the workflow/routing SQL matrix. It is excluded from both application
solutions and runs only through an explicit project command from `backend`:

```sh
dotnet test tests/ORP.OperationMatrix.Tests/ORP.OperationMatrix.Tests.csproj -p:ORPLinuxBuild=true -m:1
```

Set `ORP_TEST_SQL_SERVER` to a SQL Server connection with permission to create and
drop test databases. Each fixture creates its own `ORP_ImporterTests_<uuid>`
database, applies migrations and deletes that database on disposal. Existing
application data is not reset. The SQL fixture is linked from `ORP.Importer.SqlTests`
so the regular SQL tests and the manual matrix use the same setup and cleanup.

The regular backend suite retains targeted transaction, concurrency, assignment,
review, authorization and grid regression tests. Keep this project outside
`ORP.sln` and `ORP.Docker.sln`; do not add it to the default test launcher.
