# AI 群组聊天完整调用链路图

## 📊 完整数据流（从用户请求到设备推送）

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 1️⃣ 用户发送消息 (MCP Tool 调用)                                             │
│                                                                             │
│  AI 助手 (Claude/ChatGPT)                                                   │
│       │                                                                     │
│       ├─> POST /mcp/all                                                    │
│       │   Headers:                                                         │
│       │     - Authorization: Bearer {token}                                │
│       │     - X-User-Id: user-123          ◄───┐                          │
│       │     - Content-Type: application/json    │                          │
│       │   Body: { message: "生成一张猫咪图片" }  │                         │
│       │                                          │                          │
│       ▼                                          │                          │
│  AiGroupChatTool.ChatWithGroup()                │                          │
│       │                                          │                          │
│       ├─> 提取 X-User-Id ────────────────────────┘                         │
│       ├─> 验证用户群组成员身份                                              │
│       ├─> 保存用户消息到数据库 (ChatMessage)                                 │
│       └─> 创建 Hangfire 后台任务                                            │
│               │                                                             │
│               └─> BackgroundJobClient.Enqueue<ChatMessageBackgroundJob>()  │
│                                                                             │
│  返回: { success: true, message: "消息已发送，智能体正在处理中..." }         │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ (异步执行)
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 2️⃣ 后台任务处理 (Hangfire Background Job)                                   │
│                                                                             │
│  ChatMessageBackgroundJob.ProcessChatMessageAsync()                         │
│       │                                                                     │
│       ├─> 参数: chatRoomId, messageId, userId="user-123"  ◄───┐           │
│       │                                                         │           │
│       ▼                                                         │           │
│  AgentOrchestrationService.ProcessMessageAsync()               │           │
│       │                                                         │           │
│       ├─> ✅ 设置 UserContext (AsyncLocal)                     │           │
│       │   UserContext.Current = new UserContext {              │           │
│       │       UserId = "user-123",       ◄─────────────────────┘           │
│       │       UserEmail = "user-123@example.com"                            │
│       │   }                                                                 │
│       │   ⚡ AsyncLocal 自动在整个异步调用链中传递                           │
│       │                                                                     │
│       ├─> 获取聊天历史（最近 10 条消息）                                     │
│       │                                                                     │
│       └─> WorkflowManager.GetOrCreateWorkflowAsync()                       │
│               │                                                             │
│               ├─> 检查 Workflow 缓存 (按 ChatRoom 级别)                     │
│               │                                                             │
│               ├─> 创建 Triage Agent (智能路由器)                            │
│               │   - SystemPrompt: 分析消息并路由到最合适的专家               │
│               │   - 无工具能力，只负责 handoff                               │
│               │                                                             │
│               └─> 创建 Specialist Agents (专家智能体)                       │
│                       │                                                     │
│                       ├─> 从数据库加载 AgentProfiles                        │
│                       │                                                     │
│                       └─> 为每个 Agent 加载工具                             │
│                               │                                             │
│                               ▼                                             │
│                          McpToolService.GetToolsForCapabilitiesAsync()     │
│                               │                                             │
│                               ├─> 读取配置: McpServers (appsettings.json)  │
│                               │   [{                                        │
│                               │     "id": "image-server",                   │
│                               │     "endpoint": "https://mcp-server.com",   │
│                               │     "authType": "bearer",                   │
│                               │     "bearerToken": "xxx"                    │
│                               │   }]                                        │
│                               │                                             │
│                               └─> 为每个启用的 MCP Server 创建连接          │
│                                       │                                     │
│                                       ▼                                     │
│                                  CreateMcpClientAsync()                     │
│                                       │                                     │
│                                       ├─> CreateBearerTokenTransport()     │
│                                       │       │                             │
│                                       │       ├─> 创建 HttpClient            │
│                                       │       │                             │
│                                       │       ├─> ✅ InjectUserContextToHttpClient() │
│                                       │       │   ⚡ 从 AsyncLocal 读取用户信息  │
│                                       │       │   httpClient.Headers.Add(    │
│                                       │       │     "X-User-Id", "user-123"  │
│                                       │       │   )                          │
│                                       │       │   httpClient.Headers.Add(    │
│                                       │       │     "X-User-Email",          │
│                                       │       │     "user-123@example.com"   │
│                                       │       │   )                          │
│                                       │       │                             │
│                                       │       └─> HttpClientTransport       │
│                                       │                                     │
│                                       └─> McpClient.CreateAsync()           │
│                                               │                             │
│                                               └─> ListToolsAsync()          │
│                                                   返回: [                    │
│                                                     generate_image,          │
│                                                     play_random_music        │
│                                                   ]                          │
│                                                                             │
│       ▼                                                                     │
│  创建 Handoff Workflow                                                      │
│       │                                                                     │
│       ├─> AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)       │
│       ├─> .WithHandoffs(triageAgent, specialistAgents)                     │
│       ├─> .WithHandoffs(specialistAgents, triageAgent)                     │
│       └─> .Build()                                                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 3️⃣ 执行 Workflow (Agent Framework InProcessExecution)                       │
│                                                                             │
│  InProcessExecution.StreamAsync(workflow, messages)                         │
│       │                                                                     │
│       ├─> run.TrySendMessageAsync(new TurnToken())                         │
│       │                                                                     │
│       └─> await foreach (var evt in run.WatchStreamAsync())                │
│               │                                                             │
│               ├─> [Event] AgentRunUpdateEvent (Triage Agent)               │
│               │   - Triage 分析消息: "生成一张猫咪图片"                       │
│               │   - 关键词匹配: "图片" → 需要 generate_image 工具           │
│               │   - 决策: 调用 handoff 转交给 "艺术家梅"                     │
│               │                                                             │
│               ├─> [Event] AgentRunUpdateEvent (艺术家梅)                   │
│               │   - 接手任务                                                │
│               │   - 决定调用 generate_image 工具                            │
│               │                                                             │
│               ├─> [Event] FunctionCallContent                              │
│               │   {                                                         │
│               │     "name": "generate_image",                               │
│               │     "arguments": {                                          │
│               │       "prompt": "cute cat illustration"                     │
│               │     }                                                       │
│               │   }                                                         │
│               │                                                             │
│               └─> FunctionInvokingChatClient 自动执行工具调用               │
│                       │                                                     │
│                       ▼                                                     │
│                  调用 MCP Server (generate_image)                           │
│                       │                                                     │
│                       ├─> HttpClient.PostAsync()                           │
│                       │   URL: https://mcp-server.com/tools/generate_image │
│                       │   Headers:                                          │
│                       │     - Authorization: Bearer xxx                     │
│                       │     - X-User-Id: user-123          ✅ 已注入        │
│                       │     - X-User-Email: user-123@example.com ✅ 已注入  │
│                       │   Body: { prompt: "cute cat..." }                   │
│                       │                                                     │
│                       ▼                                                     │
│                  外部 MCP Server 处理                                        │
│                       │                                                     │
│                       ├─> 验证 Bearer Token                                 │
│                       ├─> 提取 X-User-Id ✅                                 │
│                       ├─> 检查用户配额 (基于 X-User-Id)                      │
│                       ├─> 生成图片 (Azure OpenAI DALL-E)                    │
│                       ├─> 保存图片到存储                                     │
│                       └─> 返回结果                                           │
│                           {                                                 │
│                             "imageUrl": "https://cdn.com/cat.png"           │
│                           }                                                 │
│                       │                                                     │
│                       ▼                                                     │
│               [Event] FunctionResultContent                                │
│                   - 工具调用成功                                             │
│                   - 结果: { imageUrl: "..." }                               │
│                                                                             │
│               ▼                                                             │
│               [Event] AgentRunUpdateEvent (艺术家梅继续)                    │
│                   - 艺术家梅生成最终回复                                      │
│                   - Content: "我给你生成了一张可爱的猫咪图片～快看看吧！"      │
│                                                                             │
│       返回: AgentResponse {                                                 │
│           agentId: "agent-yishujiamei",                                     │
│           agentName: "艺术家梅",                                             │
│           content: "我给你生成了一张可爱的猫咪图片～快看看吧！",               │
│           toolCalls: [                                                      │
│               {                                                             │
│                   toolName: "generate_image",                               │
│                   parameters: { imageUrl: "https://cdn.com/cat.png" }       │
│               }                                                             │
│           ],                                                                │
│           metadata: { avatar: "...", personality: "..." }                   │
│       }                                                                     │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 4️⃣ 保存回复并推送给用户 (ChatMessageBackgroundJob 继续)                     │
│                                                                             │
│  ChatMessageBackgroundJob (继续执行)                                        │
│       │                                                                     │
│       ├─> 保存 Agent 回复到数据库                                            │
│       │   ChatMessage {                                                     │
│       │       senderId: "agent-yishujiamei",                                │
│       │       isAgent: true,                                                │
│       │       content: "我给你生成了...",                                    │
│       │       metadata: { avatar, personality }                             │
│       │   }                                                                 │
│       │                                                                     │
│       ├─> 处理 ToolCalls (提取附件)                                          │
│       │   attachments = [                                                   │
│       │       { type: "image", url: "https://cdn.com/cat.png" }             │
│       │   ]                                                                 │
│       │                                                                     │
│       └─> 通过 SignalR 推送给用户设备                                         │
│               │                                                             │
│               ├─> 1️⃣ 推送通知消息                                            │
│               │   DevicePushService.SendCustomMessageAsync(                │
│               │       userId: "user-123",                                   │
│               │       message: {                                            │
│               │           action: "notification",                           │
│               │           title: "新消息",                                   │
│               │           content: "艺术家梅: 我给你生成了...",               │
│               │           emotion: "happy"                                  │
│               │       }                                                     │
│               │   )                                                         │
│               │                                                             │
│               └─> 2️⃣ 推送群聊消息                                            │
│                   DevicePushService.SendCustomMessageAsync(                │
│                       userId: "user-123",                                   │
│                       message: {                                            │
│                           action: "group_chat",                             │
│                           roomId: "...",                                    │
│                           roomName: "日常交流群",                            │
│                           senderId: "agent-yishujiamei",                    │
│                           senderName: "艺术家梅",                            │
│                           content: "我给你生成了...",                        │
│                           isAgent: true,                                    │
│                           attachments: [                                    │
│                               {                                             │
│                                   type: "image",                            │
│                                   url: "https://cdn.com/cat.png"            │
│                               }                                             │
│                           ]                                                 │
│                       }                                                     │
│                   )                                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 5️⃣ 用户设备接收消息 (SignalR WebSocket)                                     │
│                                                                             │
│  DeviceHub.Clients.Group("Users:user-123")                                 │
│       │                                                                     │
│       └─> SendAsync("CustomMessage", jsonMessage)                          │
│               │                                                             │
│               ▼                                                             │
│  ESP32 设备 / Web 客户端                                                     │
│       │                                                                     │
│       ├─> onCustomMessage(jsonString) 回调                                  │
│       │                                                                     │
│       ├─> 解析 JSON                                                         │
│       │   - action: "group_chat"                                            │
│       │   - senderName: "艺术家梅"                                           │
│       │   - content: "我给你生成了..."                                       │
│       │   - attachments: [{ type: "image", url: "..." }]                   │
│       │                                                                     │
│       └─> 渲染 UI                                                           │
│           - 显示消息气泡                                                      │
│           - 显示图片预览                                                      │
│           - 播放通知音效                                                      │
│                                                                             │
│  用户看到回复和生成的图片 ✅                                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔑 关键技术点

