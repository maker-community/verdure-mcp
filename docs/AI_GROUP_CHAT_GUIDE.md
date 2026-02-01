# AI 群组交流 MCP 工具使用指南

## 概述

AI 群组交流功能允许用户通过 MCP 工具与多个智能体进行协作对话。每个智能体都有独特的性格和专长，能够为用户提供不同风格的回复和帮助。

## 功能特性

### ✅ 已实现功能

1. **群组管理**
   - 列出已加入的群组
   - 加入新群组
   - 设置默认对话群组

2. **智能体交互**
   - 发送消息到群组
   - 智能体自动选择响应（基于消息内容和能力匹配）
   - 支持多种个性化回复风格
   - 智能体可调用工具（生图、音乐）

3. **会话管理**
   - 消息持久化存储
   - 历史消息查询
   - 按用户ID关联会话

4. **实时推送**
   - 通过 SignalR 推送智能体回复
   - 支持图文混合消息

## 智能体介绍

系统预设了 6 位具有不同性格的 AI 智能体：

### 1. 小甜甜 (agent-xiaotiantian)
- **性格**: 甜美可爱，温柔体贴
- **擅长**: 情感支持、倾听、闲聊
- **特点**: 说话温柔甜美，善于给予温暖和鼓励

### 2. 御姐雅 (agent-yujieya)
- **性格**: 成熟稳重，知性优雅
- **擅长**: 深度对话、人生建议、理性分析
- **特点**: 沉稳优雅，给出理性而中肯的建议

### 3. 才女琳 (agent-cainvlin)
- **性格**: 博学多才，逻辑清晰
- **擅长**: 知识问答、学习辅导、信息查询
- **特点**: 知识渊博，善于解释复杂概念

### 4. 艺术家梅 (agent-yishujia mei)
- **性格**: 富有创意，感性浪漫
- **擅长**: 创意启发、艺术鉴赏、**生图**
- **特点**: 充满想象力，能够提供创意和设计建议
- **工具能力**: 可以生成图片

### 5. 音乐家莉 (agent-yinyuejiali)
- **性格**: 文艺浪漫，感性细腻
- **擅长**: 音乐推荐、情感表达、**音乐播放**
- **特点**: 善于用音乐表达情感
- **工具能力**: 可以播放音乐

### 6. 活泼妹 (agent-huopo)
- **性格**: 活泼开朗，幽默风趣
- **擅长**: 闲聊、讲笑话、活跃气氛
- **特点**: 充满活力和正能量，让对话变得有趣

## MCP 工具使用

### 工具名称
`chat_with_group`

### 参数说明

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `message` | string | 否* | - | 发送的消息内容（action=send 时必需） |
| `roomId` | string | 否 | 默认群组 | 群组 ID（GUID 格式） |
| `action` | string | 否 | `send` | 操作类型 |
| `limit` | number | 否 | 3 | 每页数量（list_rooms 和 get_history 使用） |

*注：当 action=send 时，message 为必填参数

### 操作类型 (action)

#### 1. send - 发送消息
发送消息到群组，智能体将自动响应。

**示例请求**:
```json
{
  "message": "今天心情不太好，有谁能陪我聊聊天吗？",
  "action": "send"
}
```

**成功响应**:
```json
{
  "success": true,
  "message": "消息已发送，智能体正在处理中...",
  "data": {
    "messageId": "uuid",
    "chatRoomId": "uuid",
    "jobId": "hangfire-job-id",
    "status": "processing"
  }
}
```

**SignalR 推送格式**:
```json
{
  "action": "group_chat",
  "roomId": "uuid",
  "roomName": "日常交流群",
  "message": {
    "id": "uuid",
    "senderId": "agent-xiaotiantian",
    "senderName": "小甜甜",
    "content": "哎呀，听到你说心情不好，我好心疼呀～别难过啦，我会一直陪着你的！",
    "isAgent": true,
    "timestamp": "2026-02-01T10:30:00Z",
    "attachments": null,
    "metadata": {
      "avatar": "https://api.dicebear.com/7.x/avataaars/svg?seed=xiaotiantian",
      "personality": "甜美可爱，温柔体贴"
    }
  }
}
```

#### 2. list_rooms - 列出群组
查看用户已加入的群组列表。

**示例请求**:
```json
{
  "action": "list_rooms",
  "limit": 5
}
```

