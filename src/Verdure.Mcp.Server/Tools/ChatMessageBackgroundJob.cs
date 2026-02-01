using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// Background job for processing chat messages with AI agents
/// </summary>
public class ChatMessageBackgroundJob
{
    private readonly McpDbContext _dbContext;
    private readonly AgentOrchestrationService _agentService;
    private readonly IDevicePushService _devicePushService;
    private readonly ILogger<ChatMessageBackgroundJob> _logger;

    public ChatMessageBackgroundJob(
        McpDbContext dbContext,
        AgentOrchestrationService agentService,
        IDevicePushService devicePushService,
        ILogger<ChatMessageBackgroundJob> logger)
    {
        _dbContext = dbContext;
        _agentService = agentService;
        _devicePushService = devicePushService;
        _logger = logger;
    }

    /// <summary>
    /// Process a user message and generate agent responses
    /// </summary>
    public async Task ExecuteAsync(
        Guid chatRoomId,
        Guid userMessageId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing chat message {MessageId} in room {RoomId} for user {UserId}",
            userMessageId, chatRoomId, userId);

        try
        {
            // Get the user message
            var userMessage = await _dbContext.ChatMessages.FindAsync(
                new object[] { userMessageId }, cancellationToken);

            if (userMessage == null)
            {
                _logger.LogWarning("User message {MessageId} not found", userMessageId);
                return;
            }

            // Get the chat room
            var chatRoom = await _dbContext.ChatRooms.FindAsync(
                new object[] { chatRoomId }, cancellationToken);

            if (chatRoom == null)
            {
                _logger.LogWarning("Chat room {RoomId} not found", chatRoomId);
                return;
            }

            // Process the message and get agent responses
            var agentResponses = await _agentService.ProcessUserMessageAsync(
                chatRoomId, userId, userMessage.Content, cancellationToken);

            foreach (var response in agentResponses)
            {
                // Save agent message to database
                var agentMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatRoomId = chatRoomId,
                    SenderId = response.AgentId,
                    IsAgent = true,
                    Content = response.Content,
                    Metadata = response.ToolCall != null
                        ? JsonSerializer.Serialize(new { toolCall = response.ToolCall })
                        : null,
                    CreatedAt = response.Timestamp
                };

                _dbContext.ChatMessages.Add(agentMessage);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Saved agent response from {AgentName} (ID: {AgentId})",
                    response.AgentName, response.AgentId);

                // Push response to user via SignalR
                var pushMessage = new
                {
                    action = "group_chat",
                    roomId = chatRoomId.ToString(),
                    roomName = chatRoom.Name,
                    message = new
                    {
                        id = agentMessage.Id.ToString(),
                        senderId = response.AgentId,
                        senderName = response.AgentName,
                        content = response.Content,
                        isAgent = true,
                        timestamp = response.Timestamp,
                        attachments = new List<object>()
                    }
                };

                await _devicePushService.SendCustomMessageAsync(userId, pushMessage);

                _logger.LogInformation("Pushed agent response to user {UserId} via SignalR", userId);

                // Handle tool calls if any
                if (response.ToolCall != null)
                {
                    await HandleToolCallAsync(response, userId, chatRoomId, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message {MessageId}", userMessageId);
        }
    }

    /// <summary>
    /// Handle tool calls from agents (image generation, music playback, etc.)
    /// </summary>
    private async Task HandleToolCallAsync(
        AgentResponse response,
        string userId,
        Guid chatRoomId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling tool call: {ToolCall} from agent {AgentId}",
            response.ToolCall, response.AgentId);

        try
        {
            switch (response.ToolCall)
            {
                case "generate_image":
                    // Tool calling would be implemented here
                    // For now, just log
                    _logger.LogInformation("Agent {AgentId} requested image generation", response.AgentId);
                    break;

                case "play_music":
                    // Tool calling would be implemented here
                    _logger.LogInformation("Agent {AgentId} requested music playback", response.AgentId);
                    break;

                default:
                    _logger.LogWarning("Unknown tool call: {ToolCall}", response.ToolCall);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call {ToolCall}", response.ToolCall);
        }
    }
}
