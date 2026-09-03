using System;

namespace ORP.Sync;

internal static class LegacySwiftSynchronizer
{
    public static void Run(string connectionString)
    {
        _ = connectionString;
        throw new InvalidOperationException(
            "The legacy SWIFT NuGet adapter is not configured. Add the package and implement LegacySwiftSynchronizer.Run before scheduling this executable.");
    }
}
