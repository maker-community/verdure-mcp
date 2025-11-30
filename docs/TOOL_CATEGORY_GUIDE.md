# MCP 工具分类扩展指南

本指南说明如何向 Verdure MCP Server 添加新的工具类别。

## 架构概述

工具分类系统使用 `McpToolFilterService` 来管理不同类别的工具过滤逻辑。这个服务:

1. **集中管理** - 所有工具分类逻辑都在一个地方
2. **易于扩展** - 使用字典存储过滤规则,添加新类别只需几行代码
3. **类型安全** - 使用强类型的 `Func<string, bool>` 过滤器
4. **日志记录** - 自动记录过滤过程和结果

## 当前工具类别

| 类别 | 端点 | 过滤规则 | 工具 |
|------|------|----------|------|
| `all` | `/all/mcp` 或 `/mcp` | 接受所有工具 | 所有已注册的工具 |
| `image` | `/image/mcp` | 工具名包含 "image" | `generate_image`, `get_image_task_status` |
| `email` | `/email/mcp` | 工具名包含 "email" | `send_email` |

## 添加新工具类别

### 方法 1: 在代码中注册(推荐用于简单场景)

在 `Program.cs` 中注册新类别:

```csharp
// 在应用启动时注册新类别
using (var scope = app.Services.CreateScope())
{
    var filterService = scope.ServiceProvider.GetRequiredService<McpToolFilterService>();
    
    // 示例 1: 添加数据库工具类别
    filterService.RegisterCategory("database", 
        toolName => toolName.Contains("db", StringComparison.OrdinalIgnoreCase) || 
                    toolName.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("query", StringComparison.OrdinalIgnoreCase));
    
    // 示例 2: 添加文档工具类别
    filterService.RegisterCategory("document",
        toolName => toolName.Contains("doc", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("word", StringComparison.OrdinalIgnoreCase));
    
    // 示例 3: 添加存储工具类别
    filterService.RegisterCategory("storage",
        toolName => toolName.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("blob", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("upload", StringComparison.OrdinalIgnoreCase));
}
```

### 方法 2: 修改服务类(推荐用于永久性类别)

直接在 `McpToolFilterService.cs` 的构造函数中添加:

```csharp
private readonly Dictionary<string, Func<string, bool>> _categoryFilters = new()
{
    ["image"] = toolName => toolName.Contains("image", StringComparison.OrdinalIgnoreCase),
    ["email"] = toolName => toolName.Contains("email", StringComparison.OrdinalIgnoreCase),
    ["database"] = toolName => toolName.Contains("db", StringComparison.OrdinalIgnoreCase) || 
                               toolName.Contains("sql", StringComparison.OrdinalIgnoreCase),
    ["all"] = _ => true
};
```

### 方法 3: 从配置文件加载(推荐用于动态场景)

1. 在 `appsettings.json` 中定义类别:

```json
{
  "McpToolCategories": {
    "database": {
      "keywords": ["db", "sql", "query", "table"]
    },
    "document": {
      "keywords": ["doc", "pdf", "word", "excel"]
    },
    "storage": {
      "keywords": ["file", "blob", "upload", "download"]
    }
  }
}
```

2. 创建配置类:

```csharp
public class McpToolCategoriesSettings
{
    public Dictionary<string, CategoryDefinition> Categories { get; set; } = new();
}

public class CategoryDefinition
{
    public List<string> Keywords { get; set; } = new();
}
```

3. 在服务启动时加载:

```csharp
var categoriesConfig = builder.Configuration
    .GetSection("McpToolCategories")
    .Get<Dictionary<string, CategoryDefinition>>();

// 在应用启动后注册
using (var scope = app.Services.CreateScope())
{
    var filterService = scope.ServiceProvider.GetRequiredService<McpToolFilterService>();
    
    foreach (var (category, definition) in categoriesConfig)
    {
        filterService.RegisterCategory(category, toolName =>
            definition.Keywords.Any(keyword => 
                toolName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }
}
```

## 高级过滤场景

### 基于多个条件的过滤

```csharp
// 图片和视频工具
filterService.RegisterCategory("media",
    toolName => {
        var isImage = toolName.Contains("image", StringComparison.OrdinalIgnoreCase);
        var isVideo = toolName.Contains("video", StringComparison.OrdinalIgnoreCase);
        var isAudio = toolName.Contains("audio", StringComparison.OrdinalIgnoreCase);
        return isImage || isVideo || isAudio;
    });
```

### 基于工具属性的过滤

如果需要基于工具的其他属性(不仅仅是名称)进行过滤,可以修改 `McpToolFilterService`:

