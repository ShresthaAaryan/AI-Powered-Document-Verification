using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DocumentVerification.API.Data;

/// <summary>
/// Provides design-time DbContext creation for EF Core tooling.
/// </summary>
public class DocumentVerificationDbContextFactory : IDesignTimeDbContextFactory<DocumentVerificationDbContext>
{
    public DocumentVerificationDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var sqliteConnection = configuration.GetConnectionString("SqliteConnection");
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<DocumentVerificationDbContext>();

        // Prefer PostgreSQL unless explicitly Development with SQLite
        if (environment == "Development" && !string.IsNullOrEmpty(sqliteConnection))
        {
            optionsBuilder.UseSqlite(sqliteConnection);
        }
        else if (!string.IsNullOrEmpty(defaultConnection))
        {
            optionsBuilder.UseNpgsql(defaultConnection);
        }
        else if (!string.IsNullOrEmpty(sqliteConnection))
        {
            // Fallback to SQLite if no PostgreSQL connection
            optionsBuilder.UseSqlite(sqliteConnection);
        }
        else
        {
            throw new InvalidOperationException("No connection string configured for DocumentVerificationDbContext.");
        }

        return new DocumentVerificationDbContext(optionsBuilder.Options);
    }
}

