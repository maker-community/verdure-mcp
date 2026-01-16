using System.Security.Claims;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Server.Endpoints;

/// <summary>
/// Extension methods for mapping API endpoints
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapMcpServiceEndpoints();
        app.MapTokenEndpoints();
        app.MapDeviceEndpoints();
    }

    private static void MapMcpServiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mcp-services")
            .WithTags("MCP Services");

        // Get all services (public)
        group.MapGet("/", async (
            string? category,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            var services = await service.GetServicesAsync(category, true, cancellationToken);
            return Results.Ok(new ApiResponse<List<McpServiceDto>>
            {
                Success = true,
                Data = services
            });
        })
        .WithName("GetMcpServices")
        .AllowAnonymous();

        // Get paginated services (public) - for scroll loading
        group.MapGet("/paged", async (
            int page,
            int pageSize,
            string? category,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var result = await service.GetServicesPagedAsync(page, pageSize, category, true, cancellationToken);
            return Results.Ok(new ApiResponse<PagedResult<McpServiceDto>>
            {
                Success = true,
                Data = result
            });
        })
        .WithName("GetMcpServicesPaged")
        .AllowAnonymous();

        // Get all services for admin (includes disabled) with pagination
        group.MapGet("/admin/paged", async (
            int page,
            int pageSize,
            string? category,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var result = await service.GetServicesPagedAsync(page, pageSize, category, false, cancellationToken);
            return Results.Ok(new ApiResponse<PagedResult<McpServiceDto>>
            {
                Success = true,
                Data = result
            });
        })
        .WithName("GetMcpServicesPagedAdmin")
        .RequireAuthorization("AdminPolicy");

        // Get categories (public)
        group.MapGet("/categories", async (
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            var categories = await service.GetCategoriesAsync(cancellationToken);
            return Results.Ok(new ApiResponse<List<McpCategoryDto>>
            {
                Success = true,
                Data = categories
            });
        })
        .WithName("GetMcpCategories")
        .AllowAnonymous();

        // Get single service (public)
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetServiceAsync(id, cancellationToken);
            if (result == null)
            {
                return Results.NotFound(new ApiResponse<McpServiceDto>
                {
                    Success = false,
                    Message = "Service not found"
                });
            }
            return Results.Ok(new ApiResponse<McpServiceDto>
            {
                Success = true,
                Data = result
            });
        })
        .WithName("GetMcpService")
        .AllowAnonymous();

        // Create service (admin only)
        group.MapPost("/", async (
            McpServiceRequest request,
            IMcpServiceService service,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            var result = await service.CreateServiceAsync(request, userId, cancellationToken);
            return Results.Created($"/api/mcp-services/{result.Id}", new ApiResponse<McpServiceDto>
            {
                Success = true,
                Data = result
            });
        })
        .WithName("CreateMcpService")
        .RequireAuthorization("AdminPolicy");

        // Update service (admin only)
        group.MapPut("/{id:guid}", async (
            Guid id,
            McpServiceRequest request,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateServiceAsync(id, request, cancellationToken);
            if (result == null)
            {
                return Results.NotFound(new ApiResponse<McpServiceDto>
                {
                    Success = false,
                    Message = "Service not found"
                });
            }
            return Results.Ok(new ApiResponse<McpServiceDto>
            {
                Success = true,
                Data = result
            });
        })
        .WithName("UpdateMcpService")
        .RequireAuthorization("AdminPolicy");

        // Delete service (admin only)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IMcpServiceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteServiceAsync(id, cancellationToken);
            if (!result)
            {
                return Results.NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Service not found"
                });
            }
            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Service deleted"
            });
        })
        .WithName("DeleteMcpService")
        .RequireAuthorization("AdminPolicy");
    }

    private static void MapTokenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tokens")
            .WithTags("Tokens")
            .RequireAuthorization();

        // Get user tokens
        group.MapGet("/", async (
            ClaimsPrincipal user,
            ITokenValidationService tokenService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var tokens = await tokenService.GetUserTokensAsync(userId, cancellationToken);
            var dtos = tokens.Select(t => new ApiTokenDto
            {
                Id = t.Id,
                Name = t.Name,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                LastUsedAt = t.LastUsedAt,
                DailyImageLimit = t.DailyImageLimit,
                TodayImageCount = t.TodayImageCount
            }).ToList();

            return Results.Ok(new ApiResponse<List<ApiTokenDto>>
            {
                Success = true,
                Data = dtos
            });
        })
        .WithName("GetUserTokens");

        // Create token
        group.MapPost("/", async (
            CreateTokenRequest request,
            ClaimsPrincipal user,
            ITokenValidationService tokenService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var token = await tokenService.CreateUserTokenAsync(userId, request.Name, cancellationToken);
            
            // Get the token details to return expiration info
            var tokens = await tokenService.GetUserTokensAsync(userId, cancellationToken);
            var createdToken = tokens.FirstOrDefault(t => t.Name == request.Name);

            var response = new CreateTokenResponse
            {
                Id = createdToken?.Id ?? Guid.Empty,
                Token = token,
                Name = request.Name,
                ExpiresAt = createdToken?.ExpiresAt,
                Message = "请妥善保管此密钥，它只会显示一次。"
            };

            return Results.Ok(new ApiResponse<CreateTokenResponse>
            {
                Success = true,
                Data = response
            });
        })
        .WithName("CreateUserToken");

        // Revoke token
        group.MapDelete("/{tokenId:guid}", async (
            Guid tokenId,
            ClaimsPrincipal user,
            ITokenValidationService tokenService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                         user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var result = await tokenService.RevokeTokenAsync(tokenId, userId, cancellationToken);
            if (!result)
            {
                return Results.NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Token not found or not owned by user"
                });
            }

            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Token revoked"
            });
        })
        .WithName("RevokeToken");
    }
}
