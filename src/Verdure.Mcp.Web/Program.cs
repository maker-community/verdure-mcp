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

// Add MudBlazor
builder.Services.AddMudServices();

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
