using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Net.Http.Headers;
using Verdure.Mcp.Domain.Entities;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// User context for MCP tool calls - stored in AsyncLocal for cross-service access
/// </summary>
public class UserContext
{
    private static readonly AsyncLocal<UserContext?> _current = new();

    public static UserContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
}

/// <summary>
/// Service for managing MCP (Model Context Protocol) server connections and tools.
/// ✅ Lazy-loading approach: Connects to MCP servers on-demand when tools are requested.
/// ✅ Suitable for low-frequency scenarios (like AI group chat).
/// Based on: https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/ModelContextProtocol
/// </summary>
/// <remarks>
/// This service uses a lazy-loading pattern where MCP clients are created only when needed.
/// For high-frequency scenarios, consider caching connections or using Singleton lifecycle.
/// </remarks>
public class McpToolService : IAsyncDisposable
{
    private readonly ILogger<McpToolService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public McpToolService(
        ILogger<McpToolService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get tools for specific capabilities from configured MCP servers.
    /// Creates connections on-demand (lazy loading).
    /// </summary>
    /// <param name="capabilities">Agent capabilities (currently unused, returns all tools)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of AIFunction tools from all enabled MCP servers</returns>
    public async Task<IEnumerable<AIFunction>> GetToolsForCapabilitiesAsync(
        List<string> capabilities,
        CancellationToken cancellationToken = default)
    {
        var config = _configuration.GetSection("McpServers").Get<McpServersConfig>();
        if (config == null || config.Servers.Count == 0)
        {
            _logger.LogDebug("No MCP servers configured");
            return Enumerable.Empty<AIFunction>();
        }

        var allTools = new List<AIFunction>();
        var enabledServers = config.Servers.Where(s => s.Enabled).ToList();

        _logger.LogInformation("Loading tools from {ServerCount} MCP servers", enabledServers.Count);

        // Connect to each enabled server and retrieve tools
        foreach (var serverConfig in enabledServers)
        {
            try
            {
                _logger.LogDebug("Connecting to MCP server: {ServerName} ({Endpoint})", 
                    serverConfig.Name, serverConfig.Endpoint);

                // Create MCP client (will be disposed after getting tools)
                await using var mcpClient = await CreateMcpClientAsync(serverConfig, cancellationToken);
                
                if (mcpClient != null)
                {
                    // ✅ Official pattern: ListToolsAsync returns McpClientTool which implements AITool
                    var tools = await mcpClient.ListToolsAsync();
                    var aiFunctions = tools.Cast<AIFunction>().ToList();
                    
                    allTools.AddRange(aiFunctions);
                    
                    _logger.LogInformation(
                        "Loaded {ToolCount} tools from MCP server '{ServerName}': {ToolNames}",
                        aiFunctions.Count, 
                        serverConfig.Name,
                        string.Join(", ", aiFunctions.Select(t => t.Name)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to load tools from MCP server '{ServerName}'. Continuing with other servers.",
                    serverConfig.Name);
                // Continue with other servers instead of failing completely
            }
        }

        _logger.LogInformation("Total tools loaded: {TotalCount} from {ServerCount} servers", 
            allTools.Count, enabledServers.Count);

        // TODO: Implement capability-based filtering when needed
        // For now, return all tools regardless of capabilities
        return allTools;
    }

    /// <summary>
    /// Get information about all configured MCP servers (not connected)
    /// </summary>
    public IEnumerable<McpServerInfo> GetConfiguredServers()
    {
        var config = _configuration.GetSection("McpServers").Get<McpServersConfig>();
        if (config == null || config.Servers.Count == 0)
        {
            return Enumerable.Empty<McpServerInfo>();
        }

        return config.Servers.Where(s => s.Enabled).Select(c => new McpServerInfo
        {
            Id = c.Id,
            Name = c.Name,
            Endpoint = c.Endpoint,
            Description = c.Description,
            ToolCount = 0, // Unknown until connected
            IsConnected = false
        });
    }

    /// <summary>
    /// Create an MCP client for a specific server configuration.
    /// The client should be disposed by the caller using 'await using'.
    /// </summary>
    private async Task<McpClient?> CreateMcpClientAsync(
        McpServerConfig config, 
        CancellationToken cancellationToken)
    {
        // Use lock to prevent concurrent connection attempts
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Create logger factory for MCP client
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise
            });

            // Create transport based on authentication type
            IClientTransport transport = config.AuthType.ToLowerInvariant() switch
            {
                "bearer" => CreateBearerTokenTransport(config),
                "oauth" => CreateOAuthTransport(config),
                _ => CreateNoAuthTransport(config)
            };

            // ✅ Create MCP client - caller is responsible for disposal
            var mcpClient = await McpClient.CreateAsync(
                transport, 
                cancellationToken: cancellationToken, 
                loggerFactory: loggerFactory);

            _logger.LogDebug("Created MCP client for server: {ServerName}", config.Name);

            return mcpClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MCP client for server: {ServerName}", config.Name);
            return null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Create HTTP transport with Bearer token authentication
    /// </summary>
    private IClientTransport CreateBearerTokenTransport(McpServerConfig config)
    {
        if (string.IsNullOrEmpty(config.BearerToken))
        {
            throw new InvalidOperationException($"Bearer token is required for server: {config.Name}");
        }

        // Get HttpClient from factory for proper lifecycle management
        var httpClient = _httpClientFactory.CreateClient("McpClient");
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", config.BearerToken);

        // ✅ Inject user context into MCP tool calls
        InjectUserContextToHttpClient(httpClient);

        // Convert config transport mode to SDK transport mode
        var transportMode = ConvertToSdkTransportMode(config.TransportMode);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name
        };

        // Only set TransportMode if not AutoDetect (let SDK auto-detect by default)
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = transportMode;
        }

        var transport = new HttpClientTransport(transportOptions, httpClient);

        _logger.LogDebug("Created Bearer token transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Create HTTP transport with OAuth authentication
    /// </summary>
    private IClientTransport CreateOAuthTransport(McpServerConfig config)
    {
        if (config.OAuth == null)
        {
            throw new InvalidOperationException($"OAuth configuration is required for server: {config.Name}");
        }

        // Get HttpClient from factory
        var httpClient = _httpClientFactory.CreateClient("McpClient");

        // ✅ Inject user context into MCP tool calls
        InjectUserContextToHttpClient(httpClient);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name,
            OAuth = new()
            {
                ClientId = config.OAuth.ClientId,
                ClientSecret = config.OAuth.ClientSecret,
                RedirectUri = new Uri(config.OAuth.RedirectUri),
                // Note: For production OAuth flow, you would need to implement
                // AuthorizationRedirectDelegate similar to the Agent_MCP_Server_Auth sample
            }
        };

        // Only set TransportMode if not AutoDetect
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = ConvertToSdkTransportMode(config.TransportMode);
        }

        var transport = new HttpClientTransport(transportOptions, httpClient);

        _logger.LogDebug("Created OAuth transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Create HTTP transport without authentication
    /// </summary>
    private IClientTransport CreateNoAuthTransport(McpServerConfig config)
    {
        // Get HttpClient from factory
        var httpClient = _httpClientFactory.CreateClient("McpClient");

        // ✅ Inject user context into MCP tool calls
        InjectUserContextToHttpClient(httpClient);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name
        };

        // Only set TransportMode if not AutoDetect
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = ConvertToSdkTransportMode(config.TransportMode);
        }

        var transport = new HttpClientTransport(transportOptions, httpClient);

        _logger.LogDebug("Created no-auth transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Convert configuration TransportMode to SDK HttpTransportMode
    /// </summary>
    private static HttpTransportMode ConvertToSdkTransportMode(McpTransportMode mode)
    {
        return mode switch
        {
            McpTransportMode.Sse => HttpTransportMode.Sse,
            McpTransportMode.StreamableHttp => HttpTransportMode.StreamableHttp,
            _ => HttpTransportMode.Sse // Default to SSE
        };
    }

    /// <summary>
    /// Inject user context (userId, email) into HttpClient request headers for MCP tool calls
    /// </summary>
    private void InjectUserContextToHttpClient(HttpClient httpClient)
    {
        // Get user context from AsyncLocal storage (set by AgentOrchestrationService)
        var userId = UserContext.Current?.UserId;
        var userEmail = UserContext.Current?.UserEmail;

        if (!string.IsNullOrEmpty(userId))
        {
            httpClient.DefaultRequestHeaders.Remove("X-User-Id");
            httpClient.DefaultRequestHeaders.Add("X-User-Id", userId);
            _logger.LogDebug("Injected X-User-Id: {UserId} into MCP HttpClient", userId);
        }

        if (!string.IsNullOrEmpty(userEmail))
        {
            httpClient.DefaultRequestHeaders.Remove("X-User-Email");
            httpClient.DefaultRequestHeaders.Add("X-User-Email", userEmail);
            _logger.LogDebug("Injected X-User-Email: {UserEmail} into MCP HttpClient", userEmail);
        }
    }

    /// <summary>
    /// Dispose resources (SemaphoreSlim)
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _connectionLock?.Dispose();
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Information about an MCP server
/// </summary>
public class McpServerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ToolCount { get; set; }
    public bool IsConnected { get; set; }
}
