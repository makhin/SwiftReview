# ORP.Sync tests

This project compiles the unchanged synchronization sources on .NET 10 so tests can run on Linux.
The deployed executable still targets .NET Framework 4.7.2; these tests do not verify that runtime
or the private Swift assembly. Mock tests exercise the reflection mapper through `GetMessages`.

Run the synchronization unit tests:

```bash
dotnet test tests/ORP.Sync.Tests/ORP.Sync.Tests.csproj
```

The known full-BIC/BIC8 routing and decimal-comma numeric-wrapper conversion defects remain open;
this test suite does not claim coverage of those cases. Configuration-changing tests are serialized
and restore the previous settings after each test.
