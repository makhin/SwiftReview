# ORP.Sync tests

This project compiles the unchanged synchronization sources on .NET 10 so tests can run on Linux.
The deployed executable still targets .NET Framework 4.7.2; these tests do not verify that runtime
or the private Swift assembly. Mock tests exercise the reflection mapper through `GetMessages`.

Run all synchronization tests, including persistence against a disposable SQL Server 2022 container:

```bash
RUN_INTEGRATION_TESTS=1 dotnet test tests/ORP.Sync.Tests/ORP.Sync.Tests.csproj
```

Without `RUN_INTEGRATION_TESTS=1`, the SQL test is skipped. Docker must be available for that test.
It verifies insert-only behavior within a batch and across runs, positional null padding,
unroutable messages, watermark advancement, and transaction rollback.

The known full-BIC/BIC8 routing and decimal-comma numeric-wrapper conversion defects remain open;
this test suite does not claim coverage of those cases. Configuration-changing tests are serialized
and restore the previous settings after each test.
