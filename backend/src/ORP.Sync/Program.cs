using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

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
            RegisterNewMessages(connectionString!);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RegisterNewMessages(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand("[ORP].[RegisterNewMessages]", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 300
        };
        command.Parameters.Add("@CorrelationId", SqlDbType.NVarChar, 100).Value = $"sync-{Guid.NewGuid():N}";
        connection.Open();
        command.ExecuteNonQuery();
    }
}
