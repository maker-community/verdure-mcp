using System.Reflection;

namespace Verdure.Mcp.Server.Utils;

/// <summary>
/// Extension methods for extracting version information from assemblies.
/// </summary>
public static class AssemblyExtensions
{
    /// <summary>
    /// Gets the display version by extracting version information from assembly attributes.
    /// Returns the version without the commit hash (strips everything after '+').
    /// </summary>
    /// <param name="assembly">The assembly to extract version information from</param>
    /// <returns>Display version string (e.g., "1.0.0") or null if not found</returns>
    public static string? GetDisplayVersion(this Assembly assembly)
    {
        // Try InformationalVersion first (most detailed, contains semantic version + commit hash)
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            // Remove commit hash if present (everything after '+')
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion.Substring(0, plusIndex) : infoVersion;
        }

        // Fallback to FileVersion
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            return fileVersion;
        }

        // Last fallback to AssemblyVersion
        return assembly.GetName().Version?.ToString();
    }
}
