# MCP 用户上下文转发实现文档

## 📋 概述

本文档详细说明了在 Verdure MCP 项目中如何实现将用户信息（User ID 和 Email）从群组聊天工具调用转发到外部 MCP Server 工具调用的完整机制。

**实现日期**: 2026-02-04  
**版本**: v1.1  
**最后更新**: 2026-02-04 - 完善 AiGroupChatTool 用户信息获取链路

---

## 🎯 问题背景

### 原始问题
当用户通过 AI 群组聊天调用外部 MCP 工具时（如图像生成、音乐播放），这些工具需要知道是哪个用户发起的请求，以便：
1. 将结果发送给正确的用户设备
2. 记录用户操作日志
3. 实现用户级别的权限控制和配额管理

### 调用链路

```
用户 → AiGroupChatTool (X-User-Id in header)
    ↓
    ChatMessageBackgroundJob
    ↓
    AgentOrchestrationService (需要传递 userId)
    ↓
    WorkflowManager
    ↓
    Specialist Agent 调用 MCP 工具
    ↓
    McpToolService (需要注入 X-User-Id 和 X-User-Email)
    ↓
    外部 MCP Server (如 image/music 工具)
```

**核心挑战**：如何在跨越多个服务层的异步调用链中保持用户上下文？

---

## 🔧 技术方案：AsyncLocal 用户上下文

### 方案选择

我们采用 **AsyncLocal<T>** 来存储用户上下文，原因如下：

| 方案 | 优点 | 缺点 | 是否采用 |
|------|------|------|----------|
| **AsyncLocal<T>** | ✅ 自动跨异步调用流传递<br>✅ 线程安全<br>✅ 无需修改接口签名 | ⚠️ 需要确保正确设置和清理 | ✅ **已采用** |
| 修改所有方法签名 | ✅ 显式传递，易于理解 | ❌ 需要修改大量接口<br>❌ 破坏现有架构 | ❌ |
| HttpContext.Items | ✅ ASP.NET 原生支持 | ❌ 仅限 HTTP 请求生命周期<br>❌ Hangfire 后台任务无法访问 | ❌ |
| DI Scoped Service | ✅ 依赖注入友好 | ❌ Hangfire 后台任务需额外处理<br>❌ 跨服务传递复杂 | ❌ |

### AsyncLocal 工作原理

```csharp
// AsyncLocal 自动在异步调用链中传递值
AsyncLocal<UserContext> _current = new();

// 在 Service A 中设置
_current.Value = new UserContext { UserId = "user-123" };

// 在 Service B 中读取（即使跨越 await）
var userId = _current.Value?.UserId; // ✅ 可以获取到 "user-123"
```

---

## 📝 实现细节

### 1. UserContext 类定义

**文件**: [McpToolService.cs](../src/Verdure.Mcp.Server/Services/McpToolService.cs)

```csharp
/// <summary>
/// User context for MCP tool calls - stored in AsyncLocal for cross-service access
/// </summary>
public class UserContext
{
    private static readonly AsyncLocal<UserContext?> _current = new();

    public static UserContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
}
```

**特点**:
- ✅ 使用 `AsyncLocal<T>` 实现跨异步调用的上下文传递
- ✅ 静态属性 `Current` 提供全局访问点
- ✅ 存储 `UserId` 和 `UserEmail` 两个关键信息

---

### 2. 在 AiGroupChatTool 中提取用户信息

**文件**: [AiGroupChatTool.cs](../src/Verdure.Mcp.Server/Tools/AiGroupChatTool.cs)

**修改位置**: `ChatWithGroup` 方法开始处

```csharp
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
```

**关键点**:
1. **与 GenerateImageTool 一致**: 使用相同的方式从 HTTP 请求头提取用户信息
2. **同时提取两个字段**: `X-User-Id` 和 `X-User-Email`
3. **日志记录**: 记录提取的用户信息，便于调试
4. **传递给后台任务**: 将 `userEmail` 传递给 `ChatMessageBackgroundJob`

---

### 3. 在 ChatMessageBackgroundJob 中设置 UserContext

**文件**: [ChatMessageBackgroundJob.cs](../src/Verdure.Mcp.Server/Tools/ChatMessageBackgroundJob.cs)

