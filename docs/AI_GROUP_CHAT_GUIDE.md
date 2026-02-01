# AI Group Chat Guide

## Overview

Verdure MCP now includes an AI Group Chat feature that allows users to interact with multiple AI agents in a collaborative conversation environment. Each agent has its own personality, capabilities, and expertise.

## Architecture

### Core Components

1. **AgentOrchestrationService** - Manages agent selection and response generation
2. **ChatRoomSeeder** - Initializes default chat rooms and agent profiles
3. **ChatMessageBackgroundJob** - Processes messages asynchronously
4. **AiGroupChatTool** - MCP tool for chat interactions

### Data Models

- **ChatRoom** - Represents a chat room/group
- **ChatMessage** - Messages from users and agents
- **UserChatRoomMembership** - User membership in chat rooms
- **AgentProfile** - AI agent configurations

## Available Agents

The system seeds 5 default agents on first startup:

### 小甜甜 (Xiao Tian Tian)
- **Personality**: Sweet and caring
- **Capabilities**: Casual chat, emotional support
- **Best for**: Friendly conversation, encouragement

### 御姐雅 (Yu Jie Ya)
- **Personality**: Mature and intellectual
- **Capabilities**: Deep conversation, rational analysis
- **Best for**: Thoughtful discussions, advice

### 才女琳 (Cai Nv Lin)
- **Personality**: Knowledgeable scholar
- **Capabilities**: Knowledge Q&A, learning assistance
- **Best for**: Educational questions, factual information

### 艺术家梅 (Yi Shu Jia Mei)
- **Personality**: Creative artist
- **Capabilities**: Image generation, creative design
- **Best for**: Visual content requests, creative brainstorming

### 音乐家莉 (Yin Yue Jia Li)
- **Personality**: Elegant musician
- **Capabilities**: Music playback, art appreciation
- **Best for**: Music recommendations, artistic discussion

## Using the Chat Feature

### MCP Tool: `chat_with_group`

The `chat_with_group` tool supports multiple actions:

#### 1. Send Message (default)

```json
{
  "action": "send",
  "message": "你好,今天天气怎么样？"
}
```

**Response:**
```json
{
  "success": true,
  "message": "消息已发送,AI智能体正在处理中...",
  "data": {
    "messageId": "uuid",
    "roomId": "uuid",
    "timestamp": "2026-02-01T12:00:00Z"
  }
}
```

The agent response will be pushed to your device via SignalR.

#### 2. List Available Rooms

```json
{
  "action": "list_rooms"
}
```

**Response:**
```json
{
  "success": true,
  "message": "找到 3 个聊天群组",
  "data": [
    {
      "id": "uuid",
      "name": "日常交流群",
      "description": "与多位AI智能体进行日常交流...",
      "avatarUrl": "https://...",
      "isMember": true
    }
  ]
}
```

#### 3. Join a Room

```json
{
  "action": "join",
  "roomId": "uuid"
}
```

#### 4. Set Default Room

```json
{
  "action": "set_default",
  "roomId": "uuid"
}
```

#### 5. Get Message History

```json
{
  "action": "get_history",
  "roomId": "uuid"  // optional, uses default room if not specified
}
```

**Response:**
```json
{
  "success": true,
  "message": "获取到 20 条历史消息",
  "data": {
    "roomId": "uuid",
    "messages": [
      {
        "id": "uuid",
        "senderId": "user-123",
        "isAgent": false,
        "content": "你好",
        "timestamp": "2026-02-01T12:00:00Z"
      },
      {
        "id": "uuid",
        "senderId": "agent-xiaotiantian",
        "isAgent": true,
        "content": "[小甜甜] 你好呀～",
        "timestamp": "2026-02-01T12:00:01Z"
      }
    ]
  }
}
```

## SignalR Message Format

When agents respond, messages are pushed via SignalR's `CustomMessage` event:

```json
{
  "action": "group_chat",
  "roomId": "uuid",
  "roomName": "日常交流群",
  "message": {
    "id": "uuid",
    "senderId": "agent-xiaotiantian",
    "senderName": "小甜甜",
    "content": "[小甜甜] 我理解了您的消息...",
    "isAgent": true,
    "timestamp": "2026-02-01T12:00:01Z",
    "attachments": []
  }
}
```

