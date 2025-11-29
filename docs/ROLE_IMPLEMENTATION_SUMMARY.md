# 角色解析实现总结

## 完成的工作

### ✅ 1. 创建 KeycloakRoleClaimsPrincipalFactory
**位置**: `src/Verdure.Mcp.Web/Services/KeycloakRoleClaimsPrincipalFactory.cs`

这是一个自定义的 Claims Principal 工厂类,用于从 Keycloak 的 Access Token 中提取和映射用户角色。

**核心功能**:
- 从 Access Token (而非 ID Token) 中解析角色信息
- 支持 `resource_access.<clientId>.roles` 客户端级别角色
- 支持 `realm_access.roles` Realm 级别角色
- 自动过滤 Keycloak 默认角色
- 详细的日志记录,便于调试

### ✅ 2. 更新 Program.cs
**位置**: `src/Verdure.Mcp.Web/Program.cs`

注册自定义的角色解析工厂:

```csharp
builder.Services.AddOidcAuthentication(options => { ... })
    .AddAccountClaimsPrincipalFactory<KeycloakRoleClaimsPrincipalFactory>();
```

### ✅ 3. 添加依赖包
**修改文件**:
- `Directory.Packages.props` - 添加 `System.IdentityModel.Tokens.Jwt` 版本定义
- `Verdure.Mcp.Web.csproj` - 添加包引用

### ✅ 4. 创建文档
**位置**: `docs/ROLE_PARSING.md`

详细的角色解析机制文档,包括:
- 实现原理
- 配置说明
- 使用示例
- 调试技巧
- 常见问题排查

## 关键改进点

### 🎯 1. 从 Access Token 提取角色
原因:Keycloak 的角色信息通常在 Access Token 中,而非 ID Token

```csharp
var tokenProvider = _accessor.TokenProvider;
var tokenResult = await tokenProvider.RequestAccessToken();
if (tokenResult.TryGetToken(out var accessToken))
{
    var handler = new JwtSecurityTokenHandler();
    var jwtToken = handler.ReadJwtToken(accessToken.Value);
    // 处理角色...
}
```

### 🎯 2. 双层角色映射

**Client 角色** (优先):
```json
{
  "resource_access": {
    "verdure-mcp-server": {
      "roles": ["admin", "developer"]
    }
  }
}
```

**Realm 角色** (可选):
```json
{
  "realm_access": {
    "roles": ["premium-user"]
  }
}
```

### 🎯 3. 智能过滤
自动过滤 Keycloak 的系统角色:
- `offline_access`
- `uma_authorization`
- `default-roles-*`

### 🎯 4. 详细日志
使用 emoji 标记的结构化日志:
```
🔐 Mapping Keycloak roles for user john with ClientId verdure-mcp-server
✅ Access token obtained, parsing...
📋 Extracted 2 roles: admin, developer
➕ Added role claim: admin (ClaimType: http://schemas.microsoft.com/ws/2008/06/identity/claims/role)
✅ User john authenticated with roles: admin, developer
```

## 使用方式

### 在 Razor 组件中使用角色授权

```razor
<AuthorizeView Roles="admin">
    <Authorized>
        <MudNavLink Href="/admin/services">
            MCP 服务管理
        </MudNavLink>
    </Authorized>
</AuthorizeView>
```

### 在页面中使用

```csharp
@page "/admin/services"
@attribute [Authorize(Roles = "admin")]
```

### 程序化检查角色

```csharp
var roles = user.FindAll(ClaimTypes.Role)
    .Select(c => c.Value)
    .ToList();

if (roles.Contains("admin"))
{
    // 管理员操作
}
```

## 配置要求

### appsettings.json
```json
{
  "Keycloak": {
    "Authority": "https://auth.verdure-hiro.cn/realms/maker-community",
    "ClientId": "verdure-mcp-server",  // ⚠️ 必须与 Keycloak 配置一致
    "ResponseType": "code"
  }
}
```

### Keycloak 配置

1. **创建 Client**: `verdure-mcp-server`
2. **配置角色**: 在 Client Roles 中创建 `admin`, `user` 等角色
3. **分配角色**: 为用户分配相应的角色

## 调试检查清单

当角色不生效时,按以下顺序检查:

1. ✅ 查看浏览器控制台的日志输出
2. ✅ 访问 `/profile` 页面查看当前用户的角色
3. ✅ 确认 `appsettings.json` 中的 `ClientId` 配置正确
4. ✅ 确认用户在 Keycloak 中已分配角色
5. ✅ 检查角色是分配在 Client 还是 Realm 级别
6. ✅ 查看日志中的 "Available clients:" 信息

## 测试建议

### 1. 创建测试用户
在 Keycloak 中创建不同角色的用户:
- 普通用户 (无特殊角色)
- 管理员用户 (admin 角色)
- 开发者用户 (developer 角色)

### 2. 验证授权
- 普通用户不应看到管理菜单
- 管理员应该能访问 `/admin/*` 路径
- 所有认证用户都应能访问 `/profile` 和 `/tokens`

### 3. 检查日志
启用详细日志级别,查看角色映射过程

## 与参考项目的对比

参考项目: `verdure-mcp-for-xiaozhi`

**相同点**:
- ✅ 从 Access Token 提取角色
- ✅ 支持 resource_access 和 realm_access
- ✅ 详细的日志记录
- ✅ 角色过滤机制

**适配修改**:
- 🔧 排除角色列表中添加 `default-roles-verdure-mcp`
- 🔧 默认 ClientId 使用 `verdure-mcp-server`
- 🔧 日志消息适配当前项目上下文

## 下一步建议

1. **测试**: 创建测试用户并验证角色授权
2. **监控**: 观察生产环境的日志,确保角色正确映射
3. **优化**: 根据实际需求调整过滤规则
4. **扩展**: 可以添加更多自定义 Claims (如组织、部门等)

## 相关文件

- `src/Verdure.Mcp.Web/Services/KeycloakRoleClaimsPrincipalFactory.cs` - 核心实现
- `src/Verdure.Mcp.Web/Program.cs` - 服务注册
- `src/Verdure.Mcp.Web/Pages/Profile.razor` - 角色显示示例
- `src/Verdure.Mcp.Web/Layout/NavMenu.razor` - 角色授权示例
- `docs/ROLE_PARSING.md` - 详细文档
