using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Workflow Manager - Manages Handoff Workflows for AI Group Chat
/// Based on Microsoft Agent Framework's Handoff pattern
/// </summary>
public class WorkflowManager
{
    private readonly IChatClient _chatClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WorkflowManager> _logger;
    
    // Cache workflows by chat room ID
    private readonly Dictionary<Guid, Workflow> _workflowCache = new();
    private readonly object _cacheLock = new();

    public WorkflowManager(
        IChatClient chatClient,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WorkflowManager> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get or create a workflow for a specific chat room
    /// </summary>
    public async Task<Workflow> GetOrCreateWorkflowAsync(Guid chatRoomId, CancellationToken cancellationToken = default)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_workflowCache.TryGetValue(chatRoomId, out var cachedWorkflow))
            {
                _logger.LogDebug("Using cached workflow for chat room {ChatRoomId}", chatRoomId);
                return cachedWorkflow;
            }
        }

        // Create new workflow
        var workflow = await CreateWorkflowAsync(chatRoomId, cancellationToken);
        
        // Cache it
        lock (_cacheLock)
        {
            _workflowCache[chatRoomId] = workflow;
        }
        
        _logger.LogInformation("Created and cached new workflow for chat room {ChatRoomId}", chatRoomId);
        return workflow;
    }

    /// <summary>
    /// Clear workflow cache for a specific chat room (when agents are updated)
    /// </summary>
    public void ClearWorkflowCache(Guid chatRoomId)
    {
        lock (_cacheLock)
        {
            if (_workflowCache.Remove(chatRoomId))
            {
                _logger.LogInformation("Cleared workflow cache for chat room {ChatRoomId}", chatRoomId);
            }
        }
    }

    /// <summary>
    /// Clear all workflow caches
    /// </summary>
    public void ClearAllWorkflowCache()
    {
        lock (_cacheLock)
        {
            _workflowCache.Clear();
            _logger.LogInformation("Cleared all workflow cache");
        }
    }

    private async Task<Workflow> CreateWorkflowAsync(Guid chatRoomId, CancellationToken cancellationToken)
    {
        // Create a scope to get DbContext
        using var scope = _serviceScopeFactory.CreateScope();
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
        var specialistAgents = agents.Select(agent =>
        {
            var instructions = agent.SystemPrompt +
                "\n\n【重要提示】" +
                "\n- 如果用户询问超出你专业领域的问题，可以建议他们询问其他智能体，但仍需提供有帮助的回答。" +
                "\n- 始终保持你的个性和风格。" +
                "\n- 回复要自然、友好、符合角色设定。";

            return new ChatClientAgent(
                _chatClient,
                instructions: instructions,
                name: agent.AgentId,  // Use AgentId as agent name
                description: agent.Personality);
        }).ToList();

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
