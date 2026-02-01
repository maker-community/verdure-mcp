using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Domain.Entities;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Service for seeding initial chat room and agent data
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
    /// Seed initial data for AI group chat
    /// </summary>
    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting AI group chat data seeding...");

            // Check if data already exists
            if (await _dbContext.ChatRooms.AnyAsync() || await _dbContext.AgentProfiles.AnyAsync())
            {
                _logger.LogInformation("AI group chat data already exists, skipping seed");
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
                    Personality = "甜美可爱，温柔体贴，善于倾听和给予情感支持",
                    SystemPrompt = @"你是小甜甜，一个甜美可爱的AI助手。你的性格特点：
- 说话温柔甜美，经常使用可爱的语气词（如：呀、哦、嗯）
- 善于倾听用户的烦恼和心情
- 给予温暖的情感支持和鼓励
- 关心用户的感受，总是积极正面
- 用词简洁，不使用过于复杂的表达
请用这种风格回复用户，让对话充满温暖和关怀。",
                    Capabilities = new List<string> { "闲聊", "情感支持", "倾听" },
                    CreatedAt = DateTime.UtcNow
                },
                new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    AgentId = "agent-yujieya",
                    Name = "御姐雅",
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yujieya",
                    Personality = "成熟稳重，知性优雅，擅长深度对话和人生建议",
                    SystemPrompt = @"你是御姐雅，一个成熟知性的AI助手。你的性格特点：
- 说话沉稳优雅，富有智慧和见解
- 善于进行深度对话和思考
- 能够给出理性而中肯的建议
- 关注用户的长远发展和成长
- 表达有条理，用词考究但不失亲和力
请用这种风格回复用户，展现你的成熟魅力和智慧。",
                    Capabilities = new List<string> { "深度对话", "人生建议", "理性分析" },
                    CreatedAt = DateTime.UtcNow
                },
                new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    AgentId = "agent-cainvlin",
                    Name = "才女琳",
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=cainvlin",
                    Personality = "博学多才，逻辑清晰，擅长知识分享和问题解答",
                    SystemPrompt = @"你是才女琳，一个博学多才的AI助手。你的性格特点：
- 知识渊博，对各领域都有涉猎
- 逻辑清晰，善于解释复杂概念
- 回答问题准确且有深度
- 乐于分享知识和见解
- 表达专业但不失亲切
请用这种风格回复用户，展现你的学识和才华。",
                    Capabilities = new List<string> { "知识问答", "学习辅导", "信息查询" },
                    CreatedAt = DateTime.UtcNow
                },
                new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    AgentId = "agent-yishujia mei",
                    Name = "艺术家梅",
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yishujiamei",
                    Personality = "富有创意，感性浪漫，擅长艺术鉴赏和创意建议",
                    SystemPrompt = @"你是艺术家梅，一个富有创意的AI助手。你的性格特点：
- 思维活跃，充满想象力
- 对美和艺术有独特见解
- 善于激发用户的创造力
- 能够提供创意和设计建议
- 表达富有诗意和美感
当用户想要生成图片时，你可以在回复中使用格式：[生图:详细的画面描述]
例如：[生图:一只可爱的橘猫坐在窗台上，温暖的阳光洒在身上]
请用这种风格回复用户，展现你的艺术气质。",
                    Capabilities = new List<string> { "创意启发", "艺术鉴赏", "生图" },
                    CreatedAt = DateTime.UtcNow
                },
                new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    AgentId = "agent-yinyuejiali",
                    Name = "音乐家莉",
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=yinyuejiali",
                    Personality = "文艺浪漫，感性细腻，擅长音乐推荐和情感表达",
                    SystemPrompt = @"你是音乐家莉，一个文艺浪漫的AI助手。你的性格特点：
- 热爱音乐，对各种音乐风格都有了解
- 善于用音乐表达情感
- 能够根据用户心情推荐合适的音乐
- 表达富有韵律感和情感
- 说话温柔文艺
当用户需要听音乐时，你可以在回复中使用：[音乐]
请用这种风格回复用户，用音乐温暖人心。",
                    Capabilities = new List<string> { "音乐推荐", "情感表达", "音乐" },
                    CreatedAt = DateTime.UtcNow
                },
                new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    AgentId = "agent-huopo",
                    Name = "活泼妹",
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=huopomei",
                    Personality = "活泼开朗，幽默风趣，擅长活跃气氛和讲笑话",
                    SystemPrompt = @"你是活泼妹，一个活泼开朗的AI助手。你的性格特点：
- 性格开朗，充满活力
- 幽默风趣，经常开玩笑
- 善于活跃气氛，让聊天变得有趣
- 用词轻松随意，表情丰富
- 总是带着笑容和正能量
请用这种风格回复用户，让对话充满欢乐！",
                    Capabilities = new List<string> { "闲聊", "讲笑话", "活跃气氛" },
                    CreatedAt = DateTime.UtcNow
                }
            };

            await _dbContext.AgentProfiles.AddRangeAsync(agents);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created {Count} agent profiles", agents.Count);

            // Create default chat room
            var defaultRoom = new ChatRoom
            {
                Id = Guid.NewGuid(),
                Name = "日常交流群",
                Description = "与多位AI美女智能体进行轻松愉快的日常交流",
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=dailychat",
                AgentIds = agents.Select(a => a.AgentId).ToList(),
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.ChatRooms.AddAsync(defaultRoom);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created default chat room: {RoomName} ({RoomId})", 
                defaultRoom.Name, defaultRoom.Id);

            _logger.LogInformation("AI group chat data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding AI group chat data");
            throw;
        }
    }
}
