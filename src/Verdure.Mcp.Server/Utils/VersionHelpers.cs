using System.Reflection;
using System.Runtime.InteropServices;

namespace Verdure.Mcp.Server.Utils;

/// <summary>
/// Provides version and runtime information for the Verdure MCP Server API.
/// </summary>
public static class VersionHelpers
{
    private static readonly Lazy<string?> _apiDisplayVersion = new(() =>
        Assembly.GetExecutingAssembly().GetDisplayVersion());

    private static readonly Lazy<string?> _runtimeVersion = new(() =>
    {
        // Extract .NET version from RuntimeInformation.FrameworkDescription
        // Example: ".NET 9.0.0" -> "9.0.0"
        var frameworkDesc = RuntimeInformation.FrameworkDescription;
        var parts = frameworkDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : frameworkDesc;
    });

    /// <summary>
    /// Gets the display version of the API (e.g., "1.0.0").
    /// This version excludes the commit hash from InformationalVersion.
    /// </summary>
    public static string? ApiDisplayVersion => _apiDisplayVersion.Value;

    /// <summary>
    /// Gets the .NET runtime version (e.g., "9.0.0").
    /// </summary>
    public static string? RuntimeVersion => _runtimeVersion.Value;

    /// <summary>
    /// Gets the operating system description.
    /// </summary>
    public static string OsDescription => RuntimeInformation.OSDescription;

    /// <summary>
    /// Gets the operating system architecture.
    /// </summary>
    public static string OsArchitecture => RuntimeInformation.OSArchitecture.ToString();
}
