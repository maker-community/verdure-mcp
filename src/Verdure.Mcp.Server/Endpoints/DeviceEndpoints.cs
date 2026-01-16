using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Server.Endpoints;

/// <summary>
/// Device management API endpoints
/// </summary>
public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .RequireAuthorization();

        // Get all devices for the current user
        group.MapGet("/", async (
            ClaimsPrincipal user,
            McpDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var devices = await dbContext.Devices
                .Where(d => d.OwnerUserId == userId)
                .OrderByDescending(d => d.LastSeenAt)
                .Select(d => new DeviceDto
                {
                    Id = d.Id,
                    MacAddress = d.MacAddress,
                    Status = d.Status.ToString(),
                    LastSeenAt = d.LastSeenAt,
                    Metadata = d.Metadata,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new ApiResponse<List<DeviceDto>>
            {
                Success = true,
                Data = devices
            });
        })
        .WithName("GetUserDevices");

        // Get a specific device
        group.MapGet("/{deviceId:guid}", async (
            Guid deviceId,
            ClaimsPrincipal user,
            McpDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .Where(d => d.Id == deviceId && d.OwnerUserId == userId)
                .Select(d => new DeviceDto
                {
                    Id = d.Id,
                    MacAddress = d.MacAddress,
                    Status = d.Status.ToString(),
                    LastSeenAt = d.LastSeenAt,
                    Metadata = d.Metadata,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (device == null)
            {
                return Results.NotFound(new ApiResponse<DeviceDto>
                {
                    Success = false,
                    Message = "Device not found"
                });
            }

            return Results.Ok(new ApiResponse<DeviceDto>
            {
                Success = true,
                Data = device
            });
        })
        .WithName("GetDevice");

        // Get active connections for a device
        group.MapGet("/{deviceId:guid}/connections", async (
            Guid deviceId,
            ClaimsPrincipal user,
            McpDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            // Verify device ownership
            var device = await dbContext.Devices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == userId, cancellationToken);
            
            if (device == null)
            {
                return Results.NotFound(new ApiResponse<List<DeviceConnectionDto>>
                {
                    Success = false,
                    Message = "Device not found"
                });
            }

            var connections = await dbContext.DeviceConnections
                .Where(c => c.DeviceId == deviceId)
                .OrderByDescending(c => c.ConnectedAt)
                .Select(c => new DeviceConnectionDto
                {
                    ConnectionId = c.ConnectionId,
                    DeviceId = c.DeviceId,
                    UserId = c.UserId,
                    ConnectedAt = c.ConnectedAt,
                    LastHeartbeatAt = c.LastHeartbeatAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new ApiResponse<List<DeviceConnectionDto>>
            {
                Success = true,
                Data = connections
            });
        })
        .WithName("GetDeviceConnections");

        // Send a message to a specific device
        group.MapPost("/{deviceId:guid}/send", async (
            Guid deviceId,
            SendDeviceMessageRequest request,
            ClaimsPrincipal user,
            McpDbContext dbContext,
            IDevicePushService pushService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            // Verify device ownership
            var device = await dbContext.Devices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == userId, cancellationToken);
            
            if (device == null)
            {
                return Results.NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Device not found"
                });
            }

            // Send the message
            await pushService.SendToDeviceAsync(deviceId, request.Method, request.Payload, cancellationToken);

            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Message sent to device"
            });
        })
        .WithName("SendDeviceMessage");

        // Delete a device
        group.MapDelete("/{deviceId:guid}", async (
            Guid deviceId,
            ClaimsPrincipal user,
            McpDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == userId, cancellationToken);
            
            if (device == null)
            {
                return Results.NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Device not found"
                });
            }

            dbContext.Devices.Remove(device);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Device deleted"
            });
        })
        .WithName("DeleteDevice");
    }
}

/// <summary>
/// DTO for device information
/// </summary>
public class DeviceDto
{
    public Guid Id { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for device connection information
/// </summary>
public class DeviceConnectionDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public string? UserId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
}

/// <summary>
/// Request to send a message to a device
/// </summary>
public class SendDeviceMessageRequest
{
    public required string Method { get; set; }
    public required object Payload { get; set; }
}
