# 自定义工具 + 依赖注入 (DI) 最佳实践

## 概述

本文档说明如何在 **Microsoft Agent Framework** 中创建支持依赖注入 (DI) 的自定义工具，并在 **ChatClientAgent** 中使用。

---

## 为什么需要 DI？

在实际项目中，工具通常需要访问：
- **数据库** (`DbContext`)
- **HTTP 客户端** (`IHttpClientFactory`)
- **日志服务** (`ILogger<T>`)
- **配置** (`IConfiguration`)
- **其他业务服务** (如 `IDevicePushService`, `IAzureOpenAIService`)

使用 DI 可以：
✅ 避免硬编码依赖  
✅ 支持单元测试 (Mock 依赖)  
✅ 统一管理服务生命周期  
✅ 代码更清晰、可维护  

---

## ChatClientAgent Tool 创建方式（5 种）

### 1. **AIFunctionFactory.Create 从方法创建**
```csharp
static string GetWeather(string location) => $"Weather in {location}";
var tool = AIFunctionFactory.Create(GetWeather);
```

### 2. **AIFunctionFactory.Create 从插件类创建** ⭐ **推荐**
```csharp
public class WeatherPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public WeatherPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [Description("Get current weather")]
    public async Task<string> GetWeatherAsync(string location)
    {
        var client = _httpClientFactory.CreateClient();
        // 调用 API...
        return "Sunny";
    }
}

// 使用
var plugin = serviceProvider.GetRequiredService<WeatherPlugin>();
var tool = AIFunctionFactory.Create(plugin.GetWeatherAsync);
```

### 3. **AIFunctionFactory.CreateDeclaration 仅声明（无实现）**
```csharp
var tool = AIFunctionFactory.CreateDeclaration(
    name: "get_weather",
    description: "Get weather info",
    jsonSchema: schema);
```

### 4. **自定义 AITool 子类**
```csharp
public class CustomAITool : AITool
{
    public override AIFunctionMetadata Metadata { get; }
    // 实现自定义逻辑...
}
```

### 5. **传递 tools 给 ChatClientAgent 自动注入**
```csharp
var agent = new ChatClientAgent(
    chatClient,
    tools: [tool1, tool2],
    services: serviceProvider);  // ✅ 传入 DI 容器
```

---

## 推荐方案: 插件类 + DI

### 步骤 1: 创建工具插件类

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

public class WeatherToolPlugin
{
    private readonly ILogger<WeatherToolPlugin> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    // ✅ 构造函数注入依赖
    public WeatherToolPlugin(
        ILogger<WeatherToolPlugin> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// 工具方法 - 使用 Description 特性自动生成 Tool Schema
    /// </summary>
    [Description("Get current weather for a location")]
    public async Task<string> GetWeatherAsync(
        [Description("Location name")] string location,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting weather for {Location}", location);

        var apiKey = _configuration["WeatherApi:Key"];
        var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient.GetStringAsync(
            $"https://api.weatherapi.com/v1/current.json?key={apiKey}&q={location}",
            cancellationToken);

        return response;
    }

    [Description("Get weather forecast")]
    public async Task<string> GetForecastAsync(
        [Description("Location name")] string location,
        [Description("Number of days (1-7)")] int days = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting forecast for {Location}", location);
        // 实现逻辑...
        return $"Forecast for {location}: {days} days";
    }
}
```

### 步骤 2: 在 DI 容器中注册插件

```csharp
// Program.cs
builder.Services.AddScoped<WeatherToolPlugin>();
builder.Services.AddScoped<ImageToolPlugin>();
builder.Services.AddHttpClient();  // 如果需要 HttpClient
```

### 步骤 3: 在 WorkflowManager 中创建工具

```csharp
public async Task<Workflow> CreateWorkflowAsync(
    Guid chatRoomId,
    CancellationToken cancellationToken = default)
{
    using var scope = _serviceScopeFactory.CreateScope();
    var serviceProvider = scope.ServiceProvider;

    // 1. 加载智能体配置
    var agents = await LoadAgentsAsync(chatRoomId, cancellationToken);

    // 2. 为每个智能体创建工具
    var specialistAgents = new List<ChatClientAgent>();
    
    foreach (var agent in agents)
    {
        // ✅ 从 DI 容器创建插件实例
        var tools = new List<AITool>();
        
        if (agent.Capabilities.Contains("weather"))
        {
            var weatherPlugin = serviceProvider.GetRequiredService<WeatherToolPlugin>();
            tools.Add(AIFunctionFactory.Create(weatherPlugin.GetWeatherAsync));
            tools.Add(AIFunctionFactory.Create(weatherPlugin.GetForecastAsync));
        }

        if (agent.Capabilities.Contains("image"))
        {
            var imagePlugin = serviceProvider.GetRequiredService<ImageToolPlugin>();
            tools.Add(AIFunctionFactory.Create(imagePlugin.GenerateImageAsync));
        }

        // ✅ 创建智能体时传递 tools 和 services
        var chatAgent = new ChatClientAgent(
            _chatClient,
            instructions: agent.SystemPrompt,
            name: agent.AgentId,
            description: agent.Personality,
            tools: tools,  // 传递工具
            services: serviceProvider);  // 传递 DI 容器

        specialistAgents.Add(chatAgent);
    }

    // 3. 构建 Workflow
    var triageAgent = new ChatClientAgent(_chatClient, "Triage", "triage");
    var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);
    builder.WithHandoffs(triageAgent, specialistAgents);
    builder.WithHandoffs(specialistAgents, triageAgent);

