using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using MudBlazor.Services;
using Verdure.Mcp.Web;
using Verdure.Mcp.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API base address
var apiBaseAddress = builder.HostEnvironment.BaseAddress.TrimEnd('/');

// Add MudBlazor with Material Design 3 theme configuration
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

// Add authentication with OIDC
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    // Add offline_access scope to get refresh token
    options.ProviderOptions.DefaultScopes.Add("offline_access");
})
.AddAccountClaimsPrincipalFactory<KeycloakRoleClaimsPrincipalFactory>();

// Configure authorization with case-insensitive admin policy
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireAssertion(context =>
            context.User.Claims.Any(c =>
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" &&
                c.Value.Equals("admin", StringComparison.OrdinalIgnoreCase))));
});

// Register custom authorization message handler for automatic token attachment
builder.Services.AddScoped<CustomAuthorizationMessageHandler>();

// Configure HTTP client for API with automatic token attachment
builder.Services.AddHttpClient("Verdure.Mcp.Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
})
.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// Configure default HTTP client - uses the named HttpClient
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("Verdure.Mcp.Api"));

// Add custom services
builder.Services.AddScoped<IMcpServiceClient, McpServiceClient>();
builder.Services.AddScoped<ITokenServiceClient, TokenServiceClient>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();

await builder.Build().RunAsync();
