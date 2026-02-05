using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Domain.Entities;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 自定义工具示例 - 演示如何使用 DI 创建支持依赖注入的工具
/// 适用于 Microsoft Agent Framework (ChatClientAgent)
/// </summary>
public class CustomToolExample
{
    // ==================== 方案 1: 使用插件类 (推荐) ====================
    /// <summary>
    /// 工具插件类 - 通过构造函数注入依赖服务
    /// 
    /// 使用示例:
    /// 1. 注册服务: services.AddScoped<WeatherToolPlugin>();
    /// 2. 创建工具: var plugin = serviceProvider.GetRequiredService<WeatherToolPlugin>();
    ///              var tool = AIFunctionFactory.Create(plugin.GetWeatherAsync);
    /// 3. 传递给 Agent: new ChatClientAgent(chatClient, tools: [tool], services: serviceProvider);
    /// </summary>
    public class WeatherToolPlugin
    {
        private readonly ILogger<WeatherToolPlugin> _logger;

        // ✅ 构造函数注入 - 支持任意 DI 服务
        public WeatherToolPlugin(ILogger<WeatherToolPlugin> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 工具方法 - 使用 Description 特性生成 Tool Schema
        /// </summary>
        [Description("Get current weather for a location")]
        public Task<string> GetWeatherAsync(
            [Description("Location name, e.g. 'Beijing' or 'New York'")] string location,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting weather for {Location}", location);

            // 模拟天气数据 (实际项目中调用真实 API)
            var weather = location.ToLower() switch
            {
                "beijing" => "Sunny, 15°C",
                "shanghai" => "Cloudy, 20°C",
                "new york" => "Rainy, 10°C",
                _ => "Unknown location"
            };

            return Task.FromResult($"Weather in {location}: {weather}");
        }

        [Description("Get 7-day weather forecast")]
        public Task<string> GetForecastAsync(
            [Description("Location name")] string location,
            [Description("Number of days (1-7)")] int days = 3,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting {Days}-day forecast for {Location}", days, location);
            return Task.FromResult($"Forecast for {location}: Next {days} days will be mostly sunny");
        }
    }

    // ==================== 方案 2: 访问数据库的工具 ====================
    /// <summary>
    /// 访问数据库的工具插件 - 演示如何使用 DbContext
    /// ⚠️ 重要: 必须使用静态方法 + IServiceProvider 避免 DbContext 生命周期问题
    /// </summary>
    public class DatabaseToolPlugin
    {
        /// <summary>
        /// 获取用户设备数量
        /// </summary>
        /// <remarks>
        /// ✅ 使用静态方法 + IServiceProvider 模式
        /// - FunctionInvokingChatClient 在调用时自动注入 IServiceProvider
        /// - 从 serviceProvider 动态获取 DbContext (每次调用都是新实例)
        /// - 避免持有长生命周期的 DbContext 引用
        /// </remarks>
        [Description("Get user's device count")]
        public static async Task<string> GetUserDeviceCountAsync(
            [Description("User ID")] string userId,
            IServiceProvider serviceProvider,  // ✅ 自动注入
            CancellationToken cancellationToken = default)
        {
            // ✅ 从 serviceProvider 创建 scope 动态获取服务
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<McpDbContext>();
            var logger = scope.ServiceProvider.GetService<ILogger<DatabaseToolPlugin>>();

            logger?.LogInformation("Getting device count for user {UserId}", userId);

            var count = await dbContext.Devices
                .CountAsync(d => d.OwnerUserId == userId, cancellationToken);

            return $"User {userId} has {count} device(s)";
        }

        /// <summary>
        /// 列出用户的所有设备
        /// </summary>
        [Description("List user's devices")]
        public static async Task<string> ListUserDevicesAsync(
            [Description("User ID")] string userId,
            IServiceProvider serviceProvider,  // ✅ 自动注入
            CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<McpDbContext>();
            var logger = scope.ServiceProvider.GetService<ILogger<DatabaseToolPlugin>>();

            logger?.LogInformation("Listing devices for user {UserId}", userId);

            var devices = await dbContext.Devices
                .Where(d => d.OwnerUserId == userId)
                .Take(10)
                .ToListAsync(cancellationToken);

            if (!devices.Any())
            {
                return $"User {userId} has no devices";
            }

            var deviceList = string.Join("\n", devices.Select(d => 
                $"- Device {d.MacAddress} (Status: {d.Status})"));

            return $"Devices for user {userId}:\n{deviceList}";
        }
    }

