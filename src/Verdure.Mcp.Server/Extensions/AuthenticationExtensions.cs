using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Text.Json;

namespace Verdure.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for configuring authentication with Keycloak role mapping
/// Reference: https://github.com/maker-community/verdure-mcp-for-xiaozhi
/// </summary>
internal static class AuthenticationExtensions
{
    /// <summary>
    /// Map Keycloak resource_access roles to ASP.NET Core standard roles
    /// Supports both resource_access and realm_access claims
    /// </summary>
    public static Task MapKeycloakRolesToStandardRoles(
        TokenValidatedContext context,
        string? clientId = null,
        ILogger? logger = null)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return Task.CompletedTask;
        }

        logger ??= context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();

        try
        {
            // 1. Extract resource_access roles (client-specific roles)
            if (!string.IsNullOrEmpty(clientId))
            {
                var resourceAccessClaim = identity.FindFirst("resource_access")?.Value;
                
                if (!string.IsNullOrEmpty(resourceAccessClaim))
                {
                    var resourceAccess = JsonDocument.Parse(resourceAccessClaim);
                    
                    if (resourceAccess.RootElement.TryGetProperty(clientId, out var clientResource))
                    {
                        if (clientResource.TryGetProperty("roles", out var rolesElement))
                        {
                            var roles = rolesElement.EnumerateArray()
                                .Select(r => r.GetString())
                                .Where(r => !string.IsNullOrEmpty(r))
                                .ToList();

                            if (roles.Any())
                            {
                                logger.LogInformation(
                                    "Mapping {Count} roles from resource_access.{ClientId}: {Roles}",
                                    roles.Count,
                                    clientId,
                                    string.Join(", ", roles));

                                foreach (var role in roles)
                                {
                                    if (!identity.HasClaim(ClaimTypes.Role, role!))
                                    {
                                        identity.AddClaim(new Claim(ClaimTypes.Role, role!));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var availableClients = string.Join(", ",
                            resourceAccess.RootElement.EnumerateObject().Select(p => p.Name));
                        
                        logger.LogWarning(
                            "ClientId '{ClientId}' not found in resource_access. Available clients: {Clients}",
                            clientId,
                            availableClients);
                    }
                }
            }

            // 2. Extract realm_access roles (realm-level roles)
            var realmAccessClaim = identity.FindFirst("realm_access")?.Value;
            
            if (!string.IsNullOrEmpty(realmAccessClaim))
            {
                var realmAccess = JsonDocument.Parse(realmAccessClaim);
                
                if (realmAccess.RootElement.TryGetProperty("roles", out var realmRoles))
                {
                    var allRealmRoles = realmRoles.EnumerateArray()
                        .Select(r => r.GetString())
                        .Where(r => !string.IsNullOrEmpty(r))
                        .ToList();

                    var relevantRoles = allRealmRoles
                        .Where(r => IsRelevantRealmRole(r!))
                        .ToList();

                    if (relevantRoles.Any())
                    {
                        logger.LogInformation(
                            "Mapping {Count} realm roles (filtered from {Total}): {Roles}",
                            relevantRoles.Count,
                            allRealmRoles.Count,
                            string.Join(", ", relevantRoles));

                        foreach (var role in relevantRoles)
                        {
                            if (!identity.HasClaim(ClaimTypes.Role, role!))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, role!));
                            }
                        }
                    }
                }
            }

            // 3. Log all mapped roles for debugging
            var allRoles = identity.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            
            if (allRoles.Any())
            {
                logger.LogInformation(
                    "User {UserId} authenticated with roles: {Roles}",
                    identity.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    string.Join(", ", allRoles));
            }
            else
            {
                logger.LogWarning(
                    "User {UserId} has no roles mapped - check token claims and configuration",
                    identity.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Failed to parse role claims for role mapping - Invalid JSON");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error during role mapping");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determine if a realm role should be mapped to ASP.NET Core roles
    /// Filters out default Keycloak roles that are not relevant for authorization
    /// </summary>
    private static bool IsRelevantRealmRole(string role)
    {
        var excludedRoles = new[]
        {
            "offline_access",
            "uma_authorization",
            "default-roles-verdure-mcp",
            "default-roles-maker-community"
        };

        return !excludedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
