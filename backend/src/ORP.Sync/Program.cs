using System;
using System.Configuration;

namespace ORP.Sync;

internal static class Program
{
    public static int Main()
    {
        try
        {
            var connectionString = ConfigurationManager.ConnectionStrings["ORP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ConfigurationErrorsException("Connection string 'ORP' is required.");

            LegacySwiftSynchronizer.Run(connectionString!);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
