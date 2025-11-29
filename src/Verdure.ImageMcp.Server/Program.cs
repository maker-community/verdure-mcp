using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Verdure.ImageMcp.Infrastructure.Data;
using Verdure.ImageMcp.Infrastructure.Services;
using Verdure.ImageMcp.Server.Filters;
using Verdure.ImageMcp.Server.Settings;
using Verdure.ImageMcp.Server.Tools;

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
builder.Services.AddDbContext<ImageMcpDbContext>(options =>
    options.UseNpgsql(connectionString, 
        b => b.MigrationsAssembly("Verdure.ImageMcp.Server")));

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

// Add MCP Server with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<GenerateImageTool>();

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
    var dbContext = scope.ServiceProvider.GetRequiredService<ImageMcpDbContext>();
    
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

// Map MCP endpoints
app.MapMcp();

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

// Token management endpoints (for admin purposes)
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
.WithTags("Admin")
.RequireAuthorization();

app.Logger.LogInformation("Verdure Image MCP Server started");

app.Run();
