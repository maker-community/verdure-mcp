using System.Reflection;
using System.Runtime.InteropServices;

namespace Verdure.Mcp.Web.Utils;

/// <summary>
/// Provides version and runtime information for the Verdure MCP Web application.
/// </summary>
public static class VersionHelpers
{
    private static readonly Lazy<string?> _webDisplayVersion = new(() =>
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
    /// Gets the display version of the Web application (e.g., "1.0.0").
    /// This version excludes the commit hash from InformationalVersion.
    /// </summary>
    public static string? WebDisplayVersion => _webDisplayVersion.Value;

    /// <summary>
    /// Gets the .NET runtime version (e.g., "9.0.0").
    /// </summary>
    public static string? RuntimeVersion => _runtimeVersion.Value;
}
