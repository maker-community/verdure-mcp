using System.ComponentModel;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Server.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// MCP Tool for AI group chat functionality
/// Allows users to interact with multiple AI agents in a chat room
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
    /// Interact with AI group chat - send messages, manage rooms, and view history
    /// </summary>
    [McpServerTool(Name = "chat_with_group")]
    [Description("与AI群组进行交互,发送消息、管理群组和查看历史记录")]
    public async Task<ChatResponse> ChatWithGroup(
        [Description("消息内容或操作参数")] string? message = null,
        [Description("群组ID(可选,默认使用用户的默认群组)")] string? roomId = null,
        [Description("操作类型: send(发送消息), list_rooms(列出群组), join(加入群组), set_default(设置默认群组), get_history(获取历史)")] 
        string action = "send",
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No userId provided in X-User-Id header");
            return new ChatResponse
            {
                Success = false,
                Message = "缺少用户ID (X-User-Id header)"
            };
        }

        _logger.LogInformation("Chat group action: {Action} for user {UserId}", action, userId);

        return action.ToLower() switch
        {
            "send" => await SendMessageAsync(userId, message, roomId, cancellationToken),
            "list_rooms" => await ListRoomsAsync(userId, cancellationToken),
            "join" => await JoinRoomAsync(userId, roomId, cancellationToken),
            "set_default" => await SetDefaultRoomAsync(userId, roomId, cancellationToken),
            "get_history" => await GetHistoryAsync(userId, roomId, cancellationToken),
            _ => new ChatResponse
            {
                Success = false,
                Message = $"未知的操作类型: {action}"
            }
        };
    }

    /// <summary>
    /// Send a message to the chat group
    /// </summary>
    private async Task<ChatResponse> SendMessageAsync(
        string userId,
        string? message,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message))
        {
            return new ChatResponse
            {
                Success = false,
                Message = "消息内容不能为空"
            };
        }

        // Get the chat room (use specified or default)
        Guid chatRoomId;
        if (!string.IsNullOrEmpty(roomId))
        {
            if (!Guid.TryParse(roomId, out chatRoomId))
            {
                return new ChatResponse
                {
                    Success = false,
                    Message = "无效的群组ID格式"
                };
            }
        }
        else
        {
            // Get user's default chat room
            var membership = await _dbContext.UserChatRoomMemberships
                .Where(m => m.UserId == userId && m.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);

            if (membership == null)
            {
                // Auto-join the first available room
                var firstRoom = await _dbContext.ChatRooms.FirstOrDefaultAsync(cancellationToken);
                if (firstRoom == null)
                {
                    return new ChatResponse
                    {
                        Success = false,
                        Message = "没有可用的聊天群组,请先创建或加入一个群组"
                    };
                }

                // Auto-join
                membership = new UserChatRoomMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ChatRoomId = firstRoom.Id,
                    IsDefault = true,
                    JoinedAt = DateTime.UtcNow
                };

                _dbContext.UserChatRoomMemberships.Add(membership);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Auto-joined user {UserId} to room {RoomId}", userId, firstRoom.Id);
            }

            chatRoomId = membership.ChatRoomId;
        }

        // Verify user is a member of the room
        var isMember = await _dbContext.UserChatRoomMemberships
            .AnyAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (!isMember)
        {
            return new ChatResponse
            {
                Success = false,
                Message = "您不是该群组的成员,请先加入群组"
            };
        }

        // Save user message to database
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = chatRoomId,
            SenderId = userId,
            IsAgent = false,
            Content = message,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ChatMessages.Add(userMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved user message {MessageId} in room {RoomId}", userMessage.Id, chatRoomId);

        // Schedule background job to process the message with agents
        _backgroundJobClient.Enqueue<ChatMessageBackgroundJob>(
            job => job.ExecuteAsync(chatRoomId, userMessage.Id, userId, CancellationToken.None));

        return new ChatResponse
        {
            Success = true,
            Message = "消息已发送,AI智能体正在处理中...",
            Data = new
            {
                messageId = userMessage.Id,
                roomId = chatRoomId,
                timestamp = userMessage.CreatedAt
            }
        };
    }

    /// <summary>
    /// List available chat rooms (limited to first 3)
    /// </summary>
    private async Task<ChatResponse> ListRoomsAsync(string userId, CancellationToken cancellationToken)
    {
        var rooms = await _dbContext.ChatRooms
            .OrderBy(r => r.CreatedAt)
            .Take(3)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                description = r.Description,
                avatarUrl = r.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        // Check which rooms the user is a member of
        var memberRoomIds = await _dbContext.UserChatRoomMemberships
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatRoomId)
            .ToListAsync(cancellationToken);

        var roomsWithMembership = rooms.Select(r => new
        {
            r.id,
            r.name,
            r.description,
            r.avatarUrl,
            isMember = memberRoomIds.Contains(r.id)
        });

        return new ChatResponse
        {
            Success = true,
            Message = $"找到 {rooms.Count} 个聊天群组",
            Data = roomsWithMembership
        };
    }

    /// <summary>
    /// Join a chat room
    /// </summary>
    private async Task<ChatResponse> JoinRoomAsync(
        string userId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(roomId) || !Guid.TryParse(roomId, out var chatRoomId))
        {
            return new ChatResponse
            {
                Success = false,
                Message = "请提供有效的群组ID"
            };
        }

        var room = await _dbContext.ChatRooms.FindAsync(new object[] { chatRoomId }, cancellationToken);
        if (room == null)
        {
            return new ChatResponse
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
            return new ChatResponse
            {
                Success = false,
                Message = "您已经是该群组的成员"
            };
        }

        // Add membership
        var membership = new UserChatRoomMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChatRoomId = chatRoomId,
            IsDefault = false,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.UserChatRoomMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChatResponse
        {
            Success = true,
            Message = $"成功加入群组: {room.Name}",
            Data = new
            {
                roomId = chatRoomId,
                roomName = room.Name,
                joinedAt = membership.JoinedAt
            }
        };
    }

    /// <summary>
    /// Set a room as the default for the user
    /// </summary>
    private async Task<ChatResponse> SetDefaultRoomAsync(
        string userId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(roomId) || !Guid.TryParse(roomId, out var chatRoomId))
        {
            return new ChatResponse
            {
                Success = false,
                Message = "请提供有效的群组ID"
            };
        }

        // Verify membership
        var membership = await _dbContext.UserChatRoomMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == chatRoomId, cancellationToken);

        if (membership == null)
        {
            return new ChatResponse
            {
                Success = false,
                Message = "您不是该群组的成员"
            };
        }

        // Clear other default flags
        var otherMemberships = await _dbContext.UserChatRoomMemberships
            .Where(m => m.UserId == userId && m.Id != membership.Id)
            .ToListAsync(cancellationToken);

        foreach (var other in otherMemberships)
        {
            other.IsDefault = false;
        }

        membership.IsDefault = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChatResponse
        {
            Success = true,
            Message = "默认群组已设置",
            Data = new
            {
                roomId = chatRoomId
            }
        };
    }

    /// <summary>
    /// Get message history for a chat room
    /// </summary>
    private async Task<ChatResponse> GetHistoryAsync(
        string userId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        // Get the chat room ID
        Guid chatRoomId;
        if (!string.IsNullOrEmpty(roomId))
        {
            if (!Guid.TryParse(roomId, out chatRoomId))
            {
                return new ChatResponse
                {
                    Success = false,
                    Message = "无效的群组ID格式"
                };
            }
        }
        else
        {
            // Get user's default room
            var membership = await _dbContext.UserChatRoomMemberships
                .Where(m => m.UserId == userId && m.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);

            if (membership == null)
            {
                return new ChatResponse
                {
                    Success = false,
                    Message = "请先设置默认群组或指定群组ID"
                };
            }

            chatRoomId = membership.ChatRoomId;
        }

        // Get recent messages (last 20)
        var messages = await _dbContext.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                isAgent = m.IsAgent,
                content = m.Content,
                metadata = m.Metadata,
                timestamp = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ChatResponse
        {
            Success = true,
            Message = $"获取到 {messages.Count} 条历史消息",
            Data = new
            {
                roomId = chatRoomId,
                messages
            }
        };
    }
}

/// <summary>
/// Response from chat operations
/// </summary>
public class ChatResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public object? Data { get; set; }
}
