using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Verdure.ImageMcp.Infrastructure.Data;

namespace Verdure.ImageMcp.Server;

/// <summary>
/// Design-time factory for creating ImageMcpDbContext for EF Core migrations
/// </summary>
public class ImageMcpDbContextFactory : IDesignTimeDbContextFactory<ImageMcpDbContext>
{
    public ImageMcpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ImageMcpDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Host=localhost;Database=verdure_image_mcp;Username=postgres;Password=postgres";
        
        optionsBuilder.UseNpgsql(connectionString, 
            b => b.MigrationsAssembly("Verdure.ImageMcp.Server"));

        return new ImageMcpDbContext(optionsBuilder.Options);
    }
}
