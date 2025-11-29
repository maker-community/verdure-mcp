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

    /// <summary>
    /// Default token expiration in days
    /// </summary>
    public int DefaultTokenExpirationDays { get; set; } = 30;

    /// <summary>
    /// Default daily image generation limit per user
    /// </summary>
    public int DefaultDailyImageLimit { get; set; } = 10;
}

/// <summary>
/// Keycloak authentication settings
/// </summary>
public class KeycloakSettings
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Keycloak server URL (e.g., https://keycloak.example.com)
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak realm name
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Client ID for the application
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret (for confidential clients)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Audience to validate
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Whether to require HTTPS for metadata
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Admin role name
    /// </summary>
    public string AdminRole { get; set; } = "admin";

    /// <summary>
    /// Full authority URL with realm. Returns empty if configuration is invalid.
    /// </summary>
    public string RealmAuthority
    {
        get
        {
            if (string.IsNullOrEmpty(Authority) || string.IsNullOrEmpty(Realm))
            {
                return string.Empty;
            }

            var baseUri = Authority.TrimEnd('/');
            var realmPath = $"/realms/{Uri.EscapeDataString(Realm)}";

            if (Uri.TryCreate(baseUri + realmPath, UriKind.Absolute, out var result))
            {
                return result.ToString();
            }

            return string.Empty;
        }
    }
}
