namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents an active SignalR connection from a device
/// </summary>
public class DeviceConnection
{
    /// <summary>
    /// SignalR connection ID
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// Associated device ID
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// User ID who owns this connection (from access token)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// When the connection was established
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// When the last heartbeat was received
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>
    /// Navigation property to the device
    /// </summary>
    public Device? Device { get; set; }
}
