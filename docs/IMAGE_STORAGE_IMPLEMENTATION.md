# 图片存储功能使用指南

## 功能说明

本次更新实现了将 DALL-E 生成的图片保存到服务器本地文件系统，并返回可访问的 URL，而不是返回 base64 数据。

## 主要改动

### 1. 新增文件

- **Settings/ImageStorageSettings.cs**: 图片存储配置类
- **Services/IImageStorageService.cs**: 图片存储服务接口
- **Services/ImageStorageService.cs**: 图片存储服务实现
- **wwwroot/generated-images/**: 图片存储目录

### 2. 配置文件

#### appsettings.json
```json
{
  "ImageStorage": {
    "StoragePath": "generated-images",
    "BaseUrl": "https://localhost:5000",
    "EnableAutoCleanup": true,
    "RetentionDays": 7
  }
}
```

#### appsettings.Development.json
```json
{
  "ImageStorage": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

### 3. 环境变量配置（生产环境推荐）

```bash
# Linux/Mac
export ImageStorage__BaseUrl="https://api.yourdomain.com"
export ImageStorage__StoragePath="generated-images"
export ImageStorage__RetentionDays=7

# Windows PowerShell
$env:ImageStorage__BaseUrl="https://api.yourdomain.com"
$env:ImageStorage__StoragePath="generated-images"
$env:ImageStorage__RetentionDays="7"
```

## 工作流程

### 同步模式（无用户信息）

1. 用户调用 `generate_image` 工具，不传递 `X-User-Email` 和 `X-User-Id` 请求头
2. 系统同步调用 DALL-E API 生成图片（返回 base64）
3. 将 base64 图片保存到 `wwwroot/generated-images/{taskId}.png`
4. 生成可访问的 URL: `{BaseUrl}/generated-images/{taskId}.png`
5. 返回响应，**只包含 URL**，不包含 base64 数据

**返回示例：**
```json
{
  "taskId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "已完成",
  "message": "图片生成成功",
  "imageUrl": "https://localhost:5001/generated-images/123e4567-e89b-12d3-a456-426614174000.png",
  "revisedPrompt": "A detailed illustration of...",
  "isAsync": false
}
```

### 异步模式（有用户信息）

1. 用户调用 `generate_image` 工具，同时传递 `X-User-Email` 和 `X-User-Id` 请求头
2. 系统使用 Hangfire 异步处理
3. 立即返回任务已加入队列的中文消息
4. 后台任务完成后，图片同样保存到本地并生成 URL
5. 如果提供了邮箱，发送邮件通知

**返回示例：**
```json
{
  "taskId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "处理中",
  "message": "图片生成任务已加入队列。如果您提供了邮箱地址，稍后会收到生成结果。",
  "isAsync": true
}
```

## 图片访问

生成的图片可以通过以下 URL 直接访问：

```
{BaseUrl}/generated-images/{TaskId}.png
```

例如：
```
https://api.example.com/generated-images/123e4567-e89b-12d3-a456-426614174000.png
```

## 自动清理

如果配置中启用了 `EnableAutoCleanup`（默认启用），系统会自动清理超过 `RetentionDays`（默认 7 天）的图片文件。

可以调用 `IImageStorageService.CleanupExpiredImagesAsync()` 方法手动触发清理。

## 测试步骤

### 1. 更新配置

在 `appsettings.Development.json` 中设置正确的 `BaseUrl`：
```json
{
  "ImageStorage": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

### 2. 运行项目

```bash
cd src/Verdure.Mcp.Server
dotnet run
```

### 3. 测试同步生成（不传用户信息）

调用 MCP 工具生成图片，不传递 `X-User-Email` 和 `X-User-Id`：

```bash
# 示例请求（具体调用方式取决于你的 MCP 客户端）
POST /image/mcp
Content-Type: application/json

{
  "method": "tools/call",
  "params": {
    "name": "generate_image",
    "arguments": {
      "prompt": "一只可爱的小猫在玩毛线球"
    }
  }
}
```

响应应该包含 `imageUrl` 而不是 `imageBase64`。

### 4. 测试异步生成（传用户信息）

添加请求头：

```bash
X-User-Email: test@example.com
X-User-Id: user123
```

应该立即返回"处理中"的中文消息。

### 5. 验证图片访问

使用浏览器或 curl 访问返回的 `imageUrl`，应该能看到生成的图片。

## 注意事项

1. **磁盘空间**: 定期检查 `wwwroot/generated-images/` 目录的大小
2. **性能**: 对于高流量应用，建议使用 CDN 或对象存储（如 Azure Blob Storage, AWS S3）
3. **安全**: 图片是公开可访问的，不要生成包含敏感信息的图片
4. **备份**: 重要的图片应该有备份策略
5. **BaseUrl 配置**: 在生产环境中，必须正确配置 `ImageStorage:BaseUrl` 环境变量

## 未来优化建议

1. 支持其他存储后端（Azure Blob, AWS S3, MinIO 等）
2. 添加图片缩略图生成
3. 添加图片水印功能
4. 实现图片访问统计
5. 添加图片格式转换（PNG, JPEG, WebP）
