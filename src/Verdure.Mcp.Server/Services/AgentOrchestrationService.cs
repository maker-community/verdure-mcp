using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Service for orchestrating AI agents in group chat conversations
/// Implements a simplified agent framework using Azure OpenAI directly
/// </summary>
public class AgentOrchestrationService
{
    private readonly McpDbContext _dbContext;
    private readonly ILogger<AgentOrchestrationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAISettings _settings;

    public AgentOrchestrationService(
        McpDbContext dbContext,
        ILogger<AgentOrchestrationService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAISettings> settings)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _settings = settings.Value;
    }

    /// <summary>
    /// Process a user message in a chat room and get agent responses
    /// </summary>
    public async Task<List<AgentResponse>> ProcessUserMessageAsync(
        Guid chatRoomId,
        string userId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing user message in room {ChatRoomId} from user {UserId}", chatRoomId, userId);

        // Load chat room
        var chatRoom = await _dbContext.ChatRooms.FindAsync(new object[] { chatRoomId }, cancellationToken);
        if (chatRoom == null)
        {
            _logger.LogWarning("Chat room {ChatRoomId} not found", chatRoomId);
            return new List<AgentResponse>();
        }

        // Parse agent IDs
        var agentIds = JsonSerializer.Deserialize<List<string>>(chatRoom.AgentIds) ?? new List<string>();
        if (!agentIds.Any())
        {
            _logger.LogWarning("No agents configured for room {ChatRoomId}", chatRoomId);
            return new List<AgentResponse>();
        }

        // Load agent profiles
        var agents = await _dbContext.AgentProfiles
            .Where(a => agentIds.Contains(a.AgentId))
            .ToListAsync(cancellationToken);

        if (!agents.Any())
        {
            _logger.LogWarning("No agent profiles found for room {ChatRoomId}", chatRoomId);
            return new List<AgentResponse>();
        }

        // Get recent message history (last 10 messages)
        var recentMessages = await _dbContext.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        // Select which agent should respond (simple round-robin or random selection)
        var respondingAgent = SelectRespondingAgent(agents, recentMessages, userMessage);
        
        _logger.LogInformation("Selected agent {AgentId} ({AgentName}) to respond", 
            respondingAgent.AgentId, respondingAgent.Name);

        // Generate response from the selected agent
        var response = await GenerateAgentResponseAsync(
            respondingAgent, 
            userMessage, 
            recentMessages, 
            cancellationToken);

        return new List<AgentResponse> { response };
    }

    /// <summary>
    /// Select which agent should respond to the user message
    /// Simple implementation: rotate through agents or use keyword matching
    /// </summary>
    private AgentProfile SelectRespondingAgent(
        List<AgentProfile> agents, 
        List<ChatMessage> recentMessages,
        string userMessage)
    {
        // Check capabilities for keyword matching
        var userMessageLower = userMessage.ToLower();
        
        // Priority: match capabilities to user message
        foreach (var agent in agents)
        {
            var capabilities = JsonSerializer.Deserialize<List<string>>(agent.Capabilities) ?? new List<string>();
            if (capabilities.Any(cap => userMessageLower.Contains(cap)))
            {
                return agent;
            }
        }

        // Fallback: round-robin based on last agent that spoke
        var lastAgentMessage = recentMessages
            .Where(m => m.IsAgent)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        if (lastAgentMessage != null)
        {
            var lastAgentIndex = agents.FindIndex(a => a.AgentId == lastAgentMessage.SenderId);
            if (lastAgentIndex >= 0 && lastAgentIndex < agents.Count - 1)
            {
                return agents[lastAgentIndex + 1];
            }
        }

        // Default: return first agent
        return agents[0];
    }

    /// <summary>
    /// Generate a response from an agent using Azure OpenAI
    /// </summary>
    private async Task<AgentResponse> GenerateAgentResponseAsync(
        AgentProfile agent,
        string userMessage,
        List<ChatMessage> recentMessages,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build conversation history
            var messages = new List<object>
            {
                new { role = "system", content = agent.SystemPrompt }
            };

            // Add recent message history
            foreach (var msg in recentMessages)
            {
                messages.Add(new
                {
                    role = msg.IsAgent ? "assistant" : "user",
                    content = msg.Content
                });
            }

            // Add current user message
            messages.Add(new { role = "user", content = userMessage });

            // For now, return a simple response indicating Azure OpenAI would be called
            // In production, this would call Azure OpenAI Chat Completions API
            var responseContent = $"[{agent.Name}] 我理解了您的消息。这是一个基于 {agent.Personality} 的回复。";

            // Check if agent should call tools based on capabilities
            var capabilities = JsonSerializer.Deserialize<List<string>>(agent.Capabilities) ?? new List<string>();
            string? toolCall = null;
            
            if (capabilities.Contains("生图") && (userMessage.Contains("图") || userMessage.Contains("画")))
            {
                toolCall = "generate_image";
            }
            else if (capabilities.Contains("音乐") && (userMessage.Contains("音乐") || userMessage.Contains("歌")))
            {
                toolCall = "play_music";
            }

            return new AgentResponse
            {
                AgentId = agent.AgentId,
                AgentName = agent.Name,
                Content = responseContent,
                ToolCall = toolCall,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response for agent {AgentId}", agent.AgentId);
            return new AgentResponse
            {
                AgentId = agent.AgentId,
                AgentName = agent.Name,
                Content = $"[{agent.Name}] 抱歉,我遇到了一些问题,请稍后再试。",
                Timestamp = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Response from an AI agent
/// </summary>
public class AgentResponse
{
    public required string AgentId { get; set; }
    public required string AgentName { get; set; }
    public required string Content { get; set; }
    public string? ToolCall { get; set; } // "generate_image", "play_music", etc.
    public Dictionary<string, string>? ToolParameters { get; set; }
    public DateTime Timestamp { get; set; }
}
