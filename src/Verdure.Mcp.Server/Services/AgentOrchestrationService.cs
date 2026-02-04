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

            // Note: UserContext should be set by the caller (e.g., ChatMessageBackgroundJob)
            // to ensure correct user information is available for MCP tool calls
            if (UserContext.Current == null)
            {
                _logger.LogWarning("UserContext is not set. MCP tool calls may not have user information.");
            }

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
            var toolCalls = new List<ToolCall>();
            string? currentAgentId = null;
            string? currentAgentName = null;
            var currentContent = new System.Text.StringBuilder();

            await using (var run = await InProcessExecution.StreamAsync(workflow, messages))
            {
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

                // Process workflow events to extract agent responses
                await foreach (var evt in run.WatchStreamAsync(cancellationToken))
                {
                    _logger.LogTrace("Received workflow event: {EventType}", evt.GetType().Name);
                    
                    // Handle AgentResponseUpdateEvent to track agent switches and content
                    if (evt is AgentResponseUpdateEvent agentUpdateEvent)
                    {
                        var update = agentUpdateEvent.Update;
                        var executorId = agentUpdateEvent.ExecutorId;
                        
                        // Check if agent switched
                        if (executorId != currentAgentId)
                        {
                            // Save previous agent's response if any
                            if (currentAgentId != null && currentContent.Length > 0)
                            {
                                responses.Add(new AgentResponseContent
                                {
                                    AgentId = currentAgentId,
                                    AgentName = currentAgentName ?? currentAgentId,
                                    Content = currentContent.ToString()
                                });
                                
                                _logger.LogDebug("Agent {AgentName} completed response ({Length} chars)",
                                    currentAgentName, currentContent.Length);
                            }

                            // Switch to new agent
                            currentAgentId = executorId;
                            currentAgentName = update.AuthorName ?? executorId; // Use AuthorName if available
                            currentContent.Clear();
                            
                            _logger.LogInformation("Agent switched to: {AgentId} ({AgentName})", currentAgentId, currentAgentName);
                        }

                        // Accumulate content from this agent
                        var textContent = update.Text;
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            currentContent.Append(textContent);
                        }

                        // Capture tool calls from message contents
                        if (update.Contents != null)
                        {
                            foreach (var content in update.Contents)
                            {
                                if (content is FunctionCallContent functionCall)
                                {
                                    _logger.LogInformation("Tool call detected: {ToolName} by agent {AgentId}",
                                        functionCall.Name, currentAgentId);

                                    var parameters = new Dictionary<string, object>();
                                    if (functionCall.Arguments != null)
                                    {
                                        try
                                        {
                                            var jsonString = functionCall.Arguments.ToString();
                                            if (!string.IsNullOrEmpty(jsonString))
                                            {
                                                var argsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
                                                if (argsDict != null)
                                                {
                                                    foreach (var kvp in argsDict)
                                                    {
                                                        parameters[kvp.Key] = kvp.Value.ToString();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to parse function arguments");
                                        }
                                    }

                                    toolCalls.Add(new ToolCall
                                    {
                                        ToolName = functionCall.Name,
                                        Parameters = parameters
                                    });
                                }
                                else if (content is FunctionResultContent functionResult)
                                {
                                    var resultStr = functionResult.Result?.ToString();
                                    _logger.LogInformation("Tool result received, CallId: {CallId}, Result: {Result}",
                                        functionResult.CallId,
                                        resultStr != null && resultStr.Length > 100 ? resultStr.Substring(0, 100) + "..." : resultStr);
                                }
                            }
                        }
                    }
                    // Handle final workflow output
                    else if (evt is WorkflowOutputEvent outputEvent)
                    {
                        _logger.LogDebug("Workflow output event received");
                    }
                }
            }

            // Save final agent's response
            if (currentAgentId != null && currentContent.Length > 0)
            {
                responses.Add(new AgentResponseContent
                {
                    AgentId = currentAgentId,
                    AgentName = currentAgentName ?? currentAgentId,
                    Content = currentContent.ToString()
                });
                
                _logger.LogDebug("Final agent {AgentName} response saved ({Length} chars)",
                    currentAgentName, currentContent.Length);
            }

            // Get the final response (filter out triage agent's responses if any)
            var finalResponse = responses.LastOrDefault(r => r.AgentId != "triage");

            if (finalResponse == null)
            {
                _logger.LogWarning("No specialist agent response found. All responses: {ResponseCount}", responses.Count);
                
                // Fallback: use any response if available
                finalResponse = responses.LastOrDefault();
                
                if (finalResponse == null)
                {
                    throw new InvalidOperationException("No agent response generated from workflow");
                }
            }

            _logger.LogInformation("Agent {AgentName} ({AgentId}) responded with message of length {Length}",
                finalResponse.AgentName, finalResponse.AgentId, finalResponse.Content.Length);

            // Get agent metadata from database
            var finalAgentProfile = await _dbContext.AgentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgentId == finalResponse.AgentName, cancellationToken);

            return new AgentResponse
            {
                AgentId = finalResponse.AgentId,
                AgentName = finalAgentProfile?.Name ?? finalResponse.AgentName,
                Content = finalResponse.Content,
                Metadata = new Dictionary<string, object>
                {
                    ["avatar"] = finalAgentProfile?.Avatar ?? string.Empty,
                    ["personality"] = finalAgentProfile?.Personality ?? string.Empty
                },
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null // Include tool calls
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
