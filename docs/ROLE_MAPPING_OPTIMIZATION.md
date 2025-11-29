# Keycloak 角色映射优化总结

## 📋 优化内容

基于 [verdure-mcp-for-xiaozhi](https://github.com/maker-community/verdure-mcp-for-xiaozhi) 项目的实现,优化了当前项目的 Keycloak 角色映射逻辑。

## 🔄 主要改进

### 1. **创建统一的角色映射扩展方法**

**文件**: `src/Verdure.Mcp.Server/Extensions/AuthenticationExtensions.cs`

新增 `MapKeycloakRolesToStandardRoles` 方法,支持:
- ✅ **resource_access** 映射 (客户端级别角色)
- ✅ **realm_access** 映射 (领域级别角色)
- ✅ 自动过滤 Keycloak 默认角色
- ✅ 详细的日志记录
- ✅ 错误处理

### 2. **简化 Program.cs 中的角色映射逻辑**

**优化前**:
```csharp
// 手动解析 realm_access,代码冗长,只支持领域角色
options.Events = new JwtBearerEvents
{
    OnTokenValidated = context =>
    {
        var realmAccessClaim = context.Principal?.FindFirst("realm_access");
        // ... 30+ 行代码
        return Task.CompletedTask;
    }
};
```

**优化后**:
```csharp
// 使用扩展方法,简洁清晰,支持客户端和领域角色
options.Events = new JwtBearerEvents
{
    OnTokenValidated = context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();
        
        return AuthenticationExtensions.MapKeycloakRolesToStandardRoles(
            context,
            clientId: keycloakSettings.ClientId,
            logger: logger);
    }
};
```

## 🎯 支持的角色来源

### 1. Client Roles (resource_access)
从 Access Token 的 `resource_access.{clientId}.roles` 中提取:

```json
{
  "resource_access": {
    "verdure-mcp": {
      "roles": ["Admin", "User"]
    }
  }
}
```

### 2. Realm Roles (realm_access)
从 Access Token 的 `realm_access.roles` 中提取,并过滤默认角色:

```json
{
  "realm_access": {
    "roles": ["admin", "user", "offline_access", "uma_authorization"]
  }
}
```

**过滤规则**: 排除以下默认角色
- `offline_access`
- `uma_authorization`
- `default-roles-verdure-mcp`
- `default-roles-maker-community`

## 📊 角色映射流程

```
┌─────────────────────────────────────────┐
│   Keycloak Access Token (JWT)          │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  1. Extract resource_access (客户端角色)│
│     - verdure-mcp.roles: ["Admin"]     │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  2. Extract realm_access (领域角色)     │
│     - Filter out default roles         │
│     - Keep: ["admin", "user"]          │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  3. Map to ClaimTypes.Role              │
│     - Add to ClaimsIdentity             │
│     - Deduplicate existing claims       │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  4. Log mapped roles                    │
│     User authenticated with roles:      │
│     Admin, admin, user                  │
└─────────────────────────────────────────┘
```

## 🔍 日志示例

**成功映射**:
```
info: Mapping 1 roles from resource_access.verdure-mcp: Admin
info: Mapping 2 realm roles (filtered from 4): admin, user
info: User sub123 authenticated with roles: Admin, admin, user
```

**配置问题警告**:
```
warn: ClientId 'wrong-client' not found in resource_access. Available clients: verdure-mcp, account
warn: User sub123 has no roles mapped - check token claims and configuration
```

## ⚙️ 大小写兼容性

### 前端 (Blazor)
- ✅ `IsAdmin()` 扩展方法使用 `StringComparison.OrdinalIgnoreCase`
- ✅ `AdminPolicy` 策略不区分大小写

### 后端 (API)
- ✅ `AdminPolicy` 策略不区分大小写检查
- ✅ 支持 `admin`, `Admin`, `ADMIN` 等任意大小写

## 🚀 优势对比

| 特性 | 优化前 | 优化后 |
|------|--------|--------|
| 支持 resource_access | ❌ | ✅ |
| 支持 realm_access | ✅ | ✅ |
| 过滤默认角色 | ❌ | ✅ |
| 详细日志记录 | ❌ | ✅ |
| 错误处理 | 基础 | 完善 |
| 代码可维护性 | 低 | 高 |
| 可复用性 | 低 | 高 |

## 📚 参考项目

实现参考: [maker-community/verdure-mcp-for-xiaozhi](https://github.com/maker-community/verdure-mcp-for-xiaozhi/blob/main/src/Verdure.McpPlatform.Api/Extensions/AuthenticationExtensions.cs)

## ✅ 测试建议

1. **测试不同角色来源**
   ```powershell
   # 测试 resource_access 角色
   # 在 Keycloak 中配置客户端角色 "Admin"
   
   # 测试 realm_access 角色  
   # 在 Keycloak 中配置领域角色 "admin"
   ```

2. **测试大小写兼容**
   ```
   - admin (小写)
   - Admin (首字母大写)
   - ADMIN (全大写)
   ```

3. **检查日志输出**
   ```powershell
   # 启动应用并登录,查看控制台日志
   dotnet run --project src/Verdure.Mcp.Server
   ```

## 🔧 配置要求

确保 `appsettings.json` 或 `appsettings.Development.json` 中配置了正确的 ClientId:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080",
    "Realm": "verdure-mcp",
    "ClientId": "verdure-mcp",  // ⚠️ 必须与 Keycloak 中的客户端 ID 匹配
    "Audience": "verdure-mcp",
    "RequireHttpsMetadata": false
  }
}
```

---

**更新日期**: 2025-11-29  
**优化版本**: v2.0
