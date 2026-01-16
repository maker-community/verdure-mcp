using Verdure.Mcp.Domain.Enums;

namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents a binding relationship between a user and a device
/// This allows for "social" features where users can bind to other users' devices
/// </summary>
public class DeviceBinding
{
    /// <summary>
    /// Unique identifier for the binding
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID who owns the device
    /// </summary>
    public required string OwnerUserId { get; set; }

    /// <summary>
    /// Target user ID who is requesting/has access to the device
    /// </summary>
    public required string TargetUserId { get; set; }

    /// <summary>
    /// Device ID being bound
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Status of the binding (pending, active, rejected, revoked)
    /// </summary>
    public DeviceBindingStatus Status { get; set; } = DeviceBindingStatus.Pending;

    /// <summary>
    /// When the binding was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the binding status was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to the device
    /// </summary>
    public Device? Device { get; set; }
}
