using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Interface for MCP service management
/// </summary>
public interface IMcpServiceService
{
    Task<List<McpServiceDto>> GetServicesAsync(string? category = null, bool enabledOnly = true, CancellationToken cancellationToken = default);
    Task<McpServiceDto?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<McpCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<McpServiceDto> CreateServiceAsync(McpServiceRequest request, string? userId, CancellationToken cancellationToken = default);
    Task<McpServiceDto?> UpdateServiceAsync(Guid id, McpServiceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing MCP services
/// </summary>
public class McpServiceService : IMcpServiceService
{
    private readonly McpDbContext _dbContext;
    private readonly ILogger<McpServiceService> _logger;

    public McpServiceService(McpDbContext dbContext, ILogger<McpServiceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<McpServiceDto>> GetServicesAsync(string? category = null, bool enabledOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.McpServices.AsQueryable();

        if (enabledOnly)
        {
            query = query.Where(s => s.IsEnabled);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(s => s.Category.ToLower() == category.ToLower());
        }

        var services = await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.DisplayName)
            .ToListAsync(cancellationToken);

        return services.Select(MapToDto).ToList();
    }

    public async Task<McpServiceDto?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.McpServices.FindAsync([id], cancellationToken);
        return service != null ? MapToDto(service) : null;
    }

    public async Task<List<McpCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.McpServices
            .Where(s => s.IsEnabled)
            .GroupBy(s => s.Category)
            .Select(g => new McpCategoryDto
            {
                Name = g.Key,
                DisplayName = GetCategoryDisplayName(g.Key),
                IconName = GetCategoryIcon(g.Key),
                ServiceCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<McpServiceDto> CreateServiceAsync(McpServiceRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        var service = new McpService
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Category = request.Category,
            IconUrl = request.IconUrl,
            EndpointRoute = request.EndpointRoute,
            IsEnabled = request.IsEnabled,
            IsFree = request.IsFree,
            DisplayOrder = request.DisplayOrder,
            Version = request.Version,
            Author = request.Author,
            DocumentationUrl = request.DocumentationUrl,
            Tags = request.Tags,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _dbContext.McpServices.Add(service);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created MCP service: {ServiceName}", service.Name);

        return MapToDto(service);
    }

    public async Task<McpServiceDto?> UpdateServiceAsync(Guid id, McpServiceRequest request, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.McpServices.FindAsync([id], cancellationToken);
        if (service == null)
        {
            return null;
        }

        service.Name = request.Name;
        service.DisplayName = request.DisplayName;
        service.Description = request.Description;
        service.Category = request.Category;
        service.IconUrl = request.IconUrl;
        service.EndpointRoute = request.EndpointRoute;
        service.IsEnabled = request.IsEnabled;
        service.IsFree = request.IsFree;
        service.DisplayOrder = request.DisplayOrder;
        service.Version = request.Version;
        service.Author = request.Author;
        service.DocumentationUrl = request.DocumentationUrl;
        service.Tags = request.Tags;
        service.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated MCP service: {ServiceName}", service.Name);

        return MapToDto(service);
    }

    public async Task<bool> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.McpServices.FindAsync([id], cancellationToken);
        if (service == null)
        {
            return false;
        }

        _dbContext.McpServices.Remove(service);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted MCP service: {ServiceName}", service.Name);

        return true;
    }

    private static McpServiceDto MapToDto(McpService service)
    {
        return new McpServiceDto
        {
            Id = service.Id,
            Name = service.Name,
            DisplayName = service.DisplayName,
            Description = service.Description,
            Category = service.Category,
            IconUrl = service.IconUrl,
            EndpointRoute = service.EndpointRoute,
            IsEnabled = service.IsEnabled,
            IsFree = service.IsFree,
            DisplayOrder = service.DisplayOrder,
            Version = service.Version,
            Author = service.Author,
            DocumentationUrl = service.DocumentationUrl,
            Tags = service.Tags
        };
    }

    private static string GetCategoryDisplayName(string category) => category.ToLower() switch
    {
        "image" => "图片生成",
        "email" => "邮件服务",
        "document" => "文档处理",
        "data" => "数据服务",
        "ai" => "AI 服务",
        _ => category
    };

    private static string GetCategoryIcon(string category) => category.ToLower() switch
    {
        "image" => "Image",
        "email" => "Email",
        "document" => "Description",
        "data" => "DataObject",
        "ai" => "Psychology",
        _ => "Extension"
    };
}
