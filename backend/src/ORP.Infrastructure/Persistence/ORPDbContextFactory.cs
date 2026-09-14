using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ORP.Infrastructure.Persistence;

public sealed class ORPDbContextFactory : IDesignTimeDbContextFactory<ORPDbContext>
{
    public ORPDbContext CreateDbContext(string[] args)
    {
        var commandLineConfiguration = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        var environment = commandLineConfiguration["environment"]
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        var currentDirectory = Directory.GetCurrentDirectory();
        var contentRoot = File.Exists(Path.Combine(currentDirectory, "appsettings.json"))
            ? currentDirectory
            : Path.Combine(currentDirectory, "src", "ORP.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration["ConnectionStrings:ORP"]
            ?? throw new InvalidOperationException("Connection string 'ORP' is required.");
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ORPDbContext(options);
    }

}
