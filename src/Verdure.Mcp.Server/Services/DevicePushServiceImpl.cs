using Microsoft.AspNetCore.SignalR;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Hubs;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Concrete implementation of device push service for the DeviceHub
/// </summary>
public class DevicePushServiceImpl : IDevicePushService
{
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly ILogger<DevicePushServiceImpl> _logger;

    public DevicePushServiceImpl(
        IHubContext<DeviceHub> hubContext,
        ILogger<DevicePushServiceImpl> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendToUserAsync(string userId, string method, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = $"Users:{userId}";
        _logger.LogInformation("Sending message to user group {GroupName}, method: {Method}", groupName, method);
        
        await _hubContext.Clients.Group(groupName).SendAsync(method, payload, cancellationToken);
    }

    public async Task SendToDeviceAsync(Guid deviceId, string method, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = $"Device:{deviceId}";
        _logger.LogInformation("Sending message to device {DeviceId}, method: {Method}", deviceId, method);
        
        await _hubContext.Clients.Group(groupName).SendAsync(method, payload, cancellationToken);
    }

    public async Task SendCustomMessageAsync(string userId, object message, CancellationToken cancellationToken = default)
    {
        await SendToUserAsync(userId, "CustomMessage", message, cancellationToken);
    }

    public async Task SendNotificationAsync(string userId, string message, CancellationToken cancellationToken = default)
    {
        await SendToUserAsync(userId, "Notification", new { message, timestamp = DateTime.UtcNow }, cancellationToken);
    }
}
