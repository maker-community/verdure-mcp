using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Endpoints;
using Verdure.Mcp.Server.Filters;
using Verdure.Mcp.Server.Services;
using Verdure.Mcp.Server.Settings;
using Verdure.Mcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<AzureOpenAISettings>(
    builder.Configuration.GetSection(AzureOpenAISettings.SectionName));
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<AuthenticationSettings>(
    builder.Configuration.GetSection(AuthenticationSettings.SectionName));
builder.Services.Configure<KeycloakSettings>(
    builder.Configuration.GetSection(KeycloakSettings.SectionName));
builder.Services.Configure<TokenValidationSettings>(
    builder.Configuration.GetSection(TokenValidationSettings.SectionName));

// Add database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<McpDbContext>(options =>
    options.UseNpgsql(connectionString, 
        b => b.MigrationsAssembly("Verdure.Mcp.Server")));

// Add Hangfire services
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => 
        options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// Add HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Add infrastructure services
builder.Services.AddScoped<IImageGenerationService, ImageGenerationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenValidationService, TokenValidationService>();
builder.Services.AddScoped<IMcpServiceService, McpServiceService>();

// Add MCP tool filter service
builder.Services.AddSingleton<McpToolFilterService>();

// Add background job
builder.Services.AddScoped<ImageGenerationBackgroundJob>();

// Add MCP Server with HTTP transport and route-based tool filtering
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Configure per-session options to filter tools based on route
        options.ConfigureSessionOptions = async (httpContext, mcpOptions, cancellationToken) =>
        {
            // Get the tool filter service
            var filterService = httpContext.RequestServices.GetRequiredService<McpToolFilterService>();

            // Get the tool category from route parameter
            var toolCategory = httpContext.Request.RouteValues["toolCategory"]?.ToString() ?? "all";

            // Get all registered tools
            var allTools = mcpOptions.ToolCollection?.ToList() ?? new List<ModelContextProtocol.Server.McpServerTool>();

            // Filter tools using the service
            var filteredTools = filterService.FilterTools(allTools, toolCategory);

            // Apply filtered tools to the session
            filterService.ApplyFilteredTools(mcpOptions, filteredTools);

            await Task.CompletedTask;
        };
    })
    .WithToolsFromAssembly();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure Keycloak JWT authentication
var keycloakSettings = builder.Configuration.GetSection(KeycloakSettings.SectionName).Get<KeycloakSettings>();
if (keycloakSettings != null && !string.IsNullOrEmpty(keycloakSettings.Authority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = keycloakSettings.RealmAuthority;
            options.Audience = keycloakSettings.Audience ?? keycloakSettings.ClientId;
            options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
            
            // Handle Keycloak roles from realm_access.roles claim
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // Extract roles from Keycloak realm_access claim
                    var realmAccessClaim = context.Principal?.FindFirst("realm_access");
                    if (realmAccessClaim != null)
                    {
                        try
                        {
                            var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                            if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                            {
                                var claims = new List<System.Security.Claims.Claim>();
                                foreach (var role in rolesElement.EnumerateArray())
                                {
                                    var roleValue = role.GetString();
                                    if (!string.IsNullOrEmpty(roleValue))
                                    {
                                        claims.Add(new System.Security.Claims.Claim(
                                            System.Security.Claims.ClaimTypes.Role, roleValue));
                                    }
                                }
                                
                                if (claims.Count > 0 && context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                                {
                                    identity.AddClaims(claims);
                                }
                            }
                        }
                        catch
                        {
                            // Ignore parsing errors
                        }
                    }
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();
}

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithLogging();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<McpDbContext>();
    
    try
    {
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply migrations. Database may not be available or migrations may not exist yet.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseWebAssemblyDebugging();
}

// Serve Blazor WASM static files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Use Bearer token authentication middleware for MCP endpoints
app.UseBearerTokenAuthentication();

// Map MCP endpoints with route parameter for tool filtering
// All endpoints end with /mcp to clearly identify them as Streamable HTTP endpoints
// Supported routes:
// - /image/mcp : Only image generation tools
// - /email/mcp : Only email tools  
// - /all/mcp   : All tools
app.MapMcp("/{toolCategory}/mcp");

// Also map root /mcp for backwards compatibility (uses all tools)
app.MapMcp("/mcp").RequireHost("*");

// Map Hangfire dashboard (only in development)
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
}

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}))
.WithName("HealthCheck")
.WithTags("Health")
.AllowAnonymous();

// Map API endpoints
app.MapApiEndpoints();

// Token management endpoints (for admin purposes - only available in development)
if (app.Environment.IsDevelopment())
{
    app.MapPost("/admin/tokens", async (
        string name,
        DateTime? expiresAt,
        ITokenValidationService tokenService,
        CancellationToken cancellationToken) =>
    {
        var token = await tokenService.CreateTokenAsync(name, expiresAt, cancellationToken);
        return Results.Ok(new { token, name, expiresAt, message = "Store this token securely. It will not be shown again." });
    })
    .WithName("CreateToken")
    .WithTags("Admin");
}

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("Verdure MCP Server started");

app.Run();
