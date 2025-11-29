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

// Add background job
builder.Services.AddScoped<ImageGenerationBackgroundJob>();

// Add MCP Server with HTTP transport and route-based tool filtering
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Configure per-session options to filter tools based on route
        options.ConfigureSessionOptions = async (httpContext, mcpOptions, cancellationToken) =>
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            // Get the tool category from route parameter
            var toolCategory = httpContext.Request.RouteValues["toolCategory"]?.ToString()?.ToLower() ?? "all";
            
            logger.LogInformation("MCP session starting for tool category: {Category}", toolCategory);
            
            // Get all registered tools
            var allTools = mcpOptions.ToolCollection?.ToList() ?? new List<ModelContextProtocol.Server.McpServerTool>();
            
            if (allTools.Count == 0)
            {
                logger.LogWarning("No tools registered in the MCP server");
                return;
            }
            
            // Filter tools based on category
            var filteredTools = new List<ModelContextProtocol.Server.McpServerTool>();
            
            switch (toolCategory)
            {
                case "image":
                    // Only image generation tools
                    filteredTools = allTools.Where(t => 
                        t.ProtocolTool.Name.Contains("image", StringComparison.OrdinalIgnoreCase)).ToList();
                    logger.LogInformation("Filtered to {Count} image tools", filteredTools.Count);
                    break;
                    
                case "email":
                    // Only email tools
                    filteredTools = allTools.Where(t => 
                        t.ProtocolTool.Name.Contains("email", StringComparison.OrdinalIgnoreCase)).ToList();
                    logger.LogInformation("Filtered to {Count} email tools", filteredTools.Count);
                    break;
                    
                case "all":
                default:
                    // All tools
                    filteredTools = allTools;
                    logger.LogInformation("Using all {Count} tools", filteredTools.Count);
                    break;
            }
            
            // Clear and re-add filtered tools
            mcpOptions.ToolCollection?.Clear();
            if (mcpOptions.ToolCollection != null)
            {
                foreach (var tool in filteredTools)
                {
                    mcpOptions.ToolCollection.Add(tool);
                }
            }
            
            await Task.CompletedTask;
        };
    })
    .WithTools<GenerateImageTool>()
    .WithTools<EmailTool>();

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
// Supported routes:
// - /image     : Only image generation tools
// - /email     : Only email tools
// - /all or /  : All tools (default)
app.MapMcp("/{toolCategory?}");

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
