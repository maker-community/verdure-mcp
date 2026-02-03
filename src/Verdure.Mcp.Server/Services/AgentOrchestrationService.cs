using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Domain.Entities;
using System.Text.Json;

// 使用类型别名解决命名空间冲突
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Agent orchestration service using Microsoft Agent Framework's Handoff pattern
/// </summary>
public class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly McpDbContext _dbContext;
    private readonly WorkflowManager _workflowManager;
    private readonly ILogger<AgentOrchestrationService> _logger;

    public AgentOrchestrationService(
        McpDbContext dbContext,
        WorkflowManager workflowManager,
        ILogger<AgentOrchestrationService> logger)
    {
        _dbContext = dbContext;
        _workflowManager = workflowManager;
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

            // Get chat history (last 10 messages for context)
            var historyMessages = await GetChatHistoryAsync(chatRoomId, limit: 10, cancellationToken);

            // Prepare messages for the workflow
            var messages = new List<AIChatMessage>();
            
            // Add history as context
            foreach (var histMsg in historyMessages)
            {
                var role = histMsg.IsAgent ? ChatRole.Assistant : ChatRole.User;
                messages.Add(new AIChatMessage(role, histMsg.Content));
            }

            // Add current user message
            messages.Add(new AIChatMessage(ChatRole.User, message));

            _logger.LogDebug("Prepared {MessageCount} messages for workflow execution", messages.Count);

            // Get or create workflow for this chat room
            var workflow = await _workflowManager.GetOrCreateWorkflowAsync(chatRoomId, cancellationToken);

            _logger.LogInformation("Starting workflow execution for chat room {ChatRoomId}", chatRoomId);

            // Execute workflow using InProcessExecution
            var responses = new List<AgentResponseContent>();
            string? currentAgentId = null;
            string? currentAgentName = null;
            var currentContent = new System.Text.StringBuilder();

            await using (var run = await InProcessExecution.StreamAsync(workflow, messages))
            {
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

                // Process workflow events
                await foreach (var evt in run.WatchStreamAsync(cancellationToken))
                {
                    // Log event type for debugging
                    _logger.LogTrace("Received workflow event: {EventType}", evt.GetType().Name);
                    
                    // The actual event processing depends on the Microsoft Agent Framework version
                    // For now, we'll use a simplified approach
                    // TODO: Update based on the actual event structure in the framework
                }
            }

            // For now, return a temporary response since event processing needs to be updated
            // TODO: Update this section once we understand the correct event structure
            _logger.LogWarning("Workflow event processing not yet implemented. Returning placeholder response.");
            
            // Return a default response from the first available agent
            var firstAgent = await _dbContext.AgentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (firstAgent == null)
            {
                throw new InvalidOperationException("No agents found in database");
            }

            return new AgentResponse
            {
                AgentId = firstAgent.AgentId,
                AgentName = firstAgent.Name,
                Content = "I'm processing your message. Event handling implementation pending.",
                Metadata = new Dictionary<string, object>
                {
                    ["avatar"] = firstAgent.Avatar ?? string.Empty,
                    ["personality"] = firstAgent.Personality ?? string.Empty
                },
                ToolCalls = null
            };

            /* Original code - commented out until event structure is confirmed
            // Save final agent's response
            if (currentAgentId != null && currentContent.Length > 0)
            {
                responses.Add(new AgentResponseContent
                {
                    AgentId = currentAgentId,
                    AgentName = currentAgentName ?? currentAgentId,
                    Content = currentContent.ToString()
                });
            }

            // Get the final response (filter out triage agent's responses)
            var finalResponse = responses.FirstOrDefault(r => r.AgentId != "triage");

            if (finalResponse == null)
            {
                throw new InvalidOperationException("No agent response generated from workflow");
            }

            _logger.LogInformation("Agent {AgentName} responded with message of length {Length}",
                finalResponse.AgentName, finalResponse.Content.Length);

            // Get agent metadata
            var finalAgentProfile = await _dbContext.AgentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgentId == finalResponse.AgentId, cancellationToken);

            return new AgentResponse
            {
                AgentId = finalResponse.AgentId,
                AgentName = finalResponse.AgentName,
                Content = finalResponse.Content,
                Metadata = new Dictionary<string, object>
                {
                    ["avatar"] = finalAgentProfile?.Avatar ?? string.Empty,
                    ["personality"] = finalAgentProfile?.Personality ?? string.Empty
                },
                ToolCalls = null // Tool calls are handled by the framework automatically
            };
            */
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
        
        // Pre-create and cache the workflow for this chat room
        await _workflowManager.GetOrCreateWorkflowAsync(chatRoomId, cancellationToken);
        
        _logger.LogInformation("Workflow initialized and cached for chat room {ChatRoomId}", chatRoomId);
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
}

/// <summary>
/// Helper class to hold agent response content during workflow execution
/// </summary>
internal class AgentResponseContent
{
    public required string AgentId { get; set; }
    public required string AgentName { get; set; }
    public required string Content { get; set; }
}