### 1. AsyncLocal 用户上下文传递

```csharp
// Step 1: 在 AgentOrchestrationService 中设置
UserContext.Current = new UserContext {
    UserId = "user-123",
    UserEmail = "user-123@example.com"
};

// ⚡ AsyncLocal 自动在整个异步调用链中传递

// Step 2: 在 McpToolService 中读取并注入到 HttpClient
var userId = UserContext.Current?.UserId;
httpClient.DefaultRequestHeaders.Add("X-User-Id", userId);
```

**优势**:
- ✅ 无需修改所有方法签名
- ✅ 自动跨越 await 边界传递
- ✅ 线程安全

### 2. Workflow 缓存机制

```csharp
// 按 ChatRoom 级别缓存 Workflow
private readonly Dictionary<Guid, Workflow> _workflowCache = new();

// 首次创建后缓存，后续请求直接使用
public async Task<Workflow> GetOrCreateWorkflowAsync(Guid chatRoomId)
{
    if (_workflowCache.TryGetValue(chatRoomId, out var cachedWorkflow))
        return cachedWorkflow;
    
    var workflow = await CreateWorkflowAsync(chatRoomId);
    _workflowCache[chatRoomId] = workflow;
    return workflow;
}
```

**好处**:
- ⚡ 减少重复创建开销
- ⚡ 提升响应速度
- ⚡ 降低 MCP Server 连接频率

