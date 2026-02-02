namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents a chat room where users and AI agents can communicate
/// </summary>
public class ChatRoom
{
    /// <summary>
    /// Unique identifier for the chat room
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the chat room
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of the chat room
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Avatar URL for the chat room
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// List of agent IDs that participate in this chat room
    /// </summary>
    public List<string> AgentIds { get; set; } = new();

    /// <summary>
    /// When the chat room was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the chat room was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
