using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Server.AITools;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Workflow Manager - Manages Handoff Workflows for AI Group Chat
/// Based on Microsoft Agent Framework's Handoff pattern
/// </summary>
public class WorkflowManager
{
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _rootServiceProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WorkflowManager> _logger;

    // Cache settings
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);
    private const int MaxCacheSize = 100;

    public WorkflowManager(
        IChatClient chatClient,
        IServiceProvider rootServiceProvider,
        McpToolService mcpToolService,
        IMemoryCache cache,
        ILogger<WorkflowManager> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get or create a workflow for a specific chat room
    /// </summary>
    public async Task<Workflow> GetOrCreateWorkflowAsync(Guid chatRoomId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"workflow:{chatRoomId}";

        // Try get from cache
        if (_cache.TryGetValue<Workflow>(cacheKey, out var cachedWorkflow) && cachedWorkflow != null)
        {
            _logger.LogDebug("Using cached workflow for chat room {ChatRoomId}", chatRoomId);
            return cachedWorkflow;
        }

        // Create new workflow
        _logger.LogDebug("Creating new workflow for chat room {ChatRoomId}", chatRoomId);
        var workflow = await CreateWorkflowAsync(chatRoomId, cancellationToken);

        // Cache with expiration and size limit
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(CacheExpiration)
            .SetSize(1)  // Each workflow counts as 1 unit
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _logger.LogDebug("Workflow cache evicted for {Key}, Reason: {Reason}", key, reason);
            });

        _cache.Set(cacheKey, workflow, cacheOptions);

        _logger.LogInformation("Created and cached new workflow for chat room {ChatRoomId} (expires in {Expiration})",
            chatRoomId, CacheExpiration);
        return workflow;
    }

    /// <summary>
    /// Clear workflow cache for a specific chat room (when agents are updated)
    /// </summary>
    public void ClearWorkflowCache(Guid chatRoomId)
    {
        var cacheKey = $"workflow:{chatRoomId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Cleared workflow cache for chat room {ChatRoomId}", chatRoomId);
    }

    /// <summary>
    /// Clear all workflow caches (not supported by IMemoryCache, but can be implemented with cache key tracking)
    /// </summary>
    public void ClearAllWorkflowCache()
    {
        _logger.LogWarning("ClearAllWorkflowCache called - IMemoryCache doesn't support clearing all entries. " +
            "Workflows will expire naturally after {Expiration}.", CacheExpiration);
    }

    private async Task<Workflow> CreateWorkflowAsync(Guid chatRoomId, CancellationToken cancellationToken)
    {
        // Create a scope from the root provider to resolve scoped services (e.g., DbContext)
        using var scope = _rootServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<McpDbContext>();

        // Load chat room
        var chatRoom = await dbContext.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(cr => cr.Id == chatRoomId, cancellationToken);

        if (chatRoom == null)
        {
            throw new InvalidOperationException($"Chat room {chatRoomId} not found");
        }

        // Load agent profiles
        var agents = await dbContext.AgentProfiles
            .AsNoTracking()
            .Where(a => chatRoom.AgentIds.Contains(a.AgentId))
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            throw new InvalidOperationException($"No agents found for chat room {chatRoomId}");
        }

        _logger.LogInformation("Creating workflow for chat room {ChatRoomId} ({RoomName}) with {AgentCount} agents: {AgentNames}",
            chatRoomId, chatRoom.Name, agents.Count, string.Join(", ", agents.Select(a => a.Name)));

        // Generate Triage Agent instructions dynamically
        var triageInstructions = GenerateTriageInstructions(chatRoom, agents);

        // Create Triage Agent (router - no tools, only routing)
        var triageAgent = new ChatClientAgent(
            _chatClient,
            instructions: triageInstructions,
            name: "triage",
            description: $"智能路由器 for {chatRoom.Name}");

        _logger.LogDebug("Created triage agent for chat room {ChatRoomId}", chatRoomId);

        // Create Specialist Agents (with their system prompts and capabilities)
        // ✅ Following official best practice: ChatClientAgent auto-injects FunctionInvokingChatClient
        var specialistAgents = new List<ChatClientAgent>();

        foreach (var agent in agents)
        {
            var instructions = agent.SystemPrompt +
                "\n\n【重要提示】" +
                "\n- 如果用户询问超出你专业领域的问题，可以建议他们询问其他智能体，但仍需提供有帮助的回答。" +
                "\n- 始终保持你的个性和风格。" +
                "\n- 回复要自然、友好、符合角色设定。" +
                "\n\n【使用工具时】" +
                "\n- 调用工具后，务必等待结果并将其融入你的回复中。" +
                "\n- 以清晰、用户友好的格式呈现工具结果。" +
                "\n- 对于图像 URL，可以说类似「我给你生成了一张图片，快看看吧～」。" +
                "\n- 对于音频 URL，可以说类似「我为你挑选了一首好听的音乐，快去听听吧～」。" +
                "\n- 主动使用工具来丰富对话，让互动更加生动有趣。";

            // 1. 优先使用自定义工具 (带 DI 支持，性能更好)
            var customTools = CreateCustomToolsForCapabilities(agent.Capabilities);
            // ✅ Official best practice: Pass tools directly to ChatClientAgent
            // ChatClientAgent will automatically inject FunctionInvokingChatClient when tools are present
            var chatAgent = new ChatClientAgent(
                _chatClient,  // Use original chat client - no manual wrapping needed
                instructions: instructions,
                name: agent.AgentId,  // Use AgentId as agent name
                description: agent.Personality,
                tools: customTools,
                // IMPORTANT: Do NOT pass a scoped ServiceProvider here.
                // Workflows/agents are cached and may outlive the scope used to build them.
                services: _rootServiceProvider);

            specialistAgents.Add(chatAgent);
        }

        _logger.LogInformation("Created {SpecialistCount} specialist agents for chat room {ChatRoomId}",
            specialistAgents.Count, chatRoomId);

        // Build Handoff Workflow using AgentWorkflowBuilder
        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);

        // Configure handoff paths:
        // 1. Triage can handoff to any specialist
        builder.WithHandoffs(triageAgent, specialistAgents);

        // 2. Specialists can handoff back to triage (for re-routing if needed)
        builder.WithHandoffs(specialistAgents, triageAgent);

        var workflow = builder.Build();

        _logger.LogInformation("Handoff workflow built successfully for chat room {ChatRoomId}", chatRoomId);

        return workflow;
    }

    /// <summary>
    /// 根据 capabilities 创建自定义工具 (带 DI 支持)
    /// </summary>
    /// <remarks>
    /// 自定义工具优势:
    /// - 类型安全 (编译时检查)
    /// - 性能更好 (本地调用)
    /// - 完整 DI 支持 (DbContext, Logger, HttpClient...)
    /// </remarks>
    private List<AITool> CreateCustomToolsForCapabilities(
        List<string> capabilities)
    {
        var tools = new List<AITool>();
        tools.Add(AIFunctionFactory.Create(ImageGenerationAiTool.GenerateImageAsync));
        //if (capabilities.Any(c => string.Equals(c, "生图", StringComparison.OrdinalIgnoreCase)))
        //{
        //    tools.Add(AIFunctionFactory.Create(ImageGenerationAiTool.GenerateImageAsync));
        //    _logger.LogDebug("Added AI image generation tool");
        //}
        return tools;
    }

    private string GenerateTriageInstructions(ChatRoom chatRoom, List<AgentProfile> agents)
    {
        // Build specialist descriptions dynamically
        var specialistDescriptions = string.Join("\n", agents.Select(agent =>
        {
            var capabilities = agent.Capabilities.Count > 0
                ? $"（能力：{string.Join("、", agent.Capabilities)}）"
                : "";
            return $"- {agent.AgentId}({agent.Name})：{agent.Personality}{capabilities}";
        }));

        return $@"你是 {chatRoom.Name} 的智能路由系统。你的唯一任务是分析用户消息并调用 handoff 函数将对话转交给最合适的专家智能体。

【核心规则】
1. 永远不要生成任何文本回复 - 你对用户完全透明和不可见
2. 立即调用 handoff 函数，不需要任何解释或文本
3. 不要确认、问候或回应 - 只是默默地路由

【路由策略】
分析以下因素来选择最合适的专家：
1. **话题内容**：匹配用户问题与专家的专业领域
2. **关键词**：识别消息中的关键词和能力标识（如""图""、""音乐""等）
3. **语气风格**：感受用户的语气（活泼、理性、轻松等）
4. **上下文连贯**：**重要！** 查看对话历史，如果上一条回复来自某个专家，且用户继续相关话题，应该路由到同一专家保持连贯
5. **隐式意图**：即使用户没有明确指定，也要根据话题自动选择最合适的专家

【可用的专家智能体】
{specialistDescriptions}

【执行方式】
默默分析消息，考虑上下文和话题，然后立即调用 handoff 转交给最合适的专家。不要犹豫，不要解释，直接行动。";
    }
}
