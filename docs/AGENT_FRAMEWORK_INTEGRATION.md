# Agent Framework Handoff 模式整合文档

## 概述

本项目已成功整合 **Microsoft Agent Framework** 的 **Handoff 模式**，实现了真正的多智能体协作对话系统。

## 架构变更

### 之前（假的实现）
```
AgentOrchestrationService
  └── SelectAgent (简单轮询/关键词匹配)
       └── GenerateResponse (预定义模板)
```

### 之后（Agent Framework Handoff）
```
AgentOrchestrationService
  └── WorkflowManager
       ├── GetOrCreateWorkflow(chatRoomId)
       │    ├── 从数据库加载 AgentProfiles
       │    ├── 创建 Triage Agent (智能路由器)
       │    ├── 创建 Specialist Agents (专家智能体)
       │    └── 构建 Handoff Workflow
       │
       └── InProcessExecution.StreamAsync
            ├── Triage Agent 分析消息
            ├── 调用 handoff 函数转交
            ├── Specialist Agent 生成回复
            └── 返回结果
```

## 核心组件

### 1. WorkflowManager
**文件**: `src/Verdure.Mcp.Server/Services/WorkflowManager.cs`

**职责**:
- 管理 Handoff Workflows 的创建和缓存
- 从数据库动态加载智能体配置
- 生成 Triage Agent 的动态提示词
- 配置智能体之间的 Handoff 路径

**关键方法**:
```csharp
public async Task<Workflow> GetOrCreateWorkflowAsync(Guid chatRoomId, CancellationToken cancellationToken = default)
public void ClearWorkflowCache(Guid chatRoomId)
public void ClearAllWorkflowCache()
```

### 2. AgentOrchestrationService (重构版)
**文件**: `src/Verdure.Mcp.Server/Services/AgentOrchestrationService.cs`

**职责**:
- 处理用户消息并执行 Workflow
- 追踪多个 Agent 的执行过程
- 提取最终响应并保存到数据库

**核心流程**:
1. 获取聊天历史（上下文）
2. 从 WorkflowManager 获取 Workflow
3. 执行 Workflow（流式）
4. 处理 WorkflowEvent 事件流
5. 提取 Agent 响应（过滤 triage agent）
6. 返回最终结果

### 3. IChatClient 配置
**文件**: `src/Verdure.Mcp.Server/Program.cs`

**配置**:
```csharp
builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(sp =>
{
    var azureOpenAISettings = configuration.GetSection(AzureOpenAISettings.SectionName).Get<AzureOpenAISettings>();
    
    var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(
        new Uri(azureOpenAISettings.Endpoint),
        new Azure.AzureKeyCredential(azureOpenAISettings.ApiKey));
    
    return azureClient.AsChatClient(azureOpenAISettings.DeploymentName);
});
```

## Handoff 工作原理

### 1. Triage Agent（智能路由器）
- **作用**: 分析用户消息，决定由哪个专家智能体回复
- **特点**: 完全透明，不生成文本回复，只调用 `handoff` 函数
- **提示词**: 动态生成，包含所有可用专家的信息

**路由策略**:
1. 话题内容匹配
2. 关键词识别（生图、音乐等能力）
3. 语气风格匹配
4. 上下文连贯性（继续与同一专家对话）
5. 隐式意图理解

### 2. Specialist Agents（专家智能体）
- **数据源**: 从数据库 `AgentProfiles` 表加载
- **配置**: 每个 Agent 有独立的 SystemPrompt 和 Personality
- **能力**: 可以定义 Capabilities（如"生图"、"音乐"、"闲聊"）

### 3. Handoff 路径
```
Triage ─→ Specialist A
   ↓         ↓
   └────────┘
      (双向)
```

- Triage 可以转给任何 Specialist
- Specialist 可以转回 Triage（重新路由）

## 配置要求

### 1. NuGet 包
已添加到 `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.Agents.AI" Version="1.0.0-beta.25141.1" />
<PackageVersion Include="Microsoft.Extensions.AI" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.AI.OpenAI" Version="10.0.0" />
```

### 2. Azure OpenAI 配置
在 `appsettings.json` 或环境变量中配置:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

**推荐部署**:
- `gpt-4o-mini`: 性价比高，适合日常对话
- `gpt-4o`: 更强大，适合复杂推理
- `gpt-4-turbo`: 平衡性能与成本

### 3. 数据库准备
确保 `AgentProfiles` 表已初始化:
```bash
dotnet ef database update --project src/Verdure.Mcp.Server
```

## 使用流程

### 用户发送消息
1. 用户通过 MCP Tool `chat_with_group` 发送消息
2. 消息保存到 `ChatMessages` 表
3. 触发 Hangfire 后台任务 `ChatMessageBackgroundJob`

### 后台处理
1. `ChatMessageBackgroundJob.ProcessChatMessageAsync` 执行
2. 调用 `AgentOrchestrationService.ProcessMessageAsync`
3. `AgentOrchestrationService` 调用 `WorkflowManager.GetOrCreateWorkflowAsync`
4. 执行 Workflow:
   - Triage Agent 分析消息
   - 调用 `handoff` 转交给 Specialist
   - Specialist 生成回复
5. 保存 Agent 回复到数据库
6. 通过 SignalR 推送到用户设备

