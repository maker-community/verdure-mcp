namespace Verdure.Mcp.Domain.Enums;

/// <summary>
/// Represents the status of a device binding relationship
/// </summary>
public enum DeviceBindingStatus
{
    /// <summary>
    /// Binding is pending approval
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Binding is active
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Binding has been rejected
    /// </summary>
    Rejected = 2,
    
    /// <summary>
    /// Binding has been revoked
    /// </summary>
    Revoked = 3
}