**成功响应**:
```json
{
  "success": true,
  "message": "找到 1 个群组",
  "data": {
    "rooms": [
      {
        "id": "uuid",
        "name": "日常交流群",
        "description": "与多位AI美女智能体进行轻松愉快的日常交流",
        "avatarUrl": "https://api.dicebear.com/7.x/bottts/svg?seed=dailychat",
        "isDefault": true,
        "joinedAt": "2026-02-01T10:00:00Z",
        "agentCount": 6
      }
    ]
  }
}
```

#### 3. join - 加入群组
加入指定的群组。

**示例请求**:
```json
{
  "action": "join",
  "roomId": "group-uuid"
}
```

**成功响应**:
```json
{
  "success": true,
  "message": "成功加入群组: 日常交流群",
  "data": {
    "roomId": "uuid",
    "roomName": "日常交流群",
    "isDefault": false
  }
}
```

#### 4. set_default - 设置默认群组
将指定群组设置为默认群组。

**示例请求**:
```json
{
  "action": "set_default",
  "roomId": "group-uuid"
}
```

**成功响应**:
```json
{
  "success": true,
  "message": "已将 '日常交流群' 设置为默认群组",
  "data": {
    "roomId": "uuid",
    "roomName": "日常交流群"
  }
}
```

#### 5. get_history - 获取历史消息
查询群组的历史消息。

**示例请求**:
```json
{
  "action": "get_history",
  "roomId": "group-uuid",
  "limit": 20
}
```

**成功响应**:
```json
{
  "success": true,
  "message": "找到 15 条历史消息",
  "data": {
    "chatRoomId": "uuid",
    "messages": [
      {
        "id": "uuid",
        "senderId": "user-123",
        "isAgent": false,
        "content": "大家好！",
        "metadata": null,
        "createdAt": "2026-02-01T10:00:00Z"
      },
      {
        "id": "uuid",
        "senderId": "agent-xiaotiantian",
        "isAgent": true,
        "content": "你好呀～欢迎来到我们的群组！",
        "metadata": "{\"avatar\":\"...\",\"personality\":\"...\"}",
        "createdAt": "2026-02-01T10:00:05Z"
      }
    ]
  }
}
```

## 使用场景示例

### 场景 1: 情感倾诉
```json
{
  "message": "今天工作压力好大，感觉快要崩溃了...",
  "action": "send"
}
```
**预期响应**: 小甜甜或活泼妹会回复，提供情感支持和鼓励。

### 场景 2: 知识问答
```json
{
  "message": "能帮我解释一下什么是区块链吗？",
  "action": "send"
}
```
**预期响应**: 才女琳会回复，提供专业的知识解答。

### 场景 3: 创意生图
```json
{
  "message": "我想要一张温馨的咖啡店画面",
  "action": "send"
}
```
**预期响应**: 艺术家梅会回复，并触发图片生成工具。
SignalR 推送会包含生成的图片 URL。

### 场景 4: 音乐推荐
```json
{
  "message": "推荐一些放松的音乐给我听吧",
  "action": "send"
}
```
**预期响应**: 音乐家莉会回复，并触发音乐播放工具。

## 智能体选择机制

系统会根据以下规则自动选择最合适的智能体响应：

1. **能力匹配**: 如果消息中包含特定关键词（如"图"、"画"、"音乐"、"歌"等），会优先选择具有相应能力的智能体。

2. **轮询机制**: 如果没有明确的能力匹配，系统会使用轮询方式，让所有智能体都有机会响应。

3. **个性化响应**: 每个智能体会根据自己的性格特点生成独特的回复风格。

## 认证要求

### 必需的请求头
```
X-User-Id: your-user-id
```

所有操作都需要在请求头中包含 `X-User-Id`，用于标识用户身份。

### MCP 端点
```
POST /mcp/all
POST /mcp
```

## 错误处理

### 常见错误

#### 1. 缺少用户 ID
```json
{
  "success": false,
  "message": "用户 ID 未提供。请确保 X-User-Id 请求头存在。"
}
```

#### 2. 未加入群组
```json
{
  "success": false,
  "message": "未找到默认群组。请先加入一个群组或指定群组 ID。"
}
```

#### 3. 无效的群组 ID
```json
{
  "success": false,
  "message": "无效的群组 ID 格式"
}
```

