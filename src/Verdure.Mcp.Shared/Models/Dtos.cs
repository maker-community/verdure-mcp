namespace Verdure.Mcp.Shared.Models;

/// <summary>
/// DTO for MCP service information
/// </summary>
public class McpServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string EndpointRoute { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsFree { get; set; }
    public int DisplayOrder { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? DocumentationUrl { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// Request to create or update an MCP service
/// </summary>
public class McpServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string EndpointRoute { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsFree { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? DocumentationUrl { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// DTO for API token information
/// </summary>
public class ApiTokenDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int DailyImageLimit { get; set; }
    public int TodayImageCount { get; set; }
}

/// <summary>
/// Response after creating a new token
/// </summary>
public class CreateTokenResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a new token
/// </summary>
public class CreateTokenRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// User info from Keycloak
/// </summary>
public class UserInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsAdmin => Roles.Contains("admin");
}

/// <summary>
/// Response wrapper for API responses
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Pagination wrapper
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Available MCP categories
/// </summary>
public class McpCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconName { get; set; }
    public int ServiceCount { get; set; }
}
