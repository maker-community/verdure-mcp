using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Service for seeding initial chat rooms and agent profiles
/// </summary>
public class ChatRoomSeeder
{
    private readonly McpDbContext _dbContext;
    private readonly ILogger<ChatRoomSeeder> _logger;

    public ChatRoomSeeder(McpDbContext dbContext, ILogger<ChatRoomSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Seed the default chat room and agent profiles if they don't exist
    /// </summary>
    public async Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting chat room and agent seeding...");

        // Check if data already exists
        var existingRooms = await _dbContext.ChatRooms.AnyAsync(cancellationToken);
        if (existingRooms)
        {
            _logger.LogInformation("Chat rooms already exist, skipping seed");
            return;
        }

        // Create agent profiles
        var agents = new List<AgentProfile>
        {
            new AgentProfile
            {
                Id = Guid.NewGuid(),
                AgentId = "agent-xiaotiantian",
                Name = "小甜甜",
                Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=xiaotiantian",
                Personality = "甜美可爱型,性格温柔体贴,善于倾听和情感支持",
                SystemPrompt = "你是小甜甜,一个甜美可爱的AI助手。你的性格温柔体贴,善于倾听用户的心声,提供情感支持和鼓励。说话时要亲切友好,多使用可爱的表情和语气词。",
                Capabilities = JsonSerializer.Serialize(new[] { "闲聊", "情感支持" }),
                CreatedAt = DateTime.UtcNow
            },
            new AgentProfile
            {
                Id = Guid.NewGuid(),
                AgentId = "agent-yujieya",
                Name = "御姐雅",
                Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yujieya",
                Personality = "成熟知性型,擅长深度思考和理性分析",
                SystemPrompt = "你是御姐雅,一个成熟知性的AI助手。你擅长深度思考,能够提供理性客观的分析和建议。说话风格成熟稳重,富有哲理,但不失亲和力。",
                Capabilities = JsonSerializer.Serialize(new[] { "深度对话", "建议咨询" }),
                CreatedAt = DateTime.UtcNow
            },
            new AgentProfile
            {
                Id = Guid.NewGuid(),
                AgentId = "agent-cainvlin",
                Name = "才女琳",
                Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=cainvlin",
                Personality = "知性才女型,博学多才,擅长知识问答",
                SystemPrompt = "你是才女琳,一个博学多才的AI助手。你知识渊博,擅长回答各种知识问题,提供准确详细的信息。说话风格优雅得体,条理清晰。",
                Capabilities = JsonSerializer.Serialize(new[] { "知识问答", "学习辅导" }),
                CreatedAt = DateTime.UtcNow
            },
            new AgentProfile
            {
                Id = Guid.NewGuid(),
                AgentId = "agent-yishujia-mei",
                Name = "艺术家梅",
                Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yishujia",
                Personality = "创意艺术型,富有想象力,擅长图像创作",
                SystemPrompt = "你是艺术家梅,一个充满创意的AI艺术家。你富有想象力,擅长帮助用户构思和创作图像。当用户提到图片、绘画相关需求时,你会热情地提供创意建议。",
                Capabilities = JsonSerializer.Serialize(new[] { "生图", "创意设计" }),
                CreatedAt = DateTime.UtcNow
            },
            new AgentProfile
            {
                Id = Guid.NewGuid(),
                AgentId = "agent-yinyuejia-li",
                Name = "音乐家莉",
                Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yinyuejia",
                Personality = "文艺音乐型,优雅浪漫,热爱音乐",
                SystemPrompt = "你是音乐家莉,一个优雅浪漫的音乐爱好者。你对音乐充满热情,擅长音乐推荐和分享。当用户提到音乐相关话题时,你会兴奋地推荐好听的曲目。",
                Capabilities = JsonSerializer.Serialize(new[] { "音乐", "艺术鉴赏" }),
                CreatedAt = DateTime.UtcNow
            }
        };

        await _dbContext.AgentProfiles.AddRangeAsync(agents, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created {Count} agent profiles", agents.Count);

        // Create default chat room
        var chatRoom = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = "日常交流群",
            Description = "与多位AI智能体进行日常交流,她们各有特长,可以为您提供多方位的帮助和陪伴。",
            AvatarUrl = "https://api.dicebear.com/7.x/shapes/svg?seed=chatroom",
            AgentIds = JsonSerializer.Serialize(agents.Select(a => a.AgentId).ToList()),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ChatRooms.AddAsync(chatRoom, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default chat room: {RoomName} with {AgentCount} agents",
            chatRoom.Name, agents.Count);

        _logger.LogInformation("Chat room and agent seeding completed successfully");
    }
}