    return builder.Build();
}
```

---

## 高级用法: 使用 AIFunctionContext

如果需要在静态方法中访问服务：

```csharp
public class DatabaseToolPlugin
{
    [Description("Query user data")]
    public static async Task<string> QueryUserDataAsync(
        [Description("User ID")] string userId,
        AIFunctionContext context,  // ✅ 注入 context
        CancellationToken cancellationToken = default)
    {
        // 从 context 中获取服务
        var dbContext = context.GetService<McpDbContext>();
        var logger = context.GetService<ILogger<DatabaseToolPlugin>>();

        if (dbContext == null)
        {
            throw new InvalidOperationException("DbContext not available");
        }

        logger?.LogInformation("Querying user {UserId}", userId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user != null ? $"User: {user.Name}" : "User not found";
    }
}
```

**关键点:**
- ✅ `AIFunctionContext` 由 `FunctionInvokingChatClient` 自动注入
- ✅ 前提: `ChatClientAgent` 构造时传递了 `services` 参数
- ✅ 适用于静态方法或无法使用构造函数注入的场景

---

## 对比: MCP Tools vs 自定义 Tools

| 特性 | MCP Tools | 自定义 Tools |
|------|-----------|-------------|
| **创建方式** | 通过 MCP Server 动态发现 | 代码中显式创建 |
| **DI 支持** | 通过 `UserContext` 转发 | 直接注入依赖 |
| **类型安全** | ❌ 动态解析 | ✅ 编译时检查 |
| **性能** | 较慢 (HTTP 调用) | 快 (本地调用) |
| **灵活性** | 高 (可动态更新) | 中 (需重新编译) |
| **适用场景** | 外部工具集成 | 核心业务逻辑 |

**建议:**
- 外部/第三方工具 → 使用 **MCP Tools**
- 核心业务逻辑 → 使用 **自定义 Tools**
- 混合使用:
  ```csharp
  var tools = new List<AITool>();
  
  // 自定义工具 (本地)
  tools.AddRange(customTools);
  
  // MCP 工具 (外部)
  tools.AddRange(await _mcpToolService.GetToolsForCapabilitiesAsync(capabilities));
  ```

---

## 完整示例代码

完整代码请参考:
- [CustomToolExample.cs](../src/Verdure.Mcp.Server/Services/CustomToolExample.cs)

---

## 常见问题

### Q1: 为什么工具中无法获取服务？
**A:** 确保在创建 `ChatClientAgent` 时传递了 `services` 参数：
```csharp
new ChatClientAgent(chatClient, ..., services: serviceProvider)
```

### Q2: 如何在工具中使用 Scoped 服务（如 DbContext）？
**A:** 有两种方式：
1. 插件类使用 `Scoped` 生命周期
2. 在工具方法中通过 `AIFunctionContext` 获取

### Q3: 工具方法可以是异步的吗？
**A:** ✅ 可以，推荐使用 `async Task<T>`

### Q4: 如何传递 CancellationToken？
**A:** 在工具方法签名中添加 `CancellationToken` 参数：
```csharp
public async Task<string> MyToolAsync(
    string input,
    CancellationToken cancellationToken = default)
```

### Q5: 如何动态更新工具？
**A:** 清除 WorkflowManager 的缓存：
```csharp
_workflowManager.ClearWorkflowCache(chatRoomId);
```

---

## 参考资料

- [Microsoft Agent Framework 官方文档](https://github.com/microsoft/agent-framework)
- [AIFunctionFactory 源码](https://github.com/microsoft/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI.Abstractions)
- [ChatClientAgent 源码](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs)
- [FunctionInvokingChatClient 源码](https://github.com/microsoft/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs)

---

## 更新日志

| 日期 | 版本 | 更新内容 |
|------|------|----------|
| 2026-02-05 | v1.0 | 初始版本 |
