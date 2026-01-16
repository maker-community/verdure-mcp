# agents.md

## 我知道什么是 agents.md
这是用于记录自动化实现分析、需求背景、方案设计与后续落地参考的工作文档，帮助后续实现保持上下文一致。

---

## 现有实现分析（ESP32 端）

### SignalR 客户端实现概述
来源文件：xiaozhi-esp32/main/signalr_client.cc

**关键点：**
- 连接方式：使用 `hub_connection_builder`，并 `skip_negotiation(true)`，直连 WebSocket。
- 认证方式：将 Bearer Token 放入 URL 查询参数 `access_token`（适配 ASP.NET Core SignalR 的 WebSocket 认证标准）。
- 自动重连：禁用了 SDK 的 auto-reconnect，改为应用层重连（单独 FreeRTOS 任务 + 退避机制）。
- 心跳与超时：
  - `server_timeout = 60s`
  - `keepalive_interval = 15s`
  - `handshake_timeout = 5s`
- 事件回调：
  - `Notification` 作为连接确认事件（服务端连接后主动推送）。
  - `CustomMessage` 为业务主通道（JSON 字符串，ESP32 使用 cJSON 解析）。

**结论：**
ESP32 端当前与 SignalR 的交互模型是：
- 基于 `CustomMessage` 接收业务控制指令。
- 服务端通过 `Notification` 确认连接建立。
- Token 通过 `access_token` 传递。

---

## 现有实现分析（服务端示例）

### SignalR 服务端概述
来源目录：esp-signalr-example/signalr-server

**Program.cs：**
- 注册 SignalR 与 CORS，Hub 路由 `/chatHub`。
- 提供 REST 控制接口 `/api/device/*`，通过 Hub 推送 `CustomMessage`。
- `/api/device/*` 支持广播或指定 `connectionId`。

**ChatHub.cs：**
- `OnConnectedAsync`：记录 `ConnectionId`、IP、UserAgent，并输出 `access_token` 的 query 信息。
- `Notification`：连接成功后，主动通知客户端。
- `CustomMessage`：服务端通过 Hub `SendAsync("CustomMessage", json)` 推送。

**结论：**
示例服务端只做连接管理和消息转发，不涉及真实用户体系和设备绑定。

---

## Verdure MCP 项目现状概述

当前 Verdure MCP 为 ASP.NET Core 服务，重点特性：
- MCP 协议服务端，支持多工具路由与过滤。
- 使用 PostgreSQL 持久化（已有 `McpDbContext` 和基础实体）。
- 已有 JWT/Token 验证能力（Keycloak + TokenValidation）。

目前尚无 SignalR Hub 与设备绑定业务模型。

---

## 拟集成目标（你的意图）

- 在 Verdure MCP 中新增 SignalR Hub。
- ESP32 设备通过 Hub 注册（使用 Token + MAC 地址）。
- MCP 通过 `userId` 推送消息到设备。
- 支持用户设备绑定关系：
  - 设备上线时上报 `mac + userToken`。
  - Hub 记录状态与绑定关系。
  - 用户可查看自己的设备，并允许“绑定他人设备”。
  - 绑定关系用于后续“社交”功能（设备互联）。

---

## 可行方案（建议实现路线）

### 1) 服务器端整体结构
新增模块（建议在 Verdure.Mcp.Server + Verdure.Mcp.Domain + Verdure.Mcp.Infrastructure）：
- `SignalR Hub`：接收设备连接、注册与业务事件。
- `Device Registry` 服务：维护在线连接与用户绑定关系。
- `Device Push Service`：根据 `userId` 推送到指定设备（基于 Hub + Groups）。

### 2) Hub 设计
建议新增 Hub：`DeviceHub`
- 连接时读取 `access_token` 并解析 `userId`。
- 客户端上线后调用 `RegisterDevice(mac, deviceToken, meta)`。
- 服务器将连接加入用户分组：`Users:{userId}`。

**关键事件：**
- `Notification`：用于连接成功确认。
- `CustomMessage`：保留对 ESP32 的指令通道。
- `DeviceStatus`：设备上报状态心跳。

### 3) 数据模型（建议新增）
新增实体示例（可用于后续持久化与社交扩展）：
- `Device`
  - `Id`, `MacAddress`, `OwnerUserId`, `LastSeenAt`, `Status`, `Metadata`
- `DeviceConnection`
  - `ConnectionId`, `DeviceId`, `UserId`, `ConnectedAt`, `LastHeartbeatAt`
- `DeviceBinding`
  - `Id`, `OwnerUserId`, `TargetUserId`, `DeviceId`, `Status`

### 4) 用户设备绑定流程
**建议流程：**
1. 设备连接 Hub 时携带 `access_token`。
2. 设备调用 `RegisterDevice(mac, token)`。
3. Hub 校验 token 与 `userId`，建立 `Device`、`DeviceConnection`。
4. Hub 保存 `Device.OwnerUserId = userId`。
5. 可选：设备状态上报 + 心跳。

### 5) 推送消息到用户设备
**目标：** MCP 能够 `SendToUser(userId, payload)`。

**实现方式：**
- Hub 为用户加入组 `Users:{userId}`。
- MCP 服务层调用 `IHubContext<DeviceHub>.Clients.Group(...)`。
- 支持广播到：
  - 用户所有设备
  - 指定设备 `DeviceId`

### 6) 社交功能扩展思路
- `DeviceBinding` 可作为社交连接关系（设备/用户之间的授权）。
- 支持“授权/确认”流程：
  - A 设备绑定 B 设备 -> B 用户确认。
- 允许设备之间的互动消息通道（例如互发 `CustomMessage`）。

---

## 推荐的后续实现步骤（落地顺序）

1. 在 Verdure.Mcp.Server 中新增 SignalR（Hub + DI + 路由）。
2. 增加 Device 模型与持久化（EF Core Migration）。
3. 实现设备注册 + 连接状态追踪。
4. MCP 增加推送接口（支持按 `userId` 推送）。
5. 扩展绑定模型（DeviceBinding + API + Hub 事件）。

---

## 与现有 ESP32 端对齐点
- ESP32 已支持 `access_token` 放到 URL 查询参数。
- ESP32 依赖 `Notification` 确认连接。
- ESP32 主要监听 `CustomMessage`，服务端继续通过该事件推送即可。

---

## 后续实现参考（与本项目相关）

- MCP Server 入口：src/Verdure.Mcp.Server/Program.cs
- 数据库上下文：src/Verdure.Mcp.Infrastructure/Data/McpDbContext.cs
- 当前已有的认证与 Token 验证体系可复用（Keycloak/JWT）

---

## 备注
本文件用于后续实现时快速理解背景与目标，避免重复分析。