```csharp
// 修改过滤器签名以接受完整的工具对象
private readonly Dictionary<string, Func<McpServerTool, bool>> _categoryFilters = new()
{
    ["image"] = tool => tool.ProtocolTool.Name.Contains("image", StringComparison.OrdinalIgnoreCase),
    ["premium"] = tool => tool.ProtocolTool.Description?.Contains("[Premium]") ?? false,
    ["experimental"] = tool => tool.ProtocolTool.Description?.Contains("[Beta]") ?? false,
};

// 更新 FilterTools 方法
public List<McpServerTool> FilterTools(IEnumerable<McpServerTool> allTools, string category)
{
    // ... existing code ...
    
    var filtered = toolList.Where(tool => filterFunc(tool)).ToList();
    
    // ... rest of the method ...
}
```

### 组合多个类别

```csharp
// 支持组合类别,例如 "image+email"
filterService.RegisterCategory("image+email",
    toolName => {
        var isImage = toolName.Contains("image", StringComparison.OrdinalIgnoreCase);
        var isEmail = toolName.Contains("email", StringComparison.OrdinalIgnoreCase);
        return isImage || isEmail;
    });
```

## 完整示例:添加数据库工具类别

### 1. 创建数据库工具

```csharp
// src/Verdure.Mcp.Server/Tools/DatabaseQueryTool.cs
public class DatabaseQueryTool
{
    [Tool("execute_query", "Execute a SQL query")]
    public async Task<string> ExecuteQuery(
        [Parameter("sql", "The SQL query to execute")] string sql)
    {
        // Implementation
        return "Query executed";
    }
    
    [Tool("list_tables", "List all database tables")]
    public async Task<string> ListTables()
    {
        // Implementation
        return "Tables listed";
    }
}
```

### 2. 注册工具

```csharp
// Program.cs
builder.Services.AddMcpServer()
    .WithHttpTransport(options => { /* ... */ })
    .WithTools<GenerateImageTool>()
    .WithTools<EmailTool>()
    .WithTools<DatabaseQueryTool>(); // 新增
```

### 3. 注册类别

```csharp
// Program.cs - 在应用启动后
using (var scope = app.Services.CreateScope())
{
    var filterService = scope.ServiceProvider.GetRequiredService<McpToolFilterService>();
    
    filterService.RegisterCategory("database",
        toolName => toolName.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("table", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Contains("sql", StringComparison.OrdinalIgnoreCase));
}
```

### 4. 测试新端点

```bash
# 列出数据库工具
curl -X POST http://localhost:5000/database/mcp \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }'

# 应该只返回 execute_query 和 list_tables
```

### 5. 更新文档

在 `README.md` 中添加:

```markdown
| `/database/mcp` | 数据库工具 | 执行 SQL 查询和管理表 |
```

## 最佳实践

### 1. 命名约定
- 类别名称使用小写,用连字符分隔:`database-query`, `file-storage`
- 工具名称应包含类别关键词,便于过滤:`send_email`, `generate_image`

### 2. 工具分组策略
- **功能分组**: 按功能领域分组(图片、邮件、数据库)
- **权限分组**: 按权限级别分组(public、admin、premium)
- **资源分组**: 按资源类型分组(storage、compute、network)

### 3. 过滤器设计
- 使用宽松的匹配规则,避免遗漏工具
- 支持多个关键词,提高灵活性
- 考虑使用正则表达式处理复杂模式

### 4. 测试
- 为每个新类别编写测试
- 验证工具过滤的正确性
- 测试边界情况(空类别、未知类别)

### 5. 文档
- 更新 `README.md` 中的端点列表
- 在 `TESTING.md` 中添加测试示例
- 记录类别的用途和包含的工具

## 故障排除

### 类别不工作
1. 检查工具名称是否包含预期的关键词
2. 验证 `RegisterCategory` 是否在应用启动后调用
3. 查看日志确认过滤逻辑是否执行

### 工具未出现在预期类别
1. 使用 `/all/mcp` 端点确认工具已注册
2. 检查过滤器函数的逻辑
3. 启用调试日志查看详细信息

### 性能问题
1. 避免在过滤器中执行耗时操作
2. 考虑缓存过滤结果
3. 使用 `ILogger` 监控过滤性能

## 未来增强

### 可能的改进方向:

1. **动态工具发现** - 自动检测工具并分类
2. **基于标签的过滤** - 使用工具的元数据标签
3. **用户自定义类别** - 允许用户通过 API 创建类别
4. **类别继承** - 支持类别层次结构
5. **条件过滤** - 基于请求上下文动态过滤

## 总结

通过 `McpToolFilterService`,添加新工具类别变得简单直接:

1. ✅ 创建新工具并注册到 MCP Server
2. ✅ 使用 `RegisterCategory` 定义过滤规则
3. ✅ 客户端通过 `/{category}/mcp` 访问特定工具集
4. ✅ 更新文档说明新类别

这种设计使得工具分类管理集中化、可扩展且易于维护!