### 3. Handoff Workflow 模式

```csharp
// Triage Agent → Specialist Agents (路由)
AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, specialistAgents)  // 单向路由
    .WithHandoffs(specialistAgents, triageAgent)  // 允许回退重新路由
    .Build();
```

**优势**:
- ✅ 官方推荐模式
- ✅ 智能路由到最合适的专家
- ✅ 支持上下文连贯对话

### 4. 工具调用自动执行

```csharp
// ✅ ChatClientAgent 自动注入 FunctionInvokingChatClient
var chatAgent = new ChatClientAgent(
    _chatClient,           // 原始 IChatClient
    instructions,
    tools: mcpTools        // 传入 MCP 工具
);

// FunctionInvokingChatClient 自动：
// 1. 检测工具调用请求
// 2. 执行工具（调用 MCP Server）
// 3. 将结果返回给 Agent
// 4. Agent 继续生成最终回复
```

---

## 📊 性能指标

| 阶段 | 耗时 | 说明 |
|------|------|------|
| 1. MCP Tool 调用接收 | < 50ms | AiGroupChatTool 保存消息 |
| 2. Hangfire 任务调度 | < 100ms | 后台任务入队 |
| 3. Workflow 创建（首次） | 1-2s | 连接 MCP Servers + 创建 Workflow |
| 3. Workflow 创建（缓存） | < 50ms | 直接从缓存获取 |
| 4. GPT 推理 | 2-5s | Azure OpenAI API 响应时间 |
| 5. MCP 工具调用 | 5-15s | 图像生成：10-15s<br>音乐选择：1-2s |
| 6. SignalR 推送 | < 100ms | 实时推送到设备 |
| **总计（冷启动）** | **8-23s** | 取决于工具类型 |
| **总计（热路径）** | **7-20s** | 使用 Workflow 缓存 |

