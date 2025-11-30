# 生成图片存储目录

此目录用于存储由 DALL-E 生成的图片文件。

## 配置说明

在 `appsettings.json` 或环境变量中配置图片存储设置：

```json
{
  "ImageStorage": {
    "StoragePath": "generated-images",
    "BaseUrl": "https://your-domain.com",
    "EnableAutoCleanup": true,
    "RetentionDays": 7
  }
}
```

### 配置项说明

- **StoragePath**: 图片存储的相对路径（相对于 wwwroot）
- **BaseUrl**: 图片访问的基础 URL（域名部分），例如 `https://api.example.com`
- **EnableAutoCleanup**: 是否启用自动清理过期图片
- **RetentionDays**: 图片保留天数，超过此天数的图片会被自动清理

## 环境变量配置（推荐用于生产环境）

```bash
export ImageStorage__BaseUrl="https://api.example.com"
export ImageStorage__StoragePath="generated-images"
export ImageStorage__RetentionDays=7
```

## URL 访问格式

生成的图片可通过以下格式访问：
```
{BaseUrl}/generated-images/{TaskId}.png
```

例如：
```
https://api.example.com/generated-images/123e4567-e89b-12d3-a456-426614174000.png
```

## 注意事项

1. 确保 Web 服务器有权限读写此目录
2. 在生产环境中使用 CDN 或对象存储服务以获得更好的性能
3. 定期检查磁盘空间使用情况
4. 建议启用自动清理功能以避免磁盘空间耗尽
