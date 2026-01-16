namespace Verdure.Mcp.Infrastructure.Services;

/// <summary>
/// Interface for device push service
/// </summary>
public interface IDevicePushService
{
    /// <summary>
    /// Send a message to all devices owned by a specific user
    /// </summary>
    Task SendToUserAsync(string userId, string method, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a message to a specific device by device ID
    /// </summary>
    Task SendToDeviceAsync(Guid deviceId, string method, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a custom message to a specific user
    /// </summary>
    Task SendCustomMessageAsync(string userId, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a notification to a specific user
    /// </summary>
    Task SendNotificationAsync(string userId, string message, CancellationToken cancellationToken = default);
}
