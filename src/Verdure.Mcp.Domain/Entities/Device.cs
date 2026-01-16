using Verdure.Mcp.Domain.Enums;

namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents an IoT device (e.g., ESP32) that can connect to the SignalR hub
/// </summary>
public class Device
{
    /// <summary>
    /// Unique identifier for the device
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// MAC address of the device (unique identifier from hardware)
    /// </summary>
    public required string MacAddress { get; set; }

    /// <summary>
    /// User ID of the device owner (from Keycloak/auth system)
    /// </summary>
    public string? OwnerUserId { get; set; }

    /// <summary>
    /// When the device was last seen (last connected or heartbeat)
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Current status of the device
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;

    /// <summary>
    /// JSON metadata about the device (firmware version, type, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the device was first registered
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the device record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
