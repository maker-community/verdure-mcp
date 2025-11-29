using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
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
}

// Use Bearer token authentication middleware
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

app.Logger.LogInformation("Verdure MCP Server started");

app.Run();
