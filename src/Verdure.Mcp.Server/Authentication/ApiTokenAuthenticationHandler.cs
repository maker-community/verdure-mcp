using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Verdure.Mcp.Infrastructure.Services;

namespace Verdure.Mcp.Server.Authentication;

public class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenValidationService _tokenValidationService;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenValidationService tokenValidationService)
        : base(options, logger, encoder)
    {
        _tokenValidationService = tokenValidationService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            string? token = null;
            var auth = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = auth.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(token))
            {
                token = Request.Query["access_token"].FirstOrDefault();
            }

            if (string.IsNullOrEmpty(token))
            {
                // Treat missing token as an explicit authentication failure so the pipeline
                // produces a 401 challenge response rather than silently continuing.
                return AuthenticateResult.Fail("Missing API token");
            }

            var apiToken = await _tokenValidationService.GetTokenAsync(token, Context.RequestAborted);
            if (apiToken == null || !apiToken.IsActive)
            {
                return AuthenticateResult.Fail("Invalid API token");
            }

            //if (apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTime.UtcNow)
            //{
            //    return AuthenticateResult.Fail("API token expired");
            //}

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, apiToken.UserId ?? string.Empty),
                new Claim("sub", apiToken.UserId ?? string.Empty),
                new Claim("token_name", apiToken.Name ?? string.Empty),
                new Claim("auth_type", "apitoken")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "API token authentication failed");
            return AuthenticateResult.Fail("API token authentication error");
        }
    }
}