### 用户接收回复
1. ESP32 设备通过 SignalR 接收 `CustomMessage`
2. 消息格式:
```json
{
  "action": "group_chat",
  "roomId": "...",
  "roomName": "日常交流群",
  "senderId": "agent-xiaotiantian",
  "senderName": "小甜甜",
  "content": "哎呀，听到你说...",
  "isAgent": true,
  "metadata": {
    "avatar": "...",
    "personality": "甜美可爱"
  }
}
```

## 性能优化

### Workflow 缓存
- **级别**: ChatRoom 级别
- **位置**: `WorkflowManager._workflowCache`
- **优势**: 避免每次对话都重新创建 Workflow
- **清除**: 当 AgentProfiles 更新时调用 `ClearWorkflowCache(chatRoomId)`

### 日志级别
调试时建议设置:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Agents.AI": "Debug",
      "Verdure.Mcp.Server.Services.WorkflowManager": "Debug",
      "Verdure.Mcp.Server.Services.AgentOrchestrationService": "Debug"
    }
  }
}
```

## 扩展智能体

### 添加新智能体
只需在数据库中添加 `AgentProfile` 记录:
```sql
INSERT INTO "AgentProfiles" ("Id", "AgentId", "Name", "Avatar", "Personality", "SystemPrompt", "Capabilities", "CreatedAt")
VALUES (
    gen_random_uuid(),
    'agent-newagent',
    '新智能体',
    '🤖',
    '智能、友好、专业',
    '你是一个新的AI智能体...',
    ARRAY['特定能力'],
    NOW()
);

-- 将智能体添加到群组
UPDATE "ChatRooms" 
SET "AgentIds" = array_append("AgentIds", 'agent-newagent')
WHERE "Name" = '日常交流群';
```

### 更新智能体配置
更新后需清除 Workflow 缓存:
```csharp
_workflowManager.ClearWorkflowCache(chatRoomId);
// 或清除所有
_workflowManager.ClearAllWorkflowCache();
```

## 故障排查

### 常见问题

#### 1. "Azure OpenAI settings not configured properly"
**原因**: 缺少 Azure OpenAI 配置
**解决**: 配置 `appsettings.json` 中的 `AzureOpenAI` 节点

#### 2. "No agents found for chat room"
**原因**: ChatRoom 的 `AgentIds` 数组为空或智能体未启用
**解决**: 检查数据库，确保 AgentProfiles 存在且关联到 ChatRoom

#### 3. "No agent response generated from workflow"
**原因**: Triage Agent 没有调用 handoff，或所有 Specialist 都没有响应
**解决**: 检查日志，查看 Workflow 执行过程

#### 4. Agent 选择不准确
**原因**: Triage Agent 的提示词需要优化
**解决**: 修改 `WorkflowManager.GenerateTriageInstructions` 方法

### 调试建议
1. 启用 Debug 日志查看 Workflow 执行过程
2. 检查 `AgentRunUpdateEvent` 确认 Agent 切换
3. 监控 `WorkflowOutputEvent` 确认 Workflow 完成
4. 查看数据库确认消息已保存

## 与参考项目的对比

### 相似点
- ✅ 使用 `AgentWorkflowBuilder` 创建 Handoff Workflow
- ✅ Triage Agent 动态生成提示词
- ✅ Specialist Agents 从数据库加载
- ✅ Workflow 按组（ChatRoom）缓存
- ✅ 使用 `InProcessExecution.StreamAsync` 执行
- ✅ 处理 `AgentRunUpdateEvent` 追踪 Agent 切换

### 差异点
- 🔄 **存储**: 使用 PostgreSQL 而非 LiteDB
- 🔄 **推送**: 使用 SignalR 而非 HTTP 轮询
- 🔄 **工具**: 集成 MCP 工具（图像生成、音乐等）
- 🔄 **架构**: 与现有的 Hangfire 后台任务集成

## 后续增强建议

### 1. 工具集成
为 Specialist Agents 添加 MCP 工具:
```csharp
var specialistAgents = agents.Select(agent =>
{
    var tools = GetToolsForAgent(agent.Capabilities); // 根据能力获取工具
    return new ChatClientAgent(
        _chatClient,
        instructions: agent.SystemPrompt,
        name: agent.AgentId,
        description: agent.Personality,
        tools: tools); // 添加工具
}).ToList();
```

### 2. 多轮对话优化
- 增加更多历史消息上下文
- 实现对话摘要功能
- 优化 Token 使用

### 3. Agent 能力动态扩展
- 支持运行时添加新工具
- 支持 Agent 自定义函数
- 支持多模态输入（图片、语音）

### 4. 监控与分析
- 统计每个 Agent 的响应次数
- 分析用户偏好（最常用的 Agent）
- 监控 Workflow 执行耗时

## 总结

本次整合成功将 **Microsoft Agent Framework 的 Handoff 模式**完整集成到 Verdure MCP 项目中，实现了：

✅ **真正的 AI 智能路由** - Triage Agent 自动选择最合适的专家
✅ **动态 Agent 配置** - 从数据库加载，支持运行时更新
✅ **完整的 Workflow 执行** - 使用官方 Agent Framework API
✅ **性能优化** - Workflow 缓存，避免重复创建
✅ **无缝集成** - 与现有 SignalR、Hangfire、MCP 工具完美配合

现在系统不再使用假的预定义响应，而是真正通过 Azure OpenAI GPT 模型和 Agent Framework 实现智能对话！