**方法签名更新**:
```csharp
public async Task ProcessChatMessageAsync(
    Guid chatRoomId,
    Guid messageId,
    string userId,
    string? userEmail,  // ✅ 新增参数
    CancellationToken cancellationToken)
```

**设置 UserContext**:
```csharp
// ✅ Set user context for MCP tool calls (from request headers)
UserContext.Current = new UserContext
{
    UserId = userId,
    UserEmail = userEmail ?? userId // Fallback to userId if email not provided
};
_logger.LogDebug("UserContext set in ChatMessageBackgroundJob: UserId={UserId}, UserEmail={UserEmail}",
    userId, userEmail ?? userId);
```

**关键点**:
1. **设置时机**: 在调用 `AgentOrchestrationService` 之前立即设置
2. **使用真实邮箱**: 使用从 HTTP 请求头获取的 `userEmail`（而非从数据库查询）
3. **Fallback 机制**: 如果没有提供 email，使用 userId 作为 fallback
4. **日志记录**: 记录上下文设置情况，便于调试

---

### 4. 在 AgentOrchestrationService 中验证 UserContext

**文件**: [AgentOrchestrationService.cs](../src/Verdure.Mcp.Server/Services/AgentOrchestrationService.cs)

**修改位置**: `ProcessMessageAsync` 方法开始处

```csharp
public async Task<AgentResponse> ProcessMessageAsync(
    Guid chatRoomId,
    string userId,
    string message,
    CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogInformation("Processing message for chat room {ChatRoomId} from user {UserId}", 
            chatRoomId, userId);

        // Note: UserContext should be set by the caller (e.g., ChatMessageBackgroundJob)
        // to ensure correct user information is available for MCP tool calls
        if (UserContext.Current == null)
        {
            _logger.LogWarning("UserContext is not set. MCP tool calls may not have user information.");
        }

        // ... 后续处理逻辑
    }
}
```

**关键点**:
1. **不再设置 UserContext**: 由调用者（ChatMessageBackgroundJob）负责设置
2. **验证机制**: 检查 UserContext 是否已设置，未设置时记录警告
3. **职责分离**: AgentOrchestrationService 专注于业务逻辑，UserContext 由上层设置

---

### 5. 在 McpToolService 中注入用户信息到 HttpClient

**文件**: [McpToolService.cs](../src/Verdure.Mcp.Server/Services/McpToolService.cs)

#### 3.1 新增 InjectUserContextToHttpClient 方法

```csharp
/// <summary>
/// Inject user context (userId, email) into HttpClient request headers for MCP tool calls
/// </summary>
private void InjectUserContextToHttpClient(HttpClient httpClient)
{
    // Get user context from AsyncLocal storage (set by AgentOrchestrationService)
    var userId = UserContext.Current?.UserId;
    var userEmail = UserContext.Current?.UserEmail;

    if (!string.IsNullOrEmpty(userId))
    {
        httpClient.DefaultRequestHeaders.Remove("X-User-Id");
        httpClient.DefaultRequestHeaders.Add("X-User-Id", userId);
        _logger.LogDebug("Injected X-User-Id: {UserId} into MCP HttpClient", userId);
    }

    if (!string.IsNullOrEmpty(userEmail))
    {
        httpClient.DefaultRequestHeaders.Remove("X-User-Email");
        httpClient.DefaultRequestHeaders.Add("X-User-Email", userEmail);
        _logger.LogDebug("Injected X-User-Email: {UserEmail} into MCP HttpClient", userEmail);
    }
}
```

**功能**:
- 从 `UserContext.Current` 读取用户信息
- 注入 `X-User-Id` 和 `X-User-Email` 到 HttpClient 默认请求头
- 记录注入操作到日志

#### 3.2 在所有 Transport 创建方法中调用

**修改位置**: `CreateBearerTokenTransport`, `CreateOAuthTransport`, `CreateNoAuthTransport`

```csharp
private IClientTransport CreateBearerTokenTransport(McpServerConfig config)
{
    // ... 创建 httpClient
    
    // ✅ Inject user context into MCP tool calls
    InjectUserContextToHttpClient(httpClient);
    
    // ... 创建 transport
}
```

