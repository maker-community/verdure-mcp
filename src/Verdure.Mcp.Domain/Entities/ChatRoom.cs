namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents an AI chat room/group where users can interact with multiple agents
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
    /// Avatar/icon URL for the chat room
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// List of agent IDs that are part of this chat room
    /// Stored as JSON array
    /// </summary>
    public string AgentIds { get; set; } = "[]";

    /// <summary>
    /// When the chat room was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the chat room was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
