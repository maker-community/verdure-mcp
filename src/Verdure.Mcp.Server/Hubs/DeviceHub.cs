using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using System.Text.Json;

namespace Verdure.Mcp.Server.Hubs;

/// <summary>
/// SignalR Hub for ESP32 and IoT device connections
/// Devices connect using access_token for authentication
/// </summary>
public class DeviceHub : Hub
{
    private readonly McpDbContext _dbContext;
    private readonly ITokenValidationService _tokenValidationService;
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(
        McpDbContext dbContext,
        ITokenValidationService tokenValidationService,
        ILogger<DeviceHub> logger)
    {
        _dbContext = dbContext;
        _tokenValidationService = tokenValidationService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a device connects to the hub
    /// Authenticates the device via access_token query parameter
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            _logger.LogWarning("No HTTP context available for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Get access_token from query string (ESP32 sends it this way)
        var accessToken = httpContext.Request.Query["access_token"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("No access_token provided for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        _logger.LogInformation("Device connecting with token (length: {Length})", accessToken.Length);

        // Try to validate as API token first
        var apiToken = await _tokenValidationService.GetTokenAsync(accessToken);
        string? userId = null;

        if (apiToken != null && apiToken.IsActive)
        {
            // Valid API token from database
            if (apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired API token for connection {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
            userId = apiToken.UserId;
            _logger.LogInformation("Authenticated via API token, UserId: {UserId}", userId);
        }
        else
        {
            // Try to validate as JWT token (Keycloak)
            try
            {
                var claimsPrincipal = await _tokenValidationService.ValidateJwtTokenAsync(accessToken);
                if (claimsPrincipal != null)
                {
                    userId = claimsPrincipal.FindFirst("sub")?.Value;
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = claimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    }
                    _logger.LogInformation("Authenticated via JWT token, UserId: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JWT token validation failed for connection {ConnectionId}", Context.ConnectionId);
            }
        }
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Token validation failed or no userId found for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Token has no associated userId for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Store userId in the connection context for later use
        Context.Items["UserId"] = userId;
        Context.Items["Token"] = apiToken;

        // Add connection to user group
        var userGroup = $"Users:{userId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);

        _logger.LogInformation(
            "Device connected: ConnectionId={ConnectionId}, UserId={UserId}", 
            Context.ConnectionId, userId);

        // Send welcome notification to the device (ESP32 expects a string, not an object)
        await Clients.Caller.SendAsync("Notification", 
            $"Connected to Verdure MCP Device Hub. ConnectionId: {Context.ConnectionId}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a device disconnects from the hub
    /// Updates device status to offline
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Device disconnected: ConnectionId={ConnectionId}, Exception={Exception}",
            Context.ConnectionId, exception?.Message);

        // Find and remove the connection record
        var connection = await _dbContext.DeviceConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

        if (connection != null)
        {
            // Update device status to offline
            var device = await _dbContext.Devices.FindAsync(connection.DeviceId);
            if (device != null)
            {
                device.Status = DeviceStatus.Offline;
                device.LastSeenAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
            }

            _dbContext.DeviceConnections.Remove(connection);
            await _dbContext.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Register a device with MAC address, device token, and metadata
    /// This is called by the device after connecting
    /// </summary>
    public async Task RegisterDevice(string macAddress, string? deviceToken = null, string? metadata = null)
    {
        var userId = Context.Items["UserId"] as string;
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("RegisterDevice called without authenticated user");
            throw new HubException("Not authenticated");
        }

        _logger.LogInformation(
            "RegisterDevice called: MAC={MacAddress}, UserId={UserId}",
            macAddress, userId);

        // Find or create device
        var device = await _dbContext.Devices
            .FirstOrDefaultAsync(d => d.MacAddress == macAddress);

        if (device == null)
        {
            // Create new device
            device = new Device
            {
                Id = Guid.NewGuid(),
                MacAddress = macAddress,
                OwnerUserId = userId,
                Status = DeviceStatus.Online,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            _dbContext.Devices.Add(device);
        }
        else
        {
            // Update existing device
            device.OwnerUserId = userId;
            device.Status = DeviceStatus.Online;
            device.LastSeenAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(metadata))
            {
                device.Metadata = metadata;
            }
        }

        await _dbContext.SaveChangesAsync();

        // Create or update connection record
        var connection = await _dbContext.DeviceConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

        if (connection == null)
        {
            connection = new DeviceConnection
            {
                ConnectionId = Context.ConnectionId,
                DeviceId = device.Id,
                UserId = userId,
                ConnectedAt = DateTime.UtcNow,
                LastHeartbeatAt = DateTime.UtcNow
            };
            _dbContext.DeviceConnections.Add(connection);
        }
        else
        {
            connection.DeviceId = device.Id;
            connection.UserId = userId;
            connection.LastHeartbeatAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        // Add to device-specific group
        var deviceGroup = $"Device:{device.Id}";
        await Groups.AddToGroupAsync(Context.ConnectionId, deviceGroup);

        _logger.LogInformation(
            "Device registered: DeviceId={DeviceId}, MAC={MacAddress}, UserId={UserId}",
            device.Id, macAddress, userId);

        // Send registration confirmation
        await Clients.Caller.SendAsync("DeviceRegistered", new
        {
            deviceId = device.Id,
            macAddress = device.MacAddress,
            status = device.Status.ToString(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Update heartbeat timestamp for the connection
    /// Devices can call this periodically to maintain connection status
    /// </summary>
    public async Task Heartbeat()
    {
        var connection = await _dbContext.DeviceConnections
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

        if (connection != null)
        {
            connection.LastHeartbeatAt = DateTime.UtcNow;
            
            if (connection.Device != null)
            {
                connection.Device.LastSeenAt = DateTime.UtcNow;
                connection.Device.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}