**保证**：无论使用哪种认证方式，用户信息都会被正确转发。

---

## 🔄 完整数据流

### 示例：用户请求生成图片

```
1. 用户发送消息 "帮我生成一张猫咪的图片"
   ├─ POST /mcp/all
   ├─ Headers: X-User-Id: user-123, X-User-Email: user@example.com
   ├─ AiGroupChatTool 接收请求
   ├─ 提取 X-User-Id 和 X-User-Email ✅
   └─ 创建 Hangfire 后台任务，传递 userId 和 userEmail

2. ChatMessageBackgroundJob 执行
   ├─ 接收参数: userId="user-123", userEmail="user@example.com"
   ├─ 设置 UserContext.Current = { UserId: "user-123", UserEmail: "user@example.com" } ✅
   └─ 调用 AgentOrchestrationService.ProcessMessageAsync(userId="user-123", ...)
       ⬇️ AsyncLocal 自动传递

3. WorkflowManager 创建 Workflow
   ├─ 加载 AgentProfiles（包括艺术家梅）
   ├─ McpToolService.GetToolsForCapabilitiesAsync(["生图"])
   └─ 艺术家梅获得 generate_image 工具
       ⬇️ AsyncLocal 自动传递

4. Workflow 执行
   ├─ Triage Agent 分析 → 路由到艺术家梅
   ├─ 艺术家梅调用 generate_image 工具
   ├─ FunctionInvokingChatClient 执行工具调用
   └─ McpToolService 创建 HttpClient
       ⬇️ AsyncLocal 自动传递

5. McpToolService.CreateBearerTokenTransport()
   ├─ InjectUserContextToHttpClient(httpClient)
   ├─ 读取 UserContext.Current ✅
   ├─ httpClient.DefaultRequestHeaders.Add("X-User-Id", "user-123")
   └─ httpClient.DefaultRequestHeaders.Add("X-User-Email", "user-123")

6. 调用外部 MCP Server
   ├─ POST https://external-mcp-server.com/mcp/image
   ├─ Headers:
   │   ├─ Authorization: Bearer xxx
   │   ├─ X-User-Id: user-123         ✅ 成功转发
   │   └─ X-User-Email: user@example.com      ✅ 成功转发（真实邮箱）
   └─ 外部服务接收到正确的用户信息

7. 图片生成完成
   ├─ 结果返回给 FunctionInvokingChatClient
   ├─ 艺术家梅生成回复："我给你生成了一张可爱的猫咪图片～"
   ├─ ChatMessageBackgroundJob 保存回复
   └─ SignalR 推送给 user-123 的设备 ✅
```

---

## ✅ 验证方法

### 1. 日志验证

启用详细日志，查看关键日志输出：

```bash
# appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Verdure.Mcp.Server.Services.AgentOrchestrationService": "Debug",
      "Verdure.Mcp.Server.Services.McpToolService": "Debug"
    }
  }
}
```

**期望日志输出**:
```
[AiGroupChatTool] ChatWithGroup called: action=send, userId=user-123, userEmail=user@example.com, roomId=default
[ChatMessageBackgroundJob] Processing chat message: roomId=xxx, messageId=xxx, userId=user-123, userEmail=user@example.com
[ChatMessageBackgroundJob] UserContext set in ChatMessageBackgroundJob: UserId=user-123, UserEmail=user@example.com
[AgentOrchestrationService] Processing message for chat room xxx from user user-123
[McpToolService] Injected X-User-Id: user-123 into MCP HttpClient
[McpToolService] Injected X-User-Email: user@example.com into MCP HttpClient
```

### 2. 网络抓包验证

使用 Fiddler 或 Wireshark 抓取 HTTP 请求，确认请求头：

```http
POST https://external-mcp-server.com/mcp/image HTTP/1.1
Host: external-mcp-server.com
Authorization: Bearer xxxxxx
X-User-Id: user-123
X-User-Email: user-123
Content-Type: application/json

{
  "tool": "generate_image",
  "params": {
    "prompt": "cute cat"
  }
}
```

### 3. 端到端测试

**测试脚本**: 创建测试文件 `test-user-context-forwarding.ps1`

