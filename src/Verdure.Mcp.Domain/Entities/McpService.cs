namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents an MCP service that can be listed in the marketplace
/// </summary>
public class McpService
{
    /// <summary>
    /// Unique identifier for the MCP service
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the MCP service
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name of the MCP service
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Description of the MCP service
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category of the MCP service (e.g., "image", "email", "document")
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Icon URL or icon name for the MCP service
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// The endpoint route for this MCP service (e.g., "/image/mcp")
    /// </summary>
    public required string EndpointRoute { get; set; }

    /// <summary>
    /// Whether the service is enabled and visible to users
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the service is free to use
    /// </summary>
    public bool IsFree { get; set; } = true;

    /// <summary>
    /// Order for display sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Version of the MCP service
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Author of the MCP service
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Documentation URL
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Tags for filtering (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// When the service was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the service was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User ID of the admin who created this service
    /// </summary>
    public string? CreatedByUserId { get; set; }
}
