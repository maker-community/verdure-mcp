# Docker 部署指南

## 📦 构建镜像

使用 Alpine Linux 基础镜像，优化后的镜像大小约 **230MB**（相比 Debian 版本节省约 100MB）。

### 基本构建命令

```powershell
# 从项目根目录构建
docker build -f docker/Dockerfile -t verdure-mcp-server:latest .
```

### 使用构建参数

```powershell
# 指定构建配置（默认为 Release）
docker build -f docker/Dockerfile --build-arg BUILD_CONFIGURATION=Release -t verdure-mcp-server:latest .
```

## 🚀 运行容器

### 基本运行

```powershell
docker run -d `
  --name verdure-mcp `
  -p 8080:8080 `
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=verdure_mcp;Username=postgres;Password=yourpassword" `
  verdure-mcp-server:latest
```

### 完整配置示例

```powershell
docker run -d `
  --name verdure-mcp `
  -p 8080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=verdure_mcp;Username=postgres;Password=yourpassword" `
  -e AzureOpenAI__Endpoint="https://your-openai.openai.azure.com/" `
  -e AzureOpenAI__ApiKey="your-api-key" `
  -e Email__SmtpServer="smtp.gmail.com" `
  -e Email__SmtpPort=587 `
  verdure-mcp-server:latest
```

## 🔧 配置文件挂载

可以通过挂载配置文件到 `/app/wwwroot/appsettings.json` 来覆盖默认设置：

```powershell
docker run -d `
  --name verdure-mcp `
  -p 8080:8080 `
  -v ${PWD}/config/appsettings.json:/app/wwwroot/appsettings.json:ro `
  verdure-mcp-server:latest
```

容器启动时会自动：
1. 检测 `/app/wwwroot/appsettings.json` 是否存在（或已被挂载）
2. 如果文件存在且已变更，自动创建压缩版本
3. 使用 Brotli 和 Gzip 压缩配置文件（`.br` 和 `.gz`）
4. 存储文件哈希值，避免重复压缩

**注意**：外部通过 Docker volume 挂载配置文件时，容器内部会自动处理，无需手动干预。

## 📊 健康检查

容器内置健康检查，每 30 秒检查一次 `/api/health` 端点：

```powershell
# 查看容器健康状态
docker inspect --format='{{.State.Health.Status}}' verdure-mcp
```

健康检查配置：
- 检查间隔：30 秒
- 超时时间：10 秒
- 启动等待：40 秒
- 重试次数：3 次

## 🌐 访问应用

容器启动后，通过以下地址访问：

- **Web 界面**：http://localhost:8080
- **API 文档**：http://localhost:8080/scalar/v1（开发环境）
- **健康检查**：http://localhost:8080/health
- **版本信息**：http://localhost:8080/api/version

## 🐛 调试

### 查看日志

```powershell
# 查看容器日志
docker logs verdure-mcp

# 实时跟踪日志
docker logs -f verdure-mcp
```

### 进入容器

```powershell
# 以 shell 方式进入容器
docker exec -it verdure-mcp /bin/sh
```

### 检查文件

```powershell
# 检查 Blazor 静态文件
docker exec verdure-mcp ls -la /app/wwwroot/_framework

# 检查配置文件压缩
docker exec verdure-mcp ls -la /app/wwwroot/appsettings.json*
```

## 📝 镜像特性

### Alpine Linux 优化

- **基础镜像**：`mcr.microsoft.com/dotnet/aspnet:9.0-alpine`
- **大小优势**：约 230MB（vs Debian 339MB）
- **安全性**：更小的攻击面
- **性能**：轻量级，快速启动

### 包含工具

镜像中包含以下工具：

- `curl`：健康检查
- `brotli`：Brotli 压缩
- `gzip`：Gzip 压缩
- `icu-libs`：全球化支持
- `tzdata`：时区支持

### 多阶段构建

1. **build** - 使用 .NET SDK 9.0 编译项目
2. **publish** - 发布应用程序
3. **final** - 运行时镜像（Alpine）

## 🔒 生产环境建议

### 环境变量

建议通过环境变量配置敏感信息，而不是直接写入配置文件：

```powershell
-e ConnectionStrings__DefaultConnection="..." `
-e AzureOpenAI__ApiKey="..." `
-e Email__Password="..."
```

### 资源限制

生产环境中建议设置资源限制：

```powershell
docker run -d `
  --name verdure-mcp `
  --memory=512m `
  --cpus=1.0 `
  -p 8080:8080 `
  verdure-mcp-server:latest
```

### 持久化存储

如果需要持久化数据，建议使用数据卷：

```powershell
docker run -d `
  --name verdure-mcp `
  -v verdure-data:/app/data `
  -p 8080:8080 `
  verdure-mcp-server:latest
```

## 🆘 故障排查

### 问题 1: 前端页面显示空白

**原因**：静态文件未正确包含

**解决**：
```powershell
# 检查构建输出
docker exec verdure-mcp ls -la /app/wwwroot/_framework
# 应该包含 Blazor 框架文件
```

### 问题 2: API 调用失败

**原因**：数据库连接字符串配置错误

**解决**：
```powershell
# 检查环境变量
docker exec verdure-mcp env | grep ConnectionStrings
```

### 问题 3: 健康检查失败

**原因**：应用启动时间超过 40 秒

**解决**：增加启动等待时间或检查应用日志
```powershell
docker logs verdure-mcp
```

## 📚 参考资料

- [ASP.NET Core 托管 Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly)
- [Docker 最佳实践](https://docs.docker.com/develop/dev-best-practices/)
- [Alpine Linux 包管理](https://wiki.alpinelinux.org/wiki/Alpine_Package_Keeper)
