namespace Verdure.ImageMcp.Domain.Entities;

/// <summary>
/// Represents an API token for authentication
/// </summary>
public class ApiToken
{
    /// <summary>
    /// Unique identifier for the token
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The token value (hashed for security)
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Name/description for the token
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether the token is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the token was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the token expires (null means never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the token was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
