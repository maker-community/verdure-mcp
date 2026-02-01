namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents a user's membership in a chat room
/// </summary>
public class UserChatRoomMembership
{
    /// <summary>
    /// Unique identifier for the membership
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID of the member
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// ID of the chat room
    /// </summary>
    public Guid ChatRoomId { get; set; }

    /// <summary>
    /// Whether this is the user's default chat room
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// When the user joined the chat room
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Navigation property to the chat room
    /// </summary>
    public ChatRoom ChatRoom { get; set; } = null!;
}
