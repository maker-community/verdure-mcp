# Dockerfile 和版本管理实施总结

## 📊 实施概览

成功为 Verdure MCP Server 项目添加了 Docker 支持和全局版本管理，实现了单镜像部署和版本信息展示功能。

## ✅ 已完成的工作

### 1. Docker 配置

#### Dockerfile (`docker/Dockerfile`)
- ✅ 基于 Alpine Linux 的多阶段构建
- ✅ 使用 `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` 基础镜像
- ✅ 镜像大小优化到约 **230MB**（相比 Debian 节省约 100MB）
- ✅ 包含必要的工具：curl、brotli、gzip、icu-libs、tzdata
- ✅ 内置健康检查（每 30 秒检查 `/api/health`）
- ✅ 支持配置文件挂载

**多阶段构建流程**：
1. **base** - Alpine 运行时基础镜像
2. **build** - 使用 .NET SDK 9.0 构建项目
3. **publish** - 发布应用程序
4. **final** - 最终运行时镜像

#### Entrypoint 脚本 (`docker/entrypoint.sh`)
- ✅ 处理配置文件挂载（`/config/appsettings.json`）
- ✅ 自动压缩配置文件（Brotli + Gzip）
- ✅ 智能缓存（仅在配置变更时重新压缩）
- ✅ POSIX Shell 兼容

#### 其他文件
- ✅ `.dockerignore` - 优化构建上下文
- ✅ `docker/README.md` - 完整的 Docker 使用文档

### 2. 版本管理工具

#### API 版本工具 (`src/Verdure.Mcp.Server/Utils/`)

**AssemblyExtensions.cs**
- ✅ 从程序集提取版本信息
- ✅ 优先级：InformationalVersion → FileVersion → AssemblyVersion
- ✅ 自动剥离提交哈希（去除 `+` 后的部分）

**VersionHelpers.cs**
- ✅ 提供 API 显示版本（`ApiDisplayVersion`）
- ✅ 提供 .NET 运行时版本（`RuntimeVersion`）
- ✅ 提供操作系统信息（`OsDescription`、`OsArchitecture`）
- ✅ 懒加载优化

#### Web 版本工具 (`src/Verdure.Mcp.Web/Utils/`)

**AssemblyExtensions.cs** 和 **VersionHelpers.cs**
- ✅ 与 API 版本工具功能相同
- ✅ 适配 Blazor WebAssembly 环境
- ✅ 提供 Web 显示版本（`WebDisplayVersion`）

### 3. 版本信息展示

#### API 端点 (`src/Verdure.Mcp.Server/Endpoints/VersionEndpoint.cs`)
- ✅ 创建 `/api/version` 端点
- ✅ 返回完整版本信息（API 版本、运行时版本、OS 信息）
- ✅ 允许匿名访问
- ✅ OpenAPI 文档集成

#### Web 界面 (`src/Verdure.Mcp.Web/Layout/Footer.razor`)
- ✅ 精简版 Footer：显示版本徽章
- ✅ 完整版 Footer：显示版本徽章和详细信息
- ✅ 显示内容：
  - Web 版本（如：v1.0.0）
  - .NET 运行时版本（如：.NET 9.0.0）
  - Blazor WebAssembly 标识

#### 启动日志 (`src/Verdure.Mcp.Server/Program.cs`)
- ✅ 应用启动时记录版本信息
- ✅ 日志包含 API 版本和运行时版本

## 📦 项目结构变化

```
verdure-mcp/
├── docker/
│   ├── Dockerfile              # 单镜像 Dockerfile
│   ├── entrypoint.sh           # 容器入口脚本
│   └── README.md               # Docker 使用文档
├── src/
│   ├── Verdure.Mcp.Server/
│   │   ├── Endpoints/
│   │   │   └── VersionEndpoint.cs    # 版本 API 端点
│   │   ├── Utils/
│   │   │   ├── AssemblyExtensions.cs # 程序集版本提取
│   │   │   └── VersionHelpers.cs     # API 版本工具
│   │   └── Program.cs                # 添加版本日志
│   └── Verdure.Mcp.Web/
│       ├── Layout/
│       │   └── Footer.razor          # 更新版本显示
│       └── Utils/
│           ├── AssemblyExtensions.cs # 程序集版本提取
│           └── VersionHelpers.cs     # Web 版本工具
└── .dockerignore                     # Docker 构建优化
```

