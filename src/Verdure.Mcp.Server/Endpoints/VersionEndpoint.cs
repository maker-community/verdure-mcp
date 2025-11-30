using Verdure.Mcp.Server.Utils;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Server.Endpoints;

/// <summary>
/// Version information endpoint
/// </summary>
public static class VersionEndpoint
{
    public static void MapVersionEndpoint(this WebApplication app)
    {
        app.MapGet("/api/version", () =>
        {
            var versionInfo = new VersionInfoResponse
            {
                ApiVersion = VersionHelpers.ApiDisplayVersion ?? "Unknown",
                RuntimeVersion = VersionHelpers.RuntimeVersion ?? "Unknown",
                OsDescription = VersionHelpers.OsDescription,
                OsArchitecture = VersionHelpers.OsArchitecture
            };

            return Results.Ok(new ApiResponse<VersionInfoResponse>
            {
                Success = true,
                Data = versionInfo
            });
        })
        .WithName("GetVersionInfo")
        .WithTags("System")
        .AllowAnonymous();
    }
}

/// <summary>
/// Version information response
/// </summary>
public record VersionInfoResponse
{
    public string ApiVersion { get; init; } = string.Empty;
    public string RuntimeVersion { get; init; } = string.Empty;
    public string OsDescription { get; init; } = string.Empty;
    public string OsArchitecture { get; init; } = string.Empty;
}
