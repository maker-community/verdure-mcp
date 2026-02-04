# 用户上下文转发功能 - 快速参考

## 🎯 功能概述

**实现日期**: 2026-02-04  
**版本**: v1.1  
**最后更新**: 2026-02-04 - 完善 AiGroupChatTool 用户信息获取

**核心功能**: 在 AI 群组聊天调用外部 MCP 工具时，自动将用户信息（User ID 和 Email）转发到外部 MCP Server。

---

## 📋 实现文件

| 文件 | 主要修改 | 说明 |
|------|---------|------|
| [AiGroupChatTool.cs](../src/Verdure.Mcp.Server/Tools/AiGroupChatTool.cs) | ✅ 从 HTTP 请求头提取 `X-User-Id` 和 `X-User-Email`<br>✅ 将 `userEmail` 传递给后台任务 | 入口：提取用户信息 |
| [ChatMessageBackgroundJob.cs](../src/Verdure.Mcp.Server/Tools/ChatMessageBackgroundJob.cs) | ✅ 修改方法签名接收 `userEmail` 参数<br>✅ 设置 `UserContext.Current` | 核心：设置用户上下文 |
| [AgentOrchestrationService.cs](../src/Verdure.Mcp.Server/Services/AgentOrchestrationService.cs) | ✅ 移除 UserContext 设置逻辑<br>✅ 添加 UserContext 验证 | 验证：确保上下文已设置 |
| [McpToolService.cs](../src/Verdure.Mcp.Server/Services/McpToolService.cs) | ✅ 添加 `UserContext` 类<br>✅ 添加 `InjectUserContextToHttpClient()` 方法<br>✅ 在所有 Transport 创建方法中调用注入 | 核心实现：读取 AsyncLocal 并注入到 HttpClient |

---

## 🔧 技术方案

### AsyncLocal<T> 用户上下文

```csharp
// 定义 (McpToolService.cs)
public class UserContext
{
    private static readonly AsyncLocal<UserContext?> _current = new();
    
    public static UserContext? Current { get; set; }
    
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
}
```

### 设置上下文 (ChatMessageBackgroundJob)

```csharp
// 从 AiGroupChatTool 接收 userId 和 userEmail
public async Task ProcessChatMessageAsync(
    Guid chatRoomId,
    Guid messageId,
    string userId,
    string? userEmail,  // ✅ 来自 HTTP 请求头
    CancellationToken cancellationToken)
{
    // ✅ Set user context for MCP tool calls
    UserContext.Current = new UserContext
    {
        UserId = userId,
        UserEmail = userEmail ?? userId // Fallback to userId if email not provided
    };
}
```

### 注入到 HttpClient (McpToolService)

```csharp
private void InjectUserContextToHttpClient(HttpClient httpClient)
{
    var userId = UserContext.Current?.UserId;
    var userEmail = UserContext.Current?.UserEmail;

    if (!string.IsNullOrEmpty(userId))
    {
        httpClient.DefaultRequestHeaders.Add("X-User-Id", userId);
    }

    if (!string.IsNullOrEmpty(userEmail))
    {
        httpClient.DefaultRequestHeaders.Add("X-User-Email", userEmail);
    }
}
```

---

## 📊 调用链路（简化版）

```
AiGroupChatTool 
    ├─ 从 HTTP Headers 提取: X-User-Id, X-User-Email ✅
    ↓
ChatMessageBackgroundJob
    ├─ 接收参数: userId, userEmail
    ├─ UserContext.Current = { userId, userEmail } ✅ 设置
    ↓
AgentOrchestrationService
    ├─ 验证 UserContext 是否已设置
    ↓
WorkflowManager
    ↓
Specialist Agent 调用 MCP 工具
    ↓
McpToolService
    ├─ InjectUserContextToHttpClient() ✅ 注入
    ├─ httpClient.Headers["X-User-Id"] = userId
    ├─ httpClient.Headers["X-User-Email"] = userEmail
    ↓
外部 MCP Server ✅ 接收到真实的用户邮箱
```

---

## ✅ 验证方法

### 查看日志

```bash
# 启用详细日志
"Logging": {
  "LogLevel": {
    "Verdure.Mcp.Server.Services.AgentOrchestrationService": "Debug",
    "Verdure.Mcp.Server.Services.McpToolService": "Debug"
  }
}
```

**期望输出**:
```
[AgentOrchestrationService] UserContext set: UserId=user-123, UserEmail=user-123
[McpToolService] Injected X-User-Id: user-123 into MCP HttpClient
[McpToolService] Injected X-User-Email: user-123 into MCP HttpClient
```

### 网络抓包

使用 Fiddler 查看发送到外部 MCP Server 的请求：

```http
POST https://external-mcp-server.com/mcp/image
Authorization: Bearer xxx
X-User-Id: user-123          ✅
X-User-Email: user-123       ✅
```

---

## 🚀 使用场景

### 场景 1: 图像生成

用户: "帮我生成一张风景图片"

```
1. AiGroupChatTool 收到请求 (X-User-Id: user-123)
2. 设置 UserContext.Current
3. 艺术家梅调用 generate_image 工具
4. McpToolService 注入 X-User-Id 到 HttpClient
5. 外部图像服务接收 X-User-Id，记录用户操作
6. 图片生成后推送给 user-123 的设备
```

### 场景 2: 音乐播放

用户: "推荐一首好听的音乐"

```
1. AiGroupChatTool 收到请求 (X-User-Id: user-456)
2. 设置 UserContext.Current
3. 音乐家莉调用 play_random_music 工具
4. McpToolService 注入 X-User-Id 到 HttpClient
5. 音乐服务根据 X-User-Id 选择音乐并推送到设备
```

---

## 🛠️ 故障排查

### 问题: 外部 MCP Server 收不到 X-User-Id

**排查步骤**:

1. 检查 `UserContext` 是否正确设置
   ```csharp
   _logger.LogInformation("UserContext.Current: {Current}", 
       UserContext.Current != null ? "Set" : "NULL");
   ```

2. 检查 `InjectUserContextToHttpClient` 是否被调用
   ```csharp
   _logger.LogDebug("Injecting user context...");
   ```

3. 验证 HttpClient 请求头
   ```csharp
   _logger.LogDebug("HttpClient Headers: {Headers}", 
       string.Join(", ", httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}={h.Value}")));
   ```

### 问题: AsyncLocal 值丢失

**可能原因**:
- 跨越了非异步边界（如使用 `Task.Run`）
- 在不同的执行上下文中访问

**解决方案**:
- 确保整个调用链都是异步的（使用 `async/await`）
- 避免使用 `Task.Run` 创建新的执行上下文

---

## 📚 详细文档

- [MCP_USER_CONTEXT_FORWARDING.md](MCP_USER_CONTEXT_FORWARDING.md) - 完整实现文档
- [AI_GROUP_CHAT_CALL_FLOW.md](AI_GROUP_CHAT_CALL_FLOW.md) - 完整调用链路图
- [agents.md](../agents.md) - 项目架构总览

---

## 💡 关键优势

| 优势 | 说明 |
|------|------|
| ✅ **无侵入性** | 不需要修改现有接口签名 |
| ✅ **自动传播** | AsyncLocal 自动在异步调用链中传递 |
| ✅ **线程安全** | 每个异步流有独立的上下文 |
| ✅ **易于扩展** | 可随时添加更多用户信息字段 |

---

**更新时间**: 2026-02-04  
**维护者**: Verdure MCP Team
