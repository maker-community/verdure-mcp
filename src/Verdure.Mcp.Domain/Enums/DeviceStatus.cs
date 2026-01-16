namespace Verdure.Mcp.Domain.Enums;

/// <summary>
/// Represents the status of a device
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is offline
    /// </summary>
    Offline = 0,
    
    /// <summary>
    /// Device is online and connected
    /// </summary>
    Online = 1,
    
    /// <summary>
    /// Device is registered but not connected
    /// </summary>
    Registered = 2
}
