using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Domain.Entities;
using System.Text.Json;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Simplified implementation of agent orchestration
/// This version uses predefined responses and can be enhanced with Azure OpenAI later
/// </summary>
public class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly McpDbContext _dbContext;
    private readonly ILogger<AgentOrchestrationService> _logger;
    private static int _messageCounter = 0;

    public AgentOrchestrationService(
        McpDbContext dbContext,
        ILogger<AgentOrchestrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessMessageAsync(
        Guid chatRoomId,
        string userId,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing message for chat room {ChatRoomId} from user {UserId}", chatRoomId, userId);

            // Get chat room and agent profiles
            var chatRoom = await _dbContext.ChatRooms
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId, cancellationToken);

            if (chatRoom == null)
            {
                throw new InvalidOperationException($"Chat room {chatRoomId} not found");
            }

            // Get agent profiles for this room
            var agents = await _dbContext.AgentProfiles
                .AsNoTracking()
                .Where(a => chatRoom.AgentIds.Contains(a.AgentId))
                .ToListAsync(cancellationToken);

            if (agents.Count == 0)
            {
                throw new InvalidOperationException($"No agents found for chat room {chatRoomId}");
            }

            // Select the most appropriate agent based on message content and capabilities
            var selectedAgent = SelectAgent(message, agents);

            _logger.LogInformation("Selected agent {AgentName} ({AgentId}) to respond", 
                selectedAgent.Name, selectedAgent.AgentId);

            // Generate response based on agent personality
            var response = GenerateResponse(message, selectedAgent);

            _logger.LogInformation("Agent {AgentName} responded with message of length {Length}",
                selectedAgent.Name, response.Length);

            // Parse response for potential tool calls
            var toolCalls = ParseToolCallsFromResponse(response);

            return new AgentResponse
            {
                AgentId = selectedAgent.AgentId,
                AgentName = selectedAgent.Name,
                Content = response,
                Metadata = new Dictionary<string, object>
                {
                    ["avatar"] = selectedAgent.Avatar ?? string.Empty,
                    ["personality"] = selectedAgent.Personality
                },
                ToolCalls = toolCalls
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in chat room {ChatRoomId}", chatRoomId);
            throw;
        }
    }

    public async Task InitializeAgentsAsync(Guid chatRoomId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing agents for chat room {ChatRoomId}", chatRoomId);
        
        // Placeholder for future Agent Framework integration
        await Task.CompletedTask;
    }

    public async Task<List<ChatMessageDto>> GetChatHistoryAsync(
        Guid chatRoomId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatRoomId == chatRoomId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                IsAgent = m.IsAgent,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return messages.OrderBy(m => m.CreatedAt).ToList();
    }

    private AgentProfile SelectAgent(string message, List<AgentProfile> agents)
    {
        var lowerMessage = message.ToLower();
        
        // Check for specific capabilities mentioned in the message
        foreach (var agent in agents)
        {
            foreach (var capability in agent.Capabilities)
            {
                if (lowerMessage.Contains(capability.ToLower()) ||
                    (capability == "生图" && (lowerMessage.Contains("图") || lowerMessage.Contains("画") || lowerMessage.Contains("image"))) ||
                    (capability == "音乐" && (lowerMessage.Contains("音乐") || lowerMessage.Contains("歌") || lowerMessage.Contains("music"))))
                {
                    return agent;
                }
            }
        }

        // Round-robin selection to give all agents a chance to respond
        var index = Interlocked.Increment(ref _messageCounter) % agents.Count;
        return agents[index];
    }

    private string GenerateResponse(string message, AgentProfile agent)
    {
        // Simplified response generation based on agent personality
        // In a full implementation, this would use Azure OpenAI with the agent's system prompt
        
        var responses = agent.AgentId switch
        {
            "agent-xiaotiantian" => new[]
            {
                $"哎呀，听到你说'{message}'，我好开心呀～😊",
                $"嗯嗯，我明白你的意思了呢！让我想想怎么帮你～",
                $"哇，这个问题好有趣呀！我觉得{message}是个不错的话题呢～",
                $"你说得对呀！关于'{message}'，我也有同感呢～💕"
            },
            "agent-yujieya" => new[]
            {
                $"关于'{message}'这个问题，我认为需要从多个角度来看待。",
                $"很高兴和你探讨'{message}'这个话题，这确实值得深思。",
                $"嗯，'{message}'是个很好的切入点。让我分享一下我的看法...",
                $"你提到的'{message}'让我想到了一些相关的思考..."
            },
            "agent-cainvlin" => new[]
            {
                $"关于'{message}'，从知识的角度来说，这涉及到几个重要方面。",
                $"很好的问题！'{message}'这个话题可以这样理解：",
                $"让我来解释一下'{message}'的相关知识点。",
                $"'{message}'是个很值得研究的主题，让我为你详细说明。"
            },
            "agent-yishujia mei" => new[]
            {
                $"'{message}'让我想到了一幅美丽的画面～",
                $"如果要表现'{message}'，我觉得可以用充满创意的方式！",
                $"哇，'{message}'这个想法很有艺术感呢！",
                $"从艺术的角度看，'{message}'充满了可能性～"
            },
            "agent-yinyuejiali" => new[]
            {
                $"'{message}'就像一首动人的旋律，让人心情愉悦～🎵",
                $"听到'{message}'，我仿佛听到了美妙的音乐声...",
                $"'{message}'这个主题很适合配上一首温暖的歌曲呢～",
                $"让我为你推荐一些音乐来配合'{message}'的心情吧！"
            },
            "agent-huopo" => new[]
            {
                $"哈哈，'{message}'太有意思了！😄",
                $"耶！说到'{message}'，我超级兴奋的！",
                $"'{message}'？这个话题我喜欢！让我们聊得更嗨一点！",
                $"哇哦～'{message}'真是个好话题！我有好多想法要分享！"
            },
            _ => new[]
            {
                $"感谢你分享'{message}'，这确实是个有趣的话题。",
                $"我理解你说的'{message}'，让我们继续聊聊吧！",
                $"'{message}'是个不错的观点，我很乐意和你讨论。"
            }
        };

        // Select a random response variant
        var selectedResponse = responses[Random.Shared.Next(responses.Length)];
        
        return selectedResponse;
    }

    private List<ToolCall>? ParseToolCallsFromResponse(string response)
    {
        // Simple pattern matching for tool calls
        var toolCalls = new List<ToolCall>();

        if (response.Contains("[生图:") || response.Contains("[generate_image:"))
        {
            // Extract image generation prompt
            var match = System.Text.RegularExpressions.Regex.Match(
                response, 
                @"\[(生图|generate_image):(.+?)\]");
            
            if (match.Success)
            {
                toolCalls.Add(new ToolCall
                {
                    ToolName = "generate_image",
                    Parameters = new Dictionary<string, object>
                    {
                        ["prompt"] = match.Groups[2].Value.Trim()
                    }
                });
            }
        }

        if (response.Contains("[音乐]") || response.Contains("[music]"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "play_random_music",
                Parameters = new Dictionary<string, object>()
            });
        }

        return toolCalls.Count > 0 ? toolCalls : null;
    }
}
