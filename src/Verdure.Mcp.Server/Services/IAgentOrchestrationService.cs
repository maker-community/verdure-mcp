namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Response from the agent orchestration service
/// </summary>
public class AgentResponse
{
    public required string AgentId { get; set; }
    public required string AgentName { get; set; }
    public required string Content { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Represents a tool call request from an agent
/// </summary>
public class ToolCall
{
    public required string ToolName { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Service for orchestrating AI agents in group chats
/// </summary>
public interface IAgentOrchestrationService
{
    /// <summary>
    /// Process a user message in a chat room and get agent responses
    /// </summary>
    Task<AgentResponse> ProcessMessageAsync(
        Guid chatRoomId, 
        string userId, 
        string message, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize agents for a chat room based on agent profiles
    /// </summary>
    Task InitializeAgentsAsync(Guid chatRoomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat history for context
    /// </summary>
    Task<List<ChatMessageDto>> GetChatHistoryAsync(
        Guid chatRoomId, 
        int limit = 50, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for chat messages
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public required string SenderId { get; set; }
    public bool IsAgent { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}
