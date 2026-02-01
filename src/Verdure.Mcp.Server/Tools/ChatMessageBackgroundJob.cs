using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Server.Services;
using System.Text.Json;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// Background job for processing chat messages and generating agent responses
/// </summary>
public class ChatMessageBackgroundJob
{
    private readonly McpDbContext _dbContext;
    private readonly IAgentOrchestrationService _agentOrchestrationService;
    private readonly IDevicePushService _devicePushService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly ILogger<ChatMessageBackgroundJob> _logger;

    public ChatMessageBackgroundJob(
        McpDbContext dbContext,
        IAgentOrchestrationService agentOrchestrationService,
        IDevicePushService devicePushService,
        IImageGenerationService imageGenerationService,
        ILogger<ChatMessageBackgroundJob> logger)
    {
        _dbContext = dbContext;
        _agentOrchestrationService = agentOrchestrationService;
        _devicePushService = devicePushService;
        _imageGenerationService = imageGenerationService;
        _logger = logger;
    }

    /// <summary>
    /// Process a chat message asynchronously
    /// </summary>
    public async Task ProcessChatMessageAsync(
        Guid chatRoomId,
        Guid messageId,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Processing chat message: roomId={ChatRoomId}, messageId={MessageId}, userId={UserId}",
                chatRoomId, messageId, userId);

            // Get the user message
            var userMessage = await _dbContext.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (userMessage == null)
            {
                _logger.LogWarning("Message {MessageId} not found", messageId);
                return;
            }

            // Process message with agent orchestration
            var agentResponse = await _agentOrchestrationService.ProcessMessageAsync(
                chatRoomId,
                userId,
                userMessage.Content,
                cancellationToken);

            // Save agent response to database
            var agentMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatRoomId = chatRoomId,
                SenderId = agentResponse.AgentId,
                IsAgent = true,
                Content = agentResponse.Content,
                Metadata = agentResponse.Metadata != null 
                    ? JsonSerializer.Serialize(agentResponse.Metadata) 
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ChatMessages.Add(agentMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Agent response saved: agentId={AgentId}, messageId={MessageId}",
                agentResponse.AgentId, agentMessage.Id);

            // Handle tool calls if any
            var attachments = new List<object>();
            if (agentResponse.ToolCalls != null && agentResponse.ToolCalls.Count > 0)
            {
                foreach (var toolCall in agentResponse.ToolCalls)
                {
                    _logger.LogInformation("Processing tool call: {ToolName}", toolCall.ToolName);
                    
                    switch (toolCall.ToolName)
                    {
                        case "generate_image":
                            if (toolCall.Parameters?.TryGetValue("prompt", out var promptObj) == true)
                            {
                                var prompt = promptObj?.ToString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(prompt))
                                {
                                    // Generate image and add to attachments
                                    var imageUrl = await GenerateImageAsync(prompt, userId, cancellationToken);
                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        attachments.Add(new { type = "image", url = imageUrl });
                                    }
                                }
                            }
                            break;

                        case "play_random_music":
                            // Music will be handled by the MusicTool directly
                            // For now, just note it in the response
                            attachments.Add(new { type = "audio", status = "queued" });
                            break;
                    }
                }
            }

            // Get chat room info
            var chatRoom = await _dbContext.ChatRooms
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId, cancellationToken);

            // Push response to user via SignalR
            var pushMessage = new
            {
                action = "group_chat",
                roomId = chatRoomId,
                roomName = chatRoom?.Name ?? "AI Group Chat",
                message = new
                {
                    id = agentMessage.Id,
                    senderId = agentResponse.AgentId,
                    senderName = agentResponse.AgentName,
                    content = agentResponse.Content,
                    isAgent = true,
                    timestamp = agentMessage.CreatedAt,
                    attachments = attachments.Count > 0 ? attachments : null,
                    metadata = agentResponse.Metadata
                }
            };

            await _devicePushService.SendCustomMessageAsync(userId, pushMessage, cancellationToken);

            _logger.LogInformation(
                "Agent response pushed to user {UserId} via SignalR",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error processing chat message: roomId={ChatRoomId}, messageId={MessageId}",
                chatRoomId, messageId);
            
            // Optionally send error notification to user
            try
            {
                await _devicePushService.SendNotificationAsync(
                    userId,
                    "抱歉，处理您的消息时出现了问题。请稍后重试。",
                    cancellationToken);
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Failed to send error notification to user {UserId}", userId);
            }
        }
    }

    private async Task<string?> GenerateImageAsync(
        string prompt,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating image for prompt: {Prompt}", prompt);

            // Use the existing image generation service
            var result = await _imageGenerationService.GenerateImageAsync(
                prompt,
                size: "1024x1024",
                quality: "standard",
                style: "vivid",
                cancellationToken: cancellationToken);

            if (result.Success && !string.IsNullOrEmpty(result.ImageUrl))
            {
                _logger.LogInformation("Image generated successfully: {ImageUrl}", result.ImageUrl);
                return result.ImageUrl;
            }
            else
            {
                _logger.LogWarning("Image generation failed: {Error}", result.ErrorMessage);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image for prompt: {Prompt}", prompt);
            return null;
        }
    }
}