    // ==================== 方案 3: 简单的计算工具（无需 DI）====================
    /// <summary>
    /// 不需要依赖注入的简单工具
    /// </summary>
    public class CalculatorToolPlugin
    {
        [Description("Add two numbers")]
        public Task<string> AddAsync(
            [Description("First number")] double a,
            [Description("Second number")] double b)
        {
            var result = a + b;
            return Task.FromResult($"{a} + {b} = {result}");
        }

        [Description("Multiply two numbers")]
        public Task<string> MultiplyAsync(
            [Description("First number")] double a,
            [Description("Second number")] double b)
        {
            var result = a * b;
            return Task.FromResult($"{a} × {b} = {result}");
        }
    }

    // ==================== 快速测试示例 ====================
    /// <summary>
    /// 快速创建带自定义工具的 Agent 用于测试
    /// </summary>
    public static class QuickStartExample
    {
        /// <summary>
        /// 示例 1: 创建带天气工具的 Agent
        /// </summary>
        public static ChatClientAgent CreateWeatherAgent(
            IChatClient chatClient,
            IServiceProvider serviceProvider)
        {
            // 1. 从 DI 获取插件
            var weatherPlugin = ActivatorUtilities.CreateInstance<WeatherToolPlugin>(serviceProvider);

            // 2. 创建工具
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(weatherPlugin.GetWeatherAsync),
                AIFunctionFactory.Create(weatherPlugin.GetForecastAsync)
            };

            // 3. 创建 Agent
            return new ChatClientAgent(
                chatClient,
                instructions: "You are a weather assistant. Use the weather tools to answer questions.",
                name: "WeatherAgent",
                description: "Provides weather information",
                tools: tools,
                services: serviceProvider);
        }

        /// <summary>
        /// 示例 2: 创建带数据库查询工具的 Agent
        /// </summary>
        public static ChatClientAgent CreateDatabaseAgent(
            IChatClient chatClient,
            IServiceProvider serviceProvider)
        {
            // ✅ 使用静态方法创建工具 (不需要从 DI 获取实例)
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(DatabaseToolPlugin.GetUserDeviceCountAsync),
                AIFunctionFactory.Create(DatabaseToolPlugin.ListUserDevicesAsync)
            };

            return new ChatClientAgent(
                chatClient,
                instructions: "You are a database assistant. Help users query their device information.",
                name: "DatabaseAgent",
                tools: tools,
                services: serviceProvider);  // ✅ 关键: 传递 serviceProvider
        }

        /// <summary>
        /// 示例 3: 创建多种工具的混合 Agent
        /// </summary>
        public static ChatClientAgent CreateMixedAgent(
            IChatClient chatClient,
            IServiceProvider serviceProvider)
        {
            // 获取各种插件
            var weatherPlugin = ActivatorUtilities.CreateInstance<WeatherToolPlugin>(serviceProvider);
            var calcPlugin = new CalculatorToolPlugin();

            // 组合多种工具
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(weatherPlugin.GetWeatherAsync),
                AIFunctionFactory.Create(calcPlugin.AddAsync),
                AIFunctionFactory.Create(calcPlugin.MultiplyAsync)
            };

            return new ChatClientAgent(
                chatClient,
                instructions: "You are a helpful assistant with weather and calculator tools.",
                name: "MixedAgent",
                tools: tools,
                services: serviceProvider);
        }
    }
}

// ==================== 注册服务扩展 ====================
/// <summary>
/// 注册自定义工具插件到 DI 容器 (必须在顶层命名空间)
/// 
/// 使用方法: 在 Program.cs 中调用 builder.Services.AddCustomTools();
/// </summary>
public static class CustomToolServiceCollectionExtensions
{
    public static IServiceCollection AddCustomTools(this IServiceCollection services)
    {
        // ✅ 注册需要构造函数注入的插件
        services.AddScoped<CustomToolExample.WeatherToolPlugin>();
        services.AddTransient<CustomToolExample.CalculatorToolPlugin>();
        
        // ❌ DatabaseToolPlugin 使用静态方法 + IServiceProvider，无需注册
        
        return services;
    }
}