#### 4. 不是群组成员
```json
{
  "success": false,
  "message": "您不是该群组的成员"
}
```

## 数据库架构

### 表结构

#### chat_rooms - 群组表
```sql
CREATE TABLE chat_rooms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000),
    avatar_url VARCHAR(500),
    agent_ids JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP
);
```

#### chat_messages - 消息表
```sql
CREATE TABLE chat_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chat_room_id UUID NOT NULL REFERENCES chat_rooms(id),
    sender_id VARCHAR(255) NOT NULL,
    is_agent BOOLEAN NOT NULL,
    content VARCHAR(4000) NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

#### user_chat_room_memberships - 用户群组关系表
```sql
CREATE TABLE user_chat_room_memberships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id VARCHAR(255) NOT NULL,
    chat_room_id UUID NOT NULL REFERENCES chat_rooms(id),
    is_default BOOLEAN DEFAULT FALSE,
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, chat_room_id)
);
```

#### agent_profiles - 智能体配置表
```sql
CREATE TABLE agent_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id VARCHAR(100) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    avatar VARCHAR(500),
    personality VARCHAR(500) NOT NULL,
    system_prompt VARCHAR(2000) NOT NULL,
    capabilities JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## 开发环境初始化

在开发环境中，系统会自动初始化：
- 1 个默认群组："日常交流群"
- 6 个智能体配置（小甜甜、御姐雅、才女琳、艺术家梅、音乐家莉、活泼妹）

用户第一次加入群组时，该群组会自动设置为默认群组。

## 技术实现说明

### 异步处理流程
1. 用户发送消息 → MCP 工具接收
2. 消息保存到数据库
3. 创建 Hangfire 后台任务
4. 立即返回响应给客户端（状态：processing）
5. 后台任务处理：
   - 选择合适的智能体
   - 生成回复
   - 处理工具调用（如有）
   - 保存智能体回复
   - 通过 SignalR 推送给用户

### 工具调用支持
智能体可以在回复中触发工具调用：
- **生图**: 在回复中使用 `[生图:描述]` 格式
- **音乐**: 在回复中使用 `[音乐]` 标记

## 未来增强

以下功能可在后续版本中实现：

1. **完整 Agent Framework 集成**
   - 使用 Microsoft.AI.Agents 进行智能体编排
   - 支持更复杂的 Hand-Off 策略
   - 实现真正的 GroupChat 机制

2. **Azure OpenAI 集成**
   - 使用 GPT-4 生成更智能的回复
   - 基于上下文的对话理解
   - 动态调整智能体响应

3. **更多智能体能力**
   - 网络搜索
   - 文件处理
   - 日程管理
   - 邮件发送

4. **群组管理功能**
   - 创建自定义群组
   - 邀请其他用户
   - 群组权限管理
   - 群组设置配置

5. **消息增强**
   - 消息编辑和撤回
   - 消息引用和回复
   - 表情反应
   - 文件附件

## 故障排查

### 问题: 智能体没有响应
**可能原因**:
- Hangfire 后台任务未运行
- SignalR 连接断开
- 数据库连接问题

**解决方法**:
1. 检查 Hangfire Dashboard (`/hangfire`)
2. 检查 SignalR 连接状态
3. 查看应用程序日志

### 问题: 收到的是错误的智能体回复
**可能原因**:
- 智能体选择逻辑匹配错误

**解决方法**:
- 在消息中明确提及想要的功能（如"帮我生成一张图片"）
- 查看日志确认选择的智能体

### 问题: 图片生成失败
**可能原因**:
- Azure OpenAI API 配置错误
- 提示词不符合内容策略
- API 配额不足

**解决方法**:
1. 检查 Azure OpenAI 配置
2. 调整提示词内容
3. 查看 API 使用配额

## 支持与反馈

如有问题或建议，请：
1. 查看应用程序日志：`/var/log/verdure-mcp/`
2. 访问 Hangfire Dashboard：`/hangfire`
3. 提交 GitHub Issue：https://github.com/maker-community/verdure-mcp/issues

## 版本历史

### v1.0.0 (2026-02-01)
- ✅ 初始版本发布
- ✅ 基础群组聊天功能
- ✅ 6 个预设智能体
- ✅ 消息持久化
- ✅ SignalR 实时推送
- ✅ 简化的智能体选择机制
- ✅ 工具调用支持（生图、音乐）
