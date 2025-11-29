namespace Verdure.Mcp.Server.Settings;

/// <summary>
/// Authentication settings
/// </summary>
public class AuthenticationSettings
{
    public const string SectionName = "Authentication";
    
    /// <summary>
    /// Whether to require token authentication
    /// </summary>
    public bool RequireToken { get; set; } = true;
}
