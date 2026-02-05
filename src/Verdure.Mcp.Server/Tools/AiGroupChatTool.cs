using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Hangfire;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Server.Services;
using System.Text.Json;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// MCP Tool for AI group chat interaction
/// </summary>
[McpServerToolType]
public class AiGroupChatTool
{
    private readonly McpDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<AiGroupChatTool> _logger;

    public AiGroupChatTool(
        McpDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IBackgroundJobClient backgroundJobClient,
        ILogger<AiGroupChatTool> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <summary>
    /// Interact with AI group chat rooms. Send messages, manage rooms, and view history.
    /// Supports multiple actions: send (default), list_rooms, join, set_default, get_history.
    /// </summary>
    [McpServerTool(Name = "chat_with_group")]
    [Description("与 AI 群组交互，发送消息并接收智能体回复。支持群组管理和历史查询。")]
    public async Task<GroupChatResponse> ChatWithGroup(
        [Description("发送给群组的消息内容（当 action=send 时必需）")] string? message = null,
        [Description("群组 ID（可选，默认使用用户当前默认群组）")] string? roomId = null,
        [Description("操作类型：send(发送消息), list_rooms(已加入的群组), discover(发现所有可用群组), join(加入群组), set_default(设置默认群组), get_history(获取历史)")] 
        string action = "send",
        [Description("每页数量（用于 list_rooms, discover 和 get_history）")] int limit = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            // 从请求头提取用户 ID (X-User-Id)
            var userId = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
            
            // 从请求头提取邮箱地址 (X-User-Email)
            var userEmail = httpContext?.Request.Headers["X-User-Email"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new GroupChatResponse
                {
                    Success = false,
                    Message = "用户 ID 未提供。请确保 X-User-Id 请求头存在。"
                };
            }

            _logger.LogInformation("ChatWithGroup called: action={Action}, userId={UserId}, userEmail={UserEmail}, roomId={RoomId}",
                action, userId, userEmail ?? "未提供", roomId ?? "default");

            return action.ToLower() switch
            {
                "send" => await HandleSendMessageAsync(userId, userEmail, message, roomId, cancellationToken),
                "list_rooms" => await HandleListRoomsAsync(userId, limit, cancellationToken),
                "discover" => await HandleDiscoverRoomsAsync(userId, limit, cancellationToken),
                "join" => await HandleJoinRoomAsync(userId, roomId, cancellationToken),
                "set_default" => await HandleSetDefaultRoomAsync(userId, roomId, cancellationToken),
                "get_history" => await HandleGetHistoryAsync(userId, roomId, limit, cancellationToken),
                _ => new GroupChatResponse
                {
                    Success = false,
                    Message = $"未知的操作类型: {action}。支持的操作: send, list_rooms, discover, join, set_default, get_history"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatWithGroup");
            return new GroupChatResponse
            {
                Success = false,
                Message = $"处理请求时发生错误: {ex.Message}"
            };
        }
    }

    private async Task<GroupChatResponse> HandleSendMessageAsync(
        string userId,
        string? userEmail,
        string? message,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "消息内容不能为空"
            };
        }

        // Get chat room (default or specified)
        Guid chatRoomId;
        if (!string.IsNullOrEmpty(roomId))
        {
            if (!Guid.TryParse(roomId, out chatRoomId))
            {
                return new GroupChatResponse
                {
                    Success = false,
                    Message = "无效的群组 ID 格式"
                };
            }
        }
        else
        {
            // Get user's default room
            var defaultMembership = await _dbContext.UserChatRoomMemberships
                .FirstOrDefaultAsync(m => m.UserId == userId && m.IsDefault, cancellationToken);

            if (defaultMembership == null)
            {
                return new GroupChatResponse
                {
                    Success = false,
                    Message = "未找到默认群组。请先加入一个群组或指定群组 ID。"
                };
            }

            chatRoomId = defaultMembership.ChatRoomId;
        }

        // Verify user is a member of the room
        var isMember = await _dbContext.UserChatRoomMemberships
            .AnyAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (!isMember)
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "您不是该群组的成员"
            };
        }

        // Save user message to database
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = chatRoomId,
            UserId = userId,
            SenderId = userId,
            IsAgent = false,
            Content = message,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ChatMessages.Add(userMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue background job for agent processing
        var jobId = _backgroundJobClient.Enqueue<ChatMessageBackgroundJob>(
            job => job.ProcessChatMessageAsync(chatRoomId, userMessage.Id, userId, userEmail, CancellationToken.None));

        _logger.LogInformation(
            "User message saved and background job enqueued: messageId={MessageId}, jobId={JobId}",
            userMessage.Id, jobId);

        return new GroupChatResponse
        {
            Success = true,
            Message = "消息已发送，智能体正在处理中...",
            Data = new
            {
                messageId = userMessage.Id,
                chatRoomId = chatRoomId,
                jobId = jobId,
                status = "processing"
            }
        };
    }

    private async Task<GroupChatResponse> HandleListRoomsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.UserChatRoomMemberships
            .Include(m => m.ChatRoom)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.JoinedAt)
            .Take(Math.Min(limit, 10)) // Max 10 rooms
            .ToListAsync(cancellationToken);

        var rooms = memberships.Select(m => new
        {
            id = m.ChatRoomId,
            name = m.ChatRoom.Name,
            description = m.ChatRoom.Description,
            avatarUrl = m.ChatRoom.AvatarUrl,
            isDefault = m.IsDefault,
            joinedAt = m.JoinedAt,
            agentCount = m.ChatRoom.AgentIds.Count
        }).ToList();

        return new GroupChatResponse
        {
            Success = true,
            Message = $"找到 {rooms.Count} 个已加入的群组",
            Data = new { rooms = rooms }
        };
    }

    private async Task<GroupChatResponse> HandleDiscoverRoomsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Get user's joined room IDs
        var joinedRoomIds = await _dbContext.UserChatRoomMemberships
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatRoomId)
            .ToListAsync(cancellationToken);

        // Get all available rooms
        var allRooms = await _dbContext.ChatRooms
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Take(Math.Min(limit, 20)) // Max 20 rooms
            .ToListAsync(cancellationToken);

        // Get agents info for display
        var agentIds = allRooms.SelectMany(r => r.AgentIds).Distinct().ToList();
        var agents = await _dbContext.AgentProfiles
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.AgentId))
            .ToDictionaryAsync(a => a.AgentId, a => a.Name, cancellationToken);

        var rooms = allRooms.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            description = r.Description,
            avatarUrl = r.AvatarUrl,
            agentCount = r.AgentIds.Count,
            agentNames = r.AgentIds.Select(agentId => agents.GetValueOrDefault(agentId, agentId)).ToList(),
            isJoined = joinedRoomIds.Contains(r.Id),
            createdAt = r.CreatedAt
        }).ToList();

        return new GroupChatResponse
        {
            Success = true,
            Message = $"发现 {rooms.Count} 个可用群组（{rooms.Count(r => r.isJoined)} 个已加入）",
            Data = new { rooms = rooms }
        };
    }

    private async Task<GroupChatResponse> HandleJoinRoomAsync(
        string userId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(roomId) || !Guid.TryParse(roomId, out var chatRoomId))
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "请提供有效的群组 ID"
            };
        }

        // Check if room exists
        var room = await _dbContext.ChatRooms
            .FirstOrDefaultAsync(r => r.Id == chatRoomId, cancellationToken);

        if (room == null)
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "群组不存在"
            };
        }

        // Check if already a member
        var existingMembership = await _dbContext.UserChatRoomMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (existingMembership != null)
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "您已经是该群组的成员"
            };
        }

        // Check if user has any memberships (if not, this will be default)
        var hasOtherMemberships = await _dbContext.UserChatRoomMemberships
            .AnyAsync(m => m.UserId == userId, cancellationToken);

        // Add membership
        var membership = new UserChatRoomMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChatRoomId = chatRoomId,
            IsDefault = !hasOtherMemberships, // First room is default
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.UserChatRoomMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GroupChatResponse
        {
            Success = true,
            Message = $"成功加入群组: {room.Name}",
            Data = new
            {
                roomId = chatRoomId,
                roomName = room.Name,
                isDefault = membership.IsDefault
            }
        };
    }

    private async Task<GroupChatResponse> HandleSetDefaultRoomAsync(
        string userId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(roomId) || !Guid.TryParse(roomId, out var chatRoomId))
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "请提供有效的群组 ID"
            };
        }

        // Check if user is a member
        var membership = await _dbContext.UserChatRoomMemberships
            .Include(m => m.ChatRoom)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (membership == null)
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "您不是该群组的成员"
            };
        }

        // Clear all default flags for this user
        var allMemberships = await _dbContext.UserChatRoomMemberships
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var m in allMemberships)
        {
            m.IsDefault = false;
        }

        // Set new default
        membership.IsDefault = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GroupChatResponse
        {
            Success = true,
            Message = $"已将 '{membership.ChatRoom.Name}' 设置为默认群组",
            Data = new
            {
                roomId = chatRoomId,
                roomName = membership.ChatRoom.Name
            }
        };
    }

    private async Task<GroupChatResponse> HandleGetHistoryAsync(
        string userId,
        string? roomId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Get chat room (default or specified)
        Guid chatRoomId;
        if (!string.IsNullOrEmpty(roomId))
        {
            if (!Guid.TryParse(roomId, out chatRoomId))
            {
                return new GroupChatResponse
                {
                    Success = false,
                    Message = "无效的群组 ID 格式"
                };
            }
        }
        else
        {
            // Get user's default room
            var defaultMembership = await _dbContext.UserChatRoomMemberships
                .FirstOrDefaultAsync(m => m.UserId == userId && m.IsDefault, cancellationToken);

            if (defaultMembership == null)
            {
                return new GroupChatResponse
                {
                    Success = false,
                    Message = "未找到默认群组"
                };
            }

            chatRoomId = defaultMembership.ChatRoomId;
        }

        // Verify user is a member
        var isMember = await _dbContext.UserChatRoomMemberships
            .AnyAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (!isMember)
        {
            return new GroupChatResponse
            {
                Success = false,
                Message = "您不是该群组的成员"
            };
        }

        // Get recent messages
        var messages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatRoomId == chatRoomId && m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Min(limit, 50)) // Max 50 messages
            .Select(m => new
            {
                id = m.Id,
                userId = m.UserId,
                senderId = m.SenderId,
                isAgent = m.IsAgent,
                content = m.Content,
                metadata = m.Metadata,
                createdAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new GroupChatResponse
        {
            Success = true,
            Message = $"找到 {messages.Count} 条历史消息",
            Data = new
            {
                chatRoomId = chatRoomId,
                messages = messages.OrderBy(m => m.createdAt).ToList()
            }
        };
    }
}

/// <summary>
/// Response model for group chat operations
/// </summary>
public class GroupChatResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public object? Data { get; set; }
}