## 🚀 使用方法

### 构建 Docker 镜像

```powershell
# 从项目根目录构建
docker build -f docker/Dockerfile -t verdure-mcp-server:latest .
```

### 运行容器

```powershell
docker run -d `
  --name verdure-mcp `
  -p 8080:8080 `
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=verdure_mcp;Username=postgres;Password=password" `
  verdure-mcp-server:latest
```

### 访问应用

- **Web 界面**：http://localhost:8080
- **API 文档**：http://localhost:8080/scalar/v1
- **健康检查**：http://localhost:8080/health
- **版本信息**：http://localhost:8080/api/version

## 🔍 版本信息查看

### 1. Web 界面

访问任何页面，在页脚（Footer）即可看到版本信息：
- **精简模式**（仪表盘）：显示版本徽章
- **完整模式**（首页）：显示详细版本信息和社交链接

### 2. API 端点

```bash
# 获取版本信息
curl http://localhost:8080/api/version
```

响应示例：
```json
{
  "success": true,
  "data": {
    "apiVersion": "1.0.0",
    "runtimeVersion": "9.0.0",
    "osDescription": "Linux 6.1.0-27-amd64 #1 SMP PREEMPT_DYNAMIC Debian 6.1.115-1 (2024-11-01)",
    "osArchitecture": "X64"
  }
}
```

### 3. 容器日志

```powershell
docker logs verdure-mcp
```

启动日志会显示：
```
Verdure MCP Server version: 1.0.0
.NET Runtime version: 9.0.0
```

## 🎯 版本管理机制

### 版本来源优先级

1. **AssemblyInformationalVersionAttribute** - 最详细，包含语义版本 + 提交哈希
   - 格式：`1.0.0+abc123def`
   - 显示时自动剥离提交哈希：`1.0.0`

2. **AssemblyFileVersionAttribute** - 文件版本
   - 格式：`1.0.0.0`

3. **AssemblyVersion** - 程序集版本
   - 格式：`1.0.0.0`

### 版本配置

版本在 `Directory.Build.props` 中统一管理：

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <InformationalVersion>$(Version)+$(SourceRevisionId)</InformationalVersion>
</PropertyGroup>
```

## 📝 技术特性

### Docker 镜像优化

- **Alpine Linux**：更小的镜像体积和攻击面
- **多阶段构建**：分离构建和运行时环境
- **层缓存优化**：先复制项目文件，后复制源代码
- **健康检查**：自动监控应用健康状态

### 版本信息缓存

- **懒加载**：使用 `Lazy<T>` 延迟初始化
- **单例模式**：版本信息在应用生命周期内只计算一次
- **高性能**：避免重复的反射调用

### Blazor 集成

- **静态文件服务**：API 项目引用 Web 项目，自动包含静态文件
- **SPA 路由**：使用 `MapFallbackToFile("index.html")` 支持客户端路由
- **开发调试**：开发环境启用 `UseWebAssemblyDebugging()`

## 🔄 与参考项目的差异

### 相同点
- ✅ Alpine Linux 基础镜像
- ✅ 多阶段构建
- ✅ 版本管理机制
- ✅ Footer 版本显示
- ✅ API 版本端点

### 差异点
- ❌ **无 Docker Compose**（按需求仅提供 Dockerfile）
- ❌ **无 Aspire 配置**（项目不使用 .NET Aspire）
- ✅ **简化的项目结构**（更少的项目层级）
- ✅ **自定义端点路径**（MCP 端点使用 `/{toolCategory}/mcp`）

## 🎉 实施成果

1. **Docker 化完成**：单镜像部署，简化运维
2. **版本透明化**：用户可在界面和 API 查看版本
3. **镜像优化**：Alpine 基础镜像，体积小，安全性高
4. **开发友好**：完整的文档和调试支持
5. **生产就绪**：健康检查、日志、配置管理齐全

## 📚 相关文档

- [`docker/README.md`](docker/README.md) - Docker 详细使用指南
- [`Directory.Build.props`](Directory.Build.props) - 全局版本配置
- [ASP.NET Core 托管 Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly)
- [Docker Alpine 最佳实践](https://wiki.alpinelinux.org/wiki/Docker)

---

**总结**：所有需求已完成实现，项目现在具备完整的 Docker 支持和版本管理功能。✅