```powershell
# 1. 发送群组消息触发工具调用
$headers = @{
    "Authorization" = "Bearer YOUR_TOKEN"
    "X-User-Id" = "test-user-456"
    "Content-Type" = "application/json"
}

$body = @{
    message = "帮我生成一张风景图片"
    action = "send"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/mcp/all" `
    -Method POST `
    -Headers $headers `
    -Body $body

# 2. 检查日志文件确认 X-User-Id 和 X-User-Email 被正确注入
```

---

## 🚀 后续扩展

### 扩展点 1: 从数据库查询真实的用户邮箱

**当前状态**: 使用 userId 作为 email 的 fallback

**建议实现**:

```csharp
// 在 AgentOrchestrationService.cs 中
string? userEmail = null;
try
{
    // 假设有 Users 表
    var user = await _dbContext.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    
    userEmail = user?.Email;
    _logger.LogDebug("User email resolved from database: {UserEmail}", userEmail);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to resolve user email from database");
}
```

### 扩展点 2: 传递更多用户信息

可以扩展 `UserContext` 类来支持更多字段：

```csharp
public class UserContext
{
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }        // 新增
    public string? UserRole { get; set; }        // 新增
    public string? TenantId { get; set; }        // 新增（多租户场景）
    public Dictionary<string, string>? Metadata { get; set; }  // 自定义元数据
}
```

### 扩展点 3: 支持用户配额管理

在外部 MCP Server 中，可以根据 X-User-Id 实现：

```csharp
// 外部 MCP Server 的工具实现
[McpServerTool(Name = "generate_image")]
public async Task<ImageResult> GenerateImage(string prompt)
{
    var userId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
    
    // ✅ 检查用户配额
    var quota = await _quotaService.GetRemainingQuotaAsync(userId);
    if (quota <= 0)
    {
        throw new Exception($"User {userId} has exceeded image generation quota");
    }
    
    // 生成图片...
    
    // ✅ 扣减配额
    await _quotaService.DecrementQuotaAsync(userId);
    
    return result;
}
```

---

## 🛠️ 故障排查

### 问题 1: 外部 MCP Server 收不到 X-User-Id

**可能原因**:
1. UserContext 没有正确设置
2. AsyncLocal 在某个异步边界丢失
3. HttpClient 被缓存，导致旧的请求头被复用

**排查步骤**:
```csharp
// 在 McpToolService.InjectUserContextToHttpClient 中添加断点
_logger.LogWarning("UserContext.Current is null!"); // 如果为 null

// 检查 AgentOrchestrationService.ProcessMessageAsync 是否执行
_logger.LogInformation("Setting UserContext: {UserId}", userId);
```

### 问题 2: 用户邮箱为空或错误

**解决方案**:
1. 实现真实的邮箱查询逻辑（参考扩展点 1）
2. 确保用户表有正确的邮箱字段
3. 添加邮箱格式验证

---

## 📚 相关文档

- [agents.md](../agents.md) - 项目架构总览
- [AI_GROUP_CHAT_GUIDE.md](AI_GROUP_CHAT_GUIDE.md) - AI 群组聊天完整指南
- [AGENT_FRAMEWORK_INTEGRATION.md](AGENT_FRAMEWORK_INTEGRATION.md) - Agent Framework 整合文档

---

## 📝 变更记录

| 日期 | 版本 | 更新内容 | 作者 |
|------|------|---------|------|
| 2026-02-04 | v1.0 | 初始版本：实现 AsyncLocal 用户上下文转发 | GitHub Copilot |

---

## 💡 总结

**核心优势**:
- ✅ **无侵入性**: 不需要修改现有接口签名
- ✅ **自动传播**: AsyncLocal 自动在异步调用链中传递
- ✅ **线程安全**: 每个异步流有独立的上下文
- ✅ **易于扩展**: 可随时添加更多用户信息字段

**最佳实践**:
1. 始终在调用链的最早期设置 UserContext
2. 记录关键日志以便排查问题
3. 考虑添加单元测试验证上下文传递
4. 在生产环境监控上下文传递的成功率

**适用场景**:
- ✅ AI 群组聊天调用外部工具
- ✅ 后台任务需要用户上下文
- ✅ 跨服务的异步调用链
- ✅ 需要记录用户操作日志的场景
