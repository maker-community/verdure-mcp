using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Net;

namespace Verdure.Mcp.Web.Services;

/// <summary>
/// Custom authorization message handler that configures itself automatically
/// and handles authentication expiration by redirecting to login
/// </summary>
public class CustomAuthorizationMessageHandler : AuthorizationMessageHandler
{
    private readonly NavigationManager _navigation;
    private readonly ILogger<CustomAuthorizationMessageHandler> _logger;

    public CustomAuthorizationMessageHandler(
        IAccessTokenProvider provider,
        NavigationManager navigation,
        ILogger<CustomAuthorizationMessageHandler> logger)
        : base(provider, navigation)
    {
        _navigation = navigation;
        _logger = logger;

        // Configure authorized URLs - only authorize requests to the API
        // The base address will be configured by the HttpClient factory
        ConfigureHandler(
            authorizedUrls: new[] { navigation.BaseUri },
            scopes: new[] { "openid", "profile", "email" });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Handle 401 Unauthorized - redirect to login
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized, redirecting to login");
                _navigation.NavigateTo("authentication/login", forceLoad: true);
            }

            return response;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            // Token is not available or expired - redirect to authentication flow
            _logger.LogWarning("Access token not available, redirecting to login");
            ex.Redirect();
            throw;
        }
    }
}
