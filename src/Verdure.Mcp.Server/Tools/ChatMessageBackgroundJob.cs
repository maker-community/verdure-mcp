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
    private readonly IAudioSynthesisService _audioSynthesisService;
    private readonly IAudioStorageService _audioStorageService;
    private readonly ILogger<ChatMessageBackgroundJob> _logger;

    public ChatMessageBackgroundJob(
        McpDbContext dbContext,
        IAgentOrchestrationService agentOrchestrationService,
        IDevicePushService devicePushService,
        IAudioSynthesisService audioSynthesisService,
        IAudioStorageService audioStorageService,
        ILogger<ChatMessageBackgroundJob> logger)
    {
        _dbContext = dbContext;
        _agentOrchestrationService = agentOrchestrationService;
        _devicePushService = devicePushService;
        _audioSynthesisService = audioSynthesisService;
        _audioStorageService = audioStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Process a chat message asynchronously
    /// </summary>
    public async Task ProcessChatMessageAsync(
        Guid chatRoomId,
        Guid messageId,
        string userId,
        string? userEmail,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Processing chat message: roomId={ChatRoomId}, messageId={MessageId}, userId={UserId}, userEmail={UserEmail}",
                chatRoomId, messageId, userId, userEmail ?? "未提供");

            // Get the user message
            var userMessage = await _dbContext.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId, cancellationToken);

            if (userMessage == null)
            {
                _logger.LogWarning("Message {MessageId} not found", messageId);
                return;
            }

            // ✅ Set user context for MCP tool calls (from request headers)
            UserContext.Current = new UserContext
            {
                UserId = userId,
                UserEmail = userEmail
            };
            _logger.LogDebug("UserContext set in ChatMessageBackgroundJob: UserId={UserId}, UserEmail={UserEmail}",
                userId, userEmail);

            // Process message with agent orchestration
            var agentResponse = await _agentOrchestrationService.ProcessMessageAsync(
                chatRoomId,
                userId,
                userMessage.Content,
                cancellationToken);

            // Handle tool calls if any (prepare attachments before saving message)
            var attachments = new List<object>();
            if (agentResponse.ToolCalls != null && agentResponse.ToolCalls.Count > 0)
            {
                foreach (var toolCall in agentResponse.ToolCalls)
                {
                    _logger.LogInformation("Processing tool call: {ToolName}", toolCall.ToolName);

                    // Extract results from tool parameters (after execution)
                    // Note: FunctionInvokingChatClient already executed the tools
                    // We just need to extract URLs for frontend display
                    switch (toolCall.ToolName)
                    {
                        case "GenerateImage":
                            // Fallback: Try to parse from agent's message content
                            if (!string.IsNullOrEmpty(agentResponse.Content))
                            {
                                var urlMatch = System.Text.RegularExpressions.Regex.Match(
                                    agentResponse.Content,
                                    @"(https?://[^\s]+?\.(?:png|jpg|jpeg|gif|webp))");
                                if (urlMatch.Success)
                                {
                                    var imageUrl = urlMatch.Groups[1].Value;
                                    _logger.LogInformation("Image URL extracted from content: {ImageUrl}", imageUrl);
                                    attachments.Add(new { type = "image", url = imageUrl });
                                }
                            }
                            break;
                    }
                }
            }

            string? audioUrl = null;
            if (!string.IsNullOrWhiteSpace(agentResponse.Content))
            {
                var synthesisResult = await _audioSynthesisService.SynthesizeOggAsync(
                    agentResponse.Content,
                    cancellationToken);

                if (synthesisResult.Success && synthesisResult.AudioBytes is { Length: > 0 })
                {
                    var audioId = Guid.NewGuid();
                    var storageResult = await _audioStorageService.SaveOggAsync(
                        synthesisResult.AudioBytes,
                        audioId,
                        cancellationToken);

                    audioUrl = storageResult.OggUrl;
                    attachments.Add(new { type = "audio", url = audioUrl, format = "ogg" });
                }
                else if (!string.IsNullOrWhiteSpace(synthesisResult.ErrorMessage))
                {
                    _logger.LogWarning("Speech synthesis failed: {Error}", synthesisResult.ErrorMessage);
                }
            }

            // Save agent response to database
            var agentMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatRoomId = chatRoomId,
                UserId = userId,
                SenderId = agentResponse.AgentId,
                IsAgent = true,
                Content = agentResponse.Content,
                Metadata = agentResponse.Metadata != null 
                    ? JsonSerializer.Serialize(agentResponse.Metadata) 
                    : null,
                Attachments = attachments.Count > 0
                    ? JsonSerializer.Serialize(attachments)
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ChatMessages.Add(agentMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Agent response saved: agentId={AgentId}, messageId={MessageId}",
                agentResponse.AgentId, agentMessage.Id);

            // Get chat room info
            var chatRoom = await _dbContext.ChatRooms
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId, cancellationToken);

            // Push response to user via SignalR (xiaozhi 协议格式)
            // 1. 先发送通知消息
            var notificationMessage = new
            {
                action = "notification",
                title = "新消息",
                content = BuildNotificationContent(agentResponse.AgentName, agentResponse.Content),
                emotion = "happy",
                sound = "message"
            };
            await _devicePushService.SendCustomMessageAsync(userId, notificationMessage, cancellationToken);

            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                var audioMessage = new
                {
                    action = "audio",
                    url = audioUrl,
                    title = agentResponse.AgentName
                };

                await _devicePushService.SendCustomMessageAsync(userId, audioMessage, cancellationToken);
            }

            _logger.LogInformation(
                "Agent response pushed to user {UserId} via SignalR, roomId={ChatRoomId}",
                userId, chatRoomId);
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

    private static string BuildNotificationContent(string agentName, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"{agentName}: (空)";
        }

        var previewLength = Math.Min(30, content.Length);
        var preview = content.Substring(0, previewLength);
        return $"{agentName}: {preview}...";
    }
}
