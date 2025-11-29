using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Verdure.Mcp.Infrastructure.Data;

namespace Verdure.Mcp.Server;

/// <summary>
/// Design-time factory for creating McpDbContext for EF Core migrations
/// </summary>
public class McpDbContextFactory : IDesignTimeDbContextFactory<McpDbContext>
{
    public McpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<McpDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Host=localhost;Database=verdure_mcp;Username=postgres;Password=postgres";
        
        optionsBuilder.UseNpgsql(connectionString, 
            b => b.MigrationsAssembly("Verdure.Mcp.Server"));

        return new McpDbContext(optionsBuilder.Options);
    }
}
