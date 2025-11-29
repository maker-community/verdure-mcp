using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Verdure.ImageMcp.Infrastructure.Services;
using Verdure.ImageMcp.Server.Settings;

namespace Verdure.ImageMcp.Server.Filters;

/// <summary>
/// Middleware for validating Bearer token authentication
/// </summary>
public class BearerTokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BearerTokenAuthenticationMiddleware> _logger;
    private readonly AuthenticationSettings _settings;

    // Paths that don't require authentication
    private static readonly string[] ExcludedPaths = 
    {
        "/health",
        "/openapi",
        "/scalar"
    };

    public BearerTokenAuthenticationMiddleware(
        RequestDelegate next, 
        ILogger<BearerTokenAuthenticationMiddleware> logger,
        IOptions<AuthenticationSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context, ITokenValidationService tokenValidationService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Skip authentication for excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Skip authentication if not required (e.g., in development)
        if (!_settings.RequireToken)
        {
            await _next(context);
            return;
        }

        // Extract Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("Missing Authorization header for path: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing Authorization header" });
            return;
        }

        // Check for Bearer token format
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid Authorization header format for path: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid Authorization header format. Expected: Bearer <token>" });
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Empty token in Authorization header for path: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Empty token" });
            return;
        }

        // Validate the token
        var isValid = await tokenValidationService.ValidateTokenAsync(token, context.RequestAborted);
        
        if (!isValid)
        {
            _logger.LogWarning("Invalid token for path: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token" });
            return;
        }

        _logger.LogDebug("Token validated successfully for path: {Path}", path);
        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding Bearer token authentication middleware
/// </summary>
public static class BearerTokenAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseBearerTokenAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BearerTokenAuthenticationMiddleware>();
    }
}
