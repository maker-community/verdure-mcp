namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents a message in a chat room
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the chat room this message belongs to
    /// </summary>
    public Guid ChatRoomId { get; set; }

    /// <summary>
    /// User ID that this message session belongs to
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Sender ID (user ID or agent ID)
    /// </summary>
    public required string SenderId { get; set; }

    /// <summary>
    /// Whether this message is from an agent (true) or user (false)
    /// </summary>
    public bool IsAgent { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Metadata as JSON (image URLs, audio URLs, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property to the chat room
    /// </summary>
    public ChatRoom ChatRoom { get; set; } = null!;
}
