using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Whispr.Server.Data;

/// <summary>
/// Design-time factory for EF Core tools (e.g. dotnet ef migrations add).
/// </summary>
public sealed class WhisprDbContextFactory : IDesignTimeDbContextFactory<WhisprDbContext>
{
    public WhisprDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var dbPath = config["ServerOptions:DatabasePath"] ?? "whispr.db";
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(basePath, dbPath);
        var connectionString = $"Data Source={Path.GetFullPath(dbPath)}";

        var options = new DbContextOptionsBuilder<WhisprDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new WhisprDbContext(options);
    }
}
