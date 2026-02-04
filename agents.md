# agents.md

## 关于本文档
这是 **Verdure MCP** 项目的核心参考文档，用于记录项目架构、实现状态、技术决策与后续扩展方向，帮助后续开发保持上下文一致。

---

## 项目概述

### Verdure MCP 是什么？
**Verdure MCP** 是一个功能全面的 **Model Context Protocol (MCP)** 服务器，专为 AI 助手与物联网设备的集成而设计。

**核心定位：**
- ✅ **MCP 协议服务端** - 完全符合 [Model Context Protocol](https://modelcontextprotocol.io/) 规范
- ✅ **AI 驱动的工具集** - 提供图像生成、邮件通知、音频播放等 AI 可调用工具
- ✅ **物联网设备中心** - 通过 SignalR 实时连接 ESP32 等 IoT 设备
- ✅ **生产就绪基础设施** - PostgreSQL + Keycloak + Hangfire + Docker

**技术栈：**
- .NET 10.0 / ASP.NET Core
- PostgreSQL (数据持久化)
- SignalR (实时通信)
- Keycloak (身份认证)
- Hangfire (后台任务)
- Azure OpenAI (DALL-E 图像生成)

---

## ESP32 设备端参考

**ESP32 客户端代码：**
https://github.com/maker-community/xiaozhi-esp32/blob/signalr/main/signalr_client.cc

**SignalR 服务端示例：**
https://github.com/maker-community/esp-signalr-example/tree/main/signalr-server

---

## 当前实现状态 (✅ 已完成功能)

### 1. MCP 协议核心实现
**关键文件：** [src/Verdure.Mcp.Server/Program.cs](src/Verdure.Mcp.Server/Program.cs)

**已实现功能：**
- ✅ 完整 MCP HTTP 传输层
- ✅ 基于路由的工具分类过滤 (`McpToolFilterService`)
- ✅ 支持 Bearer Token 认证
- ✅ 动态工具注册与发现

**MCP 端点：**
```
/mcp/{toolCategory}  # toolCategory: all | image | email | debug | music
```

### 2. MCP 工具集
**位置：** [src/Verdure.Mcp.Server/Tools/](src/Verdure.Mcp.Server/Tools/)

**已实现的 4 个工具：**
1. **GenerateImageTool** - Azure OpenAI DALL-E 图像生成（支持异步任务队列）
2. **EmailTool** - SMTP 邮件发送
3. **MusicTool** - 随机音频推送到用户设备
4. **DebugTool** - 请求调试与 Header 检查

**后台任务：**
- `ImageGenerationBackgroundJob` - 异步图像生成（Hangfire）
- `MusicPushBackgroundJob` - 延迟音频推送（Hangfire）

### 3. SignalR 设备中心 (核心功能)
**Hub 文件：** [src/Verdure.Mcp.Server/Hubs/DeviceHub.cs](src/Verdure.Mcp.Server/Hubs/DeviceHub.cs)

**连接端点：**
```
WebSocket: /hub/device?access_token=YOUR_TOKEN
```

**核心功能：**
- ✅ 双重 Token 认证 (API Token + Keycloak JWT)
- ✅ 设备注册 (`RegisterDevice` - MAC + 元数据)
- ✅ 用户分组 (`Users:{userId}`)
- ✅ 设备分组 (`Device:{deviceId}`)
- ✅ 心跳机制 (`Heartbeat`)
- ✅ 自动状态管理 (Online/Offline)

**Hub 事件协议：**
```csharp
// 服务端 → 设备
Notification      // 连接确认 (字符串)
CustomMessage     // 业务指令 (JSON 字符串)
DeviceRegistered  // 注册成功响应 (对象)

// 设备 → 服务端
RegisterDevice(mac, deviceToken, metadata)
Heartbeat()
```

**与 ESP32 对齐点：**
- ✅ 认证方式：`?access_token=XXX` (URL 查询参数)
- ✅ 连接确认：`Notification` 事件
- ✅ 业务通道：`CustomMessage` (JSON 字符串)
- ✅ 直连模式：支持 `skip_negotiation(true)`

### 4. 设备推送服务
**实现文件：** [src/Verdure.Mcp.Server/Services/DevicePushServiceImpl.cs](src/Verdure.Mcp.Server/Services/DevicePushServiceImpl.cs)

**接口能力：** `IDevicePushService`
```csharp
// 按用户推送（所有设备）
Task SendToUserAsync(string userId, string method, object payload);

// 按设备推送（单个设备）
Task SendToDeviceAsync(Guid deviceId, string method, object payload);

// 专用消息类型
Task SendCustomMessageAsync(string userId, object message);
Task SendNotificationAsync(string userId, string message);
```

**使用示例：**
```csharp
// MusicTool 推送音频到用户设备
await _devicePushService.SendCustomMessageAsync(userId, new {
    action = "audio",
    url = "https://example.com/audio/song.mp3"
});
```

### 5. 数据模型
**位置：** [src/Verdure.Mcp.Domain/Entities/](src/Verdure.Mcp.Domain/Entities/)

**已实现实体：**
```csharp
Device              // IoT 设备 (MAC, OwnerUserId, Status, Metadata)
DeviceConnection    // SignalR 活动连接 (ConnectionId, DeviceId, UserId)
DeviceBinding       // 用户-设备绑定关系 (OwnerUserId, TargetUserId, Status)
ApiToken            // API 访问令牌
ImageGenerationTask // 图像生成任务队列
McpService          // MCP 服务配置
```

**枚举定义：**
```csharp
DeviceStatus          // Online, Offline, Error
DeviceBindingStatus   // Pending, Active, Rejected, Revoked
ImageTaskStatus       // Pending, Processing, Completed, Failed
```

### 6. REST API 端点
**文件：** [src/Verdure.Mcp.Server/Endpoints/DeviceEndpoints.cs](src/Verdure.Mcp.Server/Endpoints/DeviceEndpoints.cs)

**设备管理 API：**
```http
GET  /api/devices                      # 获取用户所有设备
GET  /api/devices/{deviceId}           # 获取指定设备
GET  /api/devices/{deviceId}/connections  # 获取设备连接
POST /api/devices/{deviceId}/bindings  # 创建设备绑定
GET  /api/devices/bindings             # 获取所有绑定关系
PUT  /api/devices/bindings/{bindingId} # 更新绑定状态
```

**其他 API：**
- `/api/version` - 版本信息
- `/api/mcp-services` - MCP 服务管理

### 7. 认证与授权体系
**文件：** [src/Verdure.Mcp.Server/Authentication/](src/Verdure.Mcp.Server/Authentication/)

**双重认证机制：**
1. **Keycloak JWT** - 标准 OIDC/OAuth2 (生产环境)
2. **API Token** - 数据库令牌验证 (开发/IoT 设备)
3. **策略选择器** - 自动路由到合适的认证方案

**关键组件：**
- `ApiTokenAuthenticationHandler` - API Token 验证器
- `PolicySelectorBuilder` - 认证策略选择
- `TokenValidationService` - JWT + API Token 统一验证接口

**认证流程：**
```
请求 → PolicySelector 
  ↓
  ├─ JWT Bearer → Keycloak 验证 → 角色映射
  └─ API Token  → 数据库验证 → 用户关联
```

### 8. 数据库与持久化
**DbContext：** [src/Verdure.Mcp.Infrastructure/Data/McpDbContext.cs](src/Verdure.Mcp.Infrastructure/Data/McpDbContext.cs)

**已配置实体集：**
```csharp
DbSet<Device>
DbSet<DeviceConnection>
DbSet<DeviceBinding>
DbSet<ApiToken>
DbSet<ImageGenerationTask>
DbSet<McpService>
```

**迁移文件：** [src/Verdure.Mcp.Server/Migrations/](src/Verdure.Mcp.Server/Migrations/)

---

## 系统架构

### 项目结构
```
Verdure.Mcp/
├── src/
│   ├── Verdure.Mcp.Server/         # 🎯 主服务入口
│   │   ├── Hubs/                   # SignalR Hubs (DeviceHub)
│   │   ├── Tools/                  # MCP 工具实现
│   │   ├── Endpoints/              # REST API 端点
│   │   ├── Services/               # 业务服务 (DevicePushService)
│   │   ├── Authentication/         # 认证处理器
│   │   ├── Migrations/             # EF Core 数据库迁移
│   │   ├── Settings/               # 配置模型
│   │   └── Utils/                  # 工具类
│   │
│   ├── Verdure.Mcp.Domain/         # 📦 领域模型
│   │   ├── Entities/               # 数据实体 (Device, DeviceBinding...)
│   │   └── Enums/                  # 枚举定义
│   │
│   ├── Verdure.Mcp.Infrastructure/ # 🔧 基础设施
│   │   ├── Data/                   # 数据库上下文 (McpDbContext)
│   │   └── Services/               # 基础服务接口
│   │
│   ├── Verdure.Mcp.Shared/         # 🔗 共享模型
│   │   └── Models/                 # DTO 模型
│   │
│   └── Verdure.Mcp.Web/            # 🌐 Web 前端
│       └── Components/             # Blazor 组件
│
├── docs/                           # 📚 技术文档
│   ├── SIGNALR_DEVICE_HUB.md      # DeviceHub 详细文档
│   ├── TOOL_CATEGORY_GUIDE.md     # MCP 工具分类指南
│   └── ...
│
└── docker/                         # 🐳 Docker 配置
    ├── Dockerfile
    └── docker-compose.yml
```

### 数据流向图

#### MCP 工具调用流程
```
AI 助手 (Claude/ChatGPT)
  ↓ HTTP POST /mcp/music
  │ Authorization: Bearer {token}
  │ X-User-Id: {userId}
  ↓
MCP Server (ASP.NET Core)
  ↓ McpToolFilterService (工具过滤)
  ↓ MusicTool.PlayRandomMusic()
  ↓ IDevicePushService.SendCustomMessageAsync()
  ↓ IHubContext<DeviceHub>.Clients.Group("Users:{userId}")
  ↓ SignalR WebSocket
  ↓
ESP32 设备 (onCustomMessage)
  ↓ 解析 JSON
  ↓ 播放音频
```

#### 设备连接与注册流程
```
ESP32 设备
  ↓ WebSocket /hub/device?access_token=XXX
  ↓
DeviceHub.OnConnectedAsync()
  ↓ TokenValidationService (验证 Token)
  ↓ 提取 userId
  ↓ Groups.AddToGroupAsync("Users:{userId}")
  ↓ SendAsync("Notification", "Connected")
  ↓
设备收到连接确认
  ↓ connection.invoke("RegisterDevice", mac, token, metadata)
  ↓
DeviceHub.RegisterDevice()
  ↓ 查找或创建 Device 实体
  ↓ 创建 DeviceConnection 记录
  ↓ SaveChangesAsync()
  ↓ SendAsync("DeviceRegistered", deviceInfo)
```

---

## 核心技术决策

### 为什么使用 SignalR？
- ✅ 原生 WebSocket 支持，完美适配 ESP32
- ✅ 自动重连与心跳机制
- ✅ 分组广播能力 (Users/Device Groups)
- ✅ .NET 生态无缝集成
- ✅ Hub 抽象简化开发

### 为什么支持双重认证？
- ✅ **Keycloak JWT** - 生产环境/企业用户场景
- ✅ **API Token** - 开发测试/IoT 设备场景
- ✅ **策略选择器** - 自动适配不同场景，无需修改代码

### 为什么使用 Hangfire？
- ✅ 持久化任务队列 (PostgreSQL 存储)
- ✅ 自动重试与失败处理
- ✅ Web UI 监控面板 (`/hangfire`)
- ✅ 分布式任务调度

### 为什么分离 MCP 工具分类？
- ✅ 减少工具发现开销 (AI 只看到需要的工具)
- ✅ 按需授权 (不同 Token 访问不同分类)
- ✅ 支持多租户场景
- ✅ 提升 AI 工具选择准确性

### 为什么使用 EF Core？
- ✅ 类型安全的数据访问
- ✅ 自动迁移管理
- ✅ 跨数据库支持 (PostgreSQL/SQL Server)
- ✅ 良好的异步性能

---

## 已实现的核心功能点

### ✅ 1. 双重 Token 验证
- 支持 Keycloak JWT (标准 OIDC)
- 支持数据库 API Token
- 策略选择器自动路由

### ✅ 2. 用户-设备绑定
- Device 实体记录所有权 (`OwnerUserId`)
- DeviceBinding 实体支持跨用户授权
- 绑定状态管理 (Pending/Active/Rejected/Revoked)
- REST API 完整 CRUD

### ✅ 3. 实时推送能力
- 按用户推送 (`Users:{userId}` 组)
- 按设备推送 (`Device:{deviceId}` 组)
- 支持自定义消息类型
- ESP32 兼容的 JSON 字符串格式

### ✅ 4. 后台任务队列
- Hangfire 集成
- 异步图像生成 (Azure OpenAI)
- 延迟音频推送
- 持久化任务存储

### ✅ 5. 工具分类过滤
- 路由参数驱动 (`/mcp/{toolCategory}`)
- 支持分类：all / image / email / debug / music
- 动态工具过滤器

---

## 待扩展功能 (后续实现建议)

### 🔲 社交功能完善
**优先级：高**
- [ ] 设备绑定确认流程 (WebSocket 实时通知)
- [ ] 跨用户设备互动消息
- [ ] 设备分享与权限管理
- [ ] 绑定请求推送通知

### 🔲 设备管理增强
**优先级：中**
- [ ] 设备固件 OTA 更新推送
- [ ] 设备状态监控与告警
- [ ] 批量设备操作 API
- [ ] 设备分组管理

### 🔲 监控与日志
**优先级：高**
- [ ] OpenTelemetry 完整集成 (已部分配置)
- [ ] 结构化日志输出 (Serilog)
- [ ] 性能指标采集
- [ ] 分布式追踪

### 🔲 多租户支持
**优先级：中**
- [ ] 组织/租户隔离
- [ ] 设备配额管理
- [ ] 租户级工具过滤

### 🔲 高级消息功能
**优先级：高**
- [ ] 消息持久化 (存储历史消息)
- [ ] 离线消息队列
- [ ] 消息确认与重试
- [ ] 消息加密传输

### 🔲 Web 前端完善
**优先级：中**
- [ ] 设备管理界面 (Blazor)
- [ ] 实时连接状态展示
- [ ] 绑定关系可视化
- [ ] 消息推送测试工具

---

## 技术参考文档

### 项目内部文档
- [SIGNALR_DEVICE_HUB.md](docs/SIGNALR_DEVICE_HUB.md) - DeviceHub 完整 API 文档
- [TOOL_CATEGORY_GUIDE.md](docs/TOOL_CATEGORY_GUIDE.md) - MCP 工具分类指南
- [CLAIMS_PRINCIPAL_EXTENSIONS.md](docs/CLAIMS_PRINCIPAL_EXTENSIONS.md) - 认证扩展实现
- [ROLE_IMPLEMENTATION_SUMMARY.md](docs/ROLE_IMPLEMENTATION_SUMMARY.md) - 角色映射实现

### 测试脚本
- `test-send-message.ps1` - SignalR 消息推送测试脚本

### API 文档
- **OpenAPI/Scalar**: `/scalar/v1` (开发环境)
- **Hangfire Dashboard**: `/hangfire`

---

## 部署与运维

### Docker 部署
```bash
# 使用 docker-compose
cd docker
docker-compose up -d

# 或使用 Dockerfile
docker build -t verdure-mcp .
docker run -p 5000:8080 verdure-mcp
```

### 环境变量配置
```env
# 数据库
ConnectionStrings__DefaultConnection=Host=localhost;Database=verdure_mcp;Username=postgres;Password=xxx

# Keycloak
Keycloak__Authority=https://keycloak.example.com
Keycloak__ClientId=verdure-mcp
Keycloak__Audience=verdure-mcp

# Azure OpenAI
AzureOpenAI__Endpoint=https://xxx.openai.azure.com/
AzureOpenAI__ApiKey=xxx
AzureOpenAI__DeploymentName=dall-e-3

# Email
EmailSettings__SmtpServer=smtp.example.com
EmailSettings__SmtpPort=587
EmailSettings__Username=xxx
EmailSettings__Password=xxx

# 图像存储
ImageStorage__BaseUrl=https://cdn.example.com
ImageStorage__LocalPath=/app/wwwroot/generated-images
```

### 数据库迁移
```bash
# 应用迁移
dotnet ef database update --project src/Verdure.Mcp.Server

# 创建新迁移
dotnet ef migrations add MigrationName --project src/Verdure.Mcp.Server
```

### 健康检查
```bash
# 基础健康检查
curl http://localhost:5000/health

# 数据库健康检查
curl http://localhost:5000/health/db
```

---

## ESP32 设备端集成指南

### ESP32 客户端配置
**参考代码：** https://github.com/maker-community/xiaozhi-esp32/blob/signalr/main/signalr_client.cc

**关键配置：**
```cpp
// 连接参数
hub_connection_builder()
    .with_url("wss://your-server.com/hub/device?access_token=YOUR_TOKEN")
    .skip_negotiation(true)  // 直连 WebSocket
    .build();

// 超时配置
server_timeout = 60s
keepalive_interval = 15s
handshake_timeout = 5s

// 事件监听
hub.on("Notification", [](const std::string& message) {
    Serial.println("Notification: " + message);
});

hub.on("CustomMessage", [](const std::string& jsonString) {
    // 解析 JSON
    cJSON* json = cJSON_Parse(jsonString.c_str());
    const char* action = cJSON_GetObjectItem(json, "action")->valuestring;
    
    if (strcmp(action, "audio") == 0) {
        const char* url = cJSON_GetObjectItem(json, "url")->valuestring;
        playAudio(url);
    }
    
    cJSON_Delete(json);
});

// 连接后注册
hub.invoke("RegisterDevice", macAddress, deviceToken, metadataJson);

// 定时心跳
hub.invoke("Heartbeat");
```

### 音频推送示例
**服务端触发（AI 调用 MCP 工具）：**
```bash
POST /mcp/music
Authorization: Bearer {token}
X-User-Id: user-123
```

**设备接收 CustomMessage：**
```json
{
  "action": "audio",
  "url": "https://cdn.example.com/audios/song.mp3"
}
```

---

## 后续实现优先级

### 🔥 高优先级（下一步实施）
1. **消息持久化** - 存储历史消息，支持离线推送
2. **设备绑定通知** - WebSocket 实时通知绑定请求
3. **OpenTelemetry 完整集成** - 分布式追踪与监控

### 🌟 中优先级（近期规划）
4. **设备固件 OTA** - 通过 SignalR 推送固件更新
5. **Web 前端完善** - Blazor 设备管理界面
6. **多租户支持** - 组织级隔离

### 💡 低优先级（长期规划）
7. **设备分组管理** - 批量操作与组播
8. **消息加密** - 端到端加密传输
9. **边缘计算集成** - 设备端 AI 推理

---

## 维护记录

| 日期 | 版本 | 更新内容 | 状态 |
|------|------|---------|------|
| 2026-02-04 | v2.1 | 新增 MCP 用户上下文转发功能（AsyncLocal 实现） | ✅ 已完成 |
| 2026-02-01 | v2.0 | 完整重写 - 反映当前实现状态与架构 | ✅ 已完成 |
| 之前 | v1.0 | 初始版本 - 设计建议阶段 | 已归档 |

---

## 快速参考索引

### 关键文件速查
- **Hub 入口**: [DeviceHub.cs](src/Verdure.Mcp.Server/Hubs/DeviceHub.cs)
- **推送服务**: [DevicePushServiceImpl.cs](src/Verdure.Mcp.Server/Services/DevicePushServiceImpl.cs)
- **MCP 工具**: [Tools/](src/Verdure.Mcp.Server/Tools/)
- **数据模型**: [Domain/Entities/](src/Verdure.Mcp.Domain/Entities/)
- **API 端点**: [Endpoints/](src/Verdure.Mcp.Server/Endpoints/)

### 常用命令
```bash
# 启动开发服务器
dotnet run --project src/Verdure.Mcp.Server

# 查看日志
dotnet run --project src/Verdure.Mcp.Server --verbosity detailed

# 测试 SignalR 推送
.\test-send-message.ps1 -UserId "user-123" -Message "Hello"

# 查看 Hangfire 任务
# 浏览器访问: http://localhost:5000/hangfire
```

---

## 总结

**Verdure MCP 当前状态：**
- ✅ **功能完整** - MCP 协议 + SignalR Hub + 设备管理全部实现
- ✅ **生产就绪** - 认证、授权、数据库、后台任务完整配置
- ✅ **ESP32 兼容** - 完全对齐 ESP32 客户端协议
- ✅ **可扩展** - 清晰的架构与扩展点

**核心价值：**
1. **AI ↔ IoT 桥接** - 通过 MCP 让 AI 助手直接控制物联网设备
2. **实时双向通信** - SignalR WebSocket 低延迟推送
3. **企业级基础设施** - 认证、授权、任务队列、监控齐全

**适用场景：**
- AI 语音助手控制智能家居 (ESP32 设备)
- AI 生成内容推送到设备 (图像/音频)
- 多用户设备共享与授权
- IoT 设备管理平台

---

## 备注
本文档应在每次重大功能更新或架构调整后同步更新，确保团队成员和后续开发者能快速理解项目全貌。

**文档维护责任：** 项目 Tech Lead  
**更新频率：** 随版本发布更新  
**反馈渠道：** 通过 Issue 或 Pull Request 提交文档改进建议

---

## AI 群组交流功能

### 功能概述
**状态**: ✅ 已实现（v2.0.0 - 2026-02-03）- **使用 Microsoft Agent Framework Handoff 模式**

Verdure MCP 的 AI 群组交流功能现在使用 **Microsoft Agent Framework** 的 **Handoff 模式**实现真正的多智能体协作。系统预设了 6 位具有不同性格和专长的 AI 智能体，通过智能 Triage Agent 自动路由到最合适的专家进行响应。

**架构升级**:
- ✅ **Agent Framework Handoff** - 官方 Workflow 模式（替代假的预定义响应）
- ✅ **智能路由** - Triage Agent 自动分析并选择最佳专家
- ✅ **真实 AI 对话** - Azure OpenAI GPT 模型生成回复
- ✅ **Workflow 缓存** - 按 ChatRoom 级别缓存，性能优化
- ✅ **动态配置** - 从数据库加载智能体，支持运行时更新

### 预设智能体

系统包含 6 位 AI 智能体，每位都有独特的性格和专长：

| 智能体 | Agent ID | 性格 | 擅长领域 | 工具能力 |
|--------|----------|------|----------|----------|
| 小甜甜 | agent-xiaotiantian | 甜美可爱，温柔体贴 | 情感支持、倾听、闲聊 | - |
| 御姐雅 | agent-yujieya | 成熟稳重，知性优雅 | 深度对话、人生建议 | - |
| 才女琳 | agent-cainvlin | 博学多才，逻辑清晰 | 知识问答、学习辅导 | - |
| 艺术家梅 | agent-yishujiamei | 富有创意，感性浪漫 | 创意启发、艺术鉴赏 | 生图 |
| 音乐家莉 | agent-yinyuejiali | 文艺浪漫，感性细腻 | 音乐推荐、情感表达 | 音乐 |
| 活泼妹 | agent-huopo | 活泼开朗，幽默风趣 | 闲聊、讲笑话 | - |

### MCP 工具

**工具名称**: `chat_with_group`

**支持的操作:**
- `send` - 发送消息到群组（默认）
- `list_rooms` - 列出已加入的群组
- `join` - 加入指定群组
- `set_default` - 设置默认群组
- `get_history` - 获取历史消息

**示例调用:**
```bash
# 发送消息
POST /mcp/all
X-User-Id: user-123
Content-Type: application/json

{
  "tool": "chat_with_group",
  "params": {
    "message": "今天心情不太好，有谁能陪我聊聊天吗？",
    "action": "send"
  }
}
```

**响应流程:**
1. 用户消息保存到数据库
2. 创建 Hangfire 后台任务（立即返回响应）
3. 后台任务选择合适的智能体
4. 生成回复并处理工具调用
5. 通过 SignalR 推送回复到用户设备

### SignalR 消息格式

**群组消息推送:**
```json
{
  "action": "group_chat",
  "roomId": "uuid",
  "roomName": "日常交流群",
  "message": {
    "id": "uuid",
    "senderId": "agent-xiaotiantian",
    "senderName": "小甜甜",
    "content": "哎呀，听到你说心情不好，我好心疼呀～",
    "isAgent": true,
    "timestamp": "2026-02-01T10:30:00Z",
    "attachments": [
      {
        "type": "image",
        "url": "https://cdn.example.com/generated/xxx.png"
      }
    ],
    "metadata": {
      "avatar": "https://api.dicebear.com/7.x/avataaars/svg?seed=xiaotiantian",
      "personality": "甜美可爱，温柔体贴"
    }
  }
}
```

### 数据库架构

新增 4 张表支持 AI 群组功能：

1. **chat_rooms** - 群组信息
2. **chat_messages** - 聊天消息
3. **user_chat_room_memberships** - 用户群组关系
4. **agent_profiles** - 智能体配置

**迁移文件**: `20260201125600_AddAiGroupChat.cs`

### 关键文件

- **MCP 工具**: [AiGroupChatTool.cs](src/Verdure.Mcp.Server/Tools/AiGroupChatTool.cs)
- **Workflow 管理**: [WorkflowManager.cs](src/Verdure.Mcp.Server/Services/WorkflowManager.cs) ⭐ **新增**
- **智能体编排**: [AgentOrchestrationService.cs](src/Verdure.Mcp.Server/Services/AgentOrchestrationService.cs) ⭐ **重构**
- **后台任务**: [ChatMessageBackgroundJob.cs](src/Verdure.Mcp.Server/Tools/ChatMessageBackgroundJob.cs)
- **数据初始化**: [ChatRoomSeeder.cs](src/Verdure.Mcp.Server/Services/ChatRoomSeeder.cs)
- **实体定义**: [Domain/Entities/](src/Verdure.Mcp.Domain/Entities/) (ChatRoom, ChatMessage, UserChatRoomMembership, AgentProfile)
- **整合文档**: [AGENT_FRAMEWORK_INTEGRATION.md](docs/AGENT_FRAMEWORK_INTEGRATION.md) ⭐ **新增**

### 使用场景示例

#### 场景 1: 情感倾诉
```json
{
  "message": "今天工作压力好大，感觉快要崩溃了..."
}
```
**响应**: 小甜甜或活泼妹提供情感支持

#### 场景 2: 知识问答
```json
{
  "message": "能帮我解释一下什么是区块链吗？"
}
```
**响应**: 才女琳提供专业解答

#### 场景 3: 创意生图
```json
{
  "message": "我想要一张温馨的咖啡店画面"
}
```
**响应**: 艺术家梅回复并生成图片

#### 场景 4: 音乐推荐
```json
{
  "message": "推荐一些放松的音乐给我听吧"
}
```
**响应**: 音乐家莉推荐并播放音乐

### 技术实现

**Agent Framework Handoff 模式:**
```csharp
// 1. WorkflowManager 创建 Handoff Workflow
var triageAgent = new ChatClientAgent(_chatClient, triageInstructions, "triage", "智能路由器");
var specialistAgents = agents.Select(a => 
    new ChatClientAgent(_chatClient, a.SystemPrompt, a.AgentId, a.Personality)).ToList();

var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, specialistAgents)      // triage → specialists
    .WithHandoffs(specialistAgents, triageAgent)      // specialists → triage
    .Build();

// 2. AgentOrchestrationService 执行 Workflow
await using var run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// 3. 处理 WorkflowEvent 提取 Agent 响应
await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent agentUpdate)
    {
        // 追踪 Agent 切换和内容生成
    }
}
```

**智能体选择机制:**
1. **话题匹配** - Triage Agent 分析消息主题
2. **关键词识别** - 检测能力标识（图、音乐等）
3. **语气风格** - 匹配用户语气（活泼、理性等）
4. **上下文连贯** - 继续与同一专家对话
5. **隐式意图** - 理解未明说的需求

**响应生成:**
- 使用 Azure OpenAI GPT 模型（不再是预定义模板）
- 每个智能体有独立的 SystemPrompt
- 基于智能体性格和专长生成个性化回复
- 支持 MCP 工具调用（图像生成、音乐等）

**异步处理:**
- Hangfire 后台任务处理消息
- Workflow 执行 + GPT 推理
- SignalR 实时推送到设备

**用户上下文转发（v2.1 新增）:**
- 使用 `AsyncLocal<UserContext>` 在异步调用链中传递用户信息
- 在 `AgentOrchestrationService` 中设置 `UserContext`（包含 UserId 和 UserEmail）
- 在 `McpToolService` 中将用户信息注入到外部 MCP Server 的 HttpClient 请求头
- 支持 `X-User-Id` 和 `X-User-Email` 请求头自动转发
- 详见: [MCP_USER_CONTEXT_FORWARDING.md](docs/MCP_USER_CONTEXT_FORWARDING.md)
- 每个智能体有独立的 SystemPrompt
- 基于智能体性格和专长生成个性化回复
- 支持 MCP 工具调用（图像生成、音乐等）

**异步处理:**
- Hangfire 后台任务处理消息
- Workflow 执行 + GPT 推理
- SignalR 实时推送到设备

### 开发环境初始化

在开发环境中，系统会自动：
1. 创建默认群组："日常交流群"
2. 初始化 6 个智能体配置
3. 配置智能体能力和系统提示词

**触发时机**: 应用启动时，如果数据库中没有群组和智能体数据

### 配置要求

**Azure OpenAI**:
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

**日志配置**（调试）:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Agents.AI": "Debug",
      "Verdure.Mcp.Server.Services.WorkflowManager": "Debug"
    }
  }
}
```

### 未来增强计划

以下功能可在后续版本实现：

1. **工具增强**
   - 为 Specialist Agents 添加更多 MCP 工具
   - 支持多模态输入（图片、语音）
   - 实现工具链式调用

2. **对话优化**
   - 增加更多历史消息上下文
   - 实现对话摘要功能
   - 优化 Token 使用

3. **监控与分析**
   - 统计每个 Agent 的响应次数
   - 分析用户偏好（最常用的 Agent）
   - 监控 Workflow 执行耗时

4. **群组管理增强**
   - 创建自定义群组
   - 邀请其他用户
   - 群组权限配置
   - 智能体动态添加/移除

### 详细文档

完整使用指南请参考: [AI_GROUP_CHAT_GUIDE.md](docs/AI_GROUP_CHAT_GUIDE.md)

---