## Agent Selection Logic

The system uses intelligent agent selection:

1. **Capability Matching** - If the user message mentions a specific capability (图/画 for images, 音乐/歌 for music), the appropriate agent is selected
2. **Round-Robin** - Otherwise, agents take turns responding to maintain variety
3. **Fallback** - If no match is found, the first agent responds

## MCP Endpoint

Access the chat tool via:

```
POST /chat/mcp
Authorization: Bearer YOUR_TOKEN
X-User-Id: user-123
```

Or use the `all` category endpoint:

```
POST /all/mcp
```

## Database Schema

### chat_rooms
```sql
id UUID PRIMARY KEY
name VARCHAR(200) NOT NULL
description VARCHAR(1000)
avatar_url VARCHAR(500)
agent_ids JSONB NOT NULL
created_at TIMESTAMP
updated_at TIMESTAMP
```

### chat_messages
```sql
id UUID PRIMARY KEY
chat_room_id UUID NOT NULL REFERENCES chat_rooms(id)
sender_id VARCHAR(255) NOT NULL
is_agent BOOLEAN NOT NULL
content TEXT NOT NULL
metadata JSONB
created_at TIMESTAMP
```

### user_chat_room_memberships
```sql
id UUID PRIMARY KEY
user_id VARCHAR(255) NOT NULL
chat_room_id UUID NOT NULL REFERENCES chat_rooms(id)
is_default BOOLEAN DEFAULT false
joined_at TIMESTAMP
UNIQUE (user_id, chat_room_id)
```

### agent_profiles
```sql
id UUID PRIMARY KEY
agent_id VARCHAR(100) NOT NULL UNIQUE
name VARCHAR(100) NOT NULL
avatar VARCHAR(500)
personality VARCHAR(1000) NOT NULL
system_prompt TEXT NOT NULL
capabilities JSONB NOT NULL
created_at TIMESTAMP
```

## Future Enhancements

### Planned Features

1. **Full Azure OpenAI Integration** - Replace stub responses with actual AI-generated content
2. **Agent Tool Calling** - Enable agents to call image generation and music tools
3. **Multi-Agent Conversations** - Allow multiple agents to respond in a thread
4. **User Preferences** - Customize agent behavior and selection
5. **Message Reactions** - React to agent messages
6. **Rich Media Support** - Images, audio, and file attachments

### Integration with Existing Tools

Agents can be configured to call existing MCP tools:

- **生图 capability** → Calls `generate_image` tool
- **音乐 capability** → Calls `play_random_music` tool

## Testing

### Test Sending a Message

```bash
curl -X POST http://localhost:5000/chat/mcp \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "X-User-Id: test-user-123" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "chat_with_group",
      "arguments": {
        "action": "send",
        "message": "你好,今天过得怎么样？"
      }
    }
  }'
```

### Monitor SignalR Messages

Connect to the DeviceHub and listen for `CustomMessage` events with `action: "group_chat"`.

## Development Notes

### Seed Data

The `ChatRoomSeeder` automatically runs on application startup and seeds:
- 1 default chat room ("日常交流群")
- 5 agent profiles with unique personalities

### Background Processing

Messages are processed asynchronously via Hangfire to ensure fast MCP response times. The flow is:

1. User sends message → Saved to database immediately
2. MCP returns success response
3. Hangfire job processes message in background
4. Agent generates response
5. Response saved to database
6. Response pushed to user via SignalR

This ensures the MCP tool responds in < 200ms while AI processing happens asynchronously.

## Troubleshooting

### No Default Room

If a user hasn't joined any room, the system automatically joins them to the first available room when they send a message.

### Agent Not Responding

Check:
1. Hangfire dashboard for job failures: `/hangfire`
2. SignalR connection is active
3. User ID is correctly set in the `X-User-Id` header

### Message History Empty

Ensure:
1. User is a member of the room
2. Messages have been sent to that room
3. Database migration has been applied

## API Examples

See the test scripts in `/docs/examples/` for complete API usage examples.

---

**Last Updated**: 2026-02-01
**Version**: 1.0.0
