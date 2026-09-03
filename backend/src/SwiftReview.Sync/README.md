# SWIFT synchronization host

`SwiftReview.Sync.exe` is a one-shot .NET Framework 4.7.2 process intended for Windows Task Scheduler.
It must finish the legacy SWIFT synchronization before it calls `ORP.RegisterNewMessages`.

Before deployment:

1. Add the legacy NuGet package and replace the fail-fast body of `LegacySwiftSynchronizer.Run` with its synchronous API call.
2. Replace the empty bootstrap `[ORP].[SwiftMessageSource]` view with a read-only projection over `[dbo].[Messages]` after the Body-to-branch/department mapping rules are known. The required output columns are listed in `SwiftMessageRecord`.
3. Put the database connection string in the deployed `SwiftReview.Sync.exe.config`. Protect that file with the service account ACL; do not pass the connection string on the command line.
4. Run the executable manually and confirm exit code `0` before enabling its schedule.

Recommended task settings are one daily trigger and three retries at 15-minute intervals. A non-zero exit code means either synchronization or ORP registration failed. Registration is idempotent and never resets an existing ORP workflow state.