---

## 🎯 调用链路总结

**核心流程**:
1. 用户消息 → MCP Tool → 保存 + 入队后台任务
2. 后台任务 → 设置 UserContext → 创建/获取 Workflow
3. Workflow 执行 → Triage 路由 → Specialist 响应 + 调用 MCP 工具
4. MCP 工具调用 → 注入用户信息 → 外部 MCP Server 处理
5. 保存 Agent 回复 → SignalR 推送 → 用户设备接收

**关键创新**:
- ✅ **AsyncLocal 用户上下文** - 解决跨服务用户信息传递问题
- ✅ **Workflow 缓存** - 优化性能，减少重复创建
- ✅ **Handoff 智能路由** - 自动选择最合适的专家
- ✅ **工具自动执行** - FunctionInvokingChatClient 自动处理工具调用

---

## 📚 相关文档

- [agents.md](../agents.md) - 项目架构总览
- [MCP_USER_CONTEXT_FORWARDING.md](MCP_USER_CONTEXT_FORWARDING.md) - 用户上下文转发详细文档
- [AI_GROUP_CHAT_GUIDE.md](AI_GROUP_CHAT_GUIDE.md) - AI 群组聊天完整指南
- [AGENT_FRAMEWORK_INTEGRATION.md](AGENT_FRAMEWORK_INTEGRATION.md) - Agent Framework 整合文档
