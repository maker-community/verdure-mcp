# ClaimsPrincipal 扩展方法使用指南

## 概述

`ClaimsPrincipalExtensions` 提供了一组扩展方法,简化了从 `ClaimsPrincipal` 中提取用户信息和检查角色的操作。这些方法封装了常见的 Claims 操作模式,使代码更简洁、可读性更好。

## 位置

```
src/Verdure.Mcp.Web/Extensions/ClaimsPrincipalExtensions.cs
```

## 扩展方法列表

### 1. 用户信息提取

#### GetUserId()
获取用户的唯一标识符。

```csharp
public static string GetUserId(this ClaimsPrincipal user)
```

**查找顺序**:
1. `ClaimTypes.NameIdentifier` (标准 ASP.NET)
2. `"sub"` (OpenID Connect 标准)

**抛出异常**: 如果找不到用户 ID

**使用示例**:
```csharp
@code {
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    private async Task<string> GetCurrentUserId()
    {
        var authState = await AuthenticationState!;
        var user = authState.User;
        
        try
        {
            return user.GetUserId();
        }
        catch (InvalidOperationException)
        {
            // 用户未认证或没有 ID claim
            return "未知";
        }
    }
}
```

#### GetUsername()
获取用户名。

```csharp
public static string GetUsername(this ClaimsPrincipal user)
```

**查找顺序**:
1. `ClaimTypes.Name`
2. `"preferred_username"` (Keycloak)
3. `"name"`
4. 默认: `"Unknown"`

**使用示例**:
```razor
<AuthorizeView>
    <Authorized>
        <MudText>欢迎, @context.User.GetUsername()</MudText>
    </Authorized>
</AuthorizeView>
```

#### GetEmail()
获取用户邮箱地址。

```csharp
public static string GetEmail(this ClaimsPrincipal user)
```

**查找顺序**:
1. `ClaimTypes.Email`
2. `"email"`
3. 默认: `string.Empty`

**使用示例**:
```csharp
private string GetUserEmail(ClaimsPrincipal user)
{
    var email = user.GetEmail();
    return string.IsNullOrEmpty(email) ? "未设置邮箱" : email;
}
```

### 2. 角色管理

#### GetRoles()
获取用户的所有角色列表。

```csharp
public static List<string> GetRoles(this ClaimsPrincipal user)
```

**特点**:
- 自动使用正确的 `RoleClaimType`
- 返回所有角色的列表

**使用示例**:
```csharp
private string GetRoles(ClaimsPrincipal user)
{
    var roles = user.GetRoles();
    return roles.Any() ? string.Join(", ", roles) : "普通用户";
}
```

```razor
<MudList>
    @foreach (var role in context.User.GetRoles())
    {
        <MudListItem>
            <MudChip Color="Color.Primary">@role</MudChip>
        </MudListItem>
    }
</MudList>
```

#### HasAnyRole()
检查用户是否拥有任意一个指定角色。

```csharp
public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
```

**使用示例**:
```razor
@if (context.User.HasAnyRole("admin", "moderator"))
{
    <MudButton>管理操作</MudButton>
}
```

```csharp
// 在代码中使用
if (user.HasAnyRole("admin", "editor", "author"))
{
    // 允许编辑内容
}
```

#### HasAllRoles()
检查用户是否拥有所有指定角色。

```csharp
public static bool HasAllRoles(this ClaimsPrincipal user, params string[] roles)
```

**使用示例**:
```csharp
// 要求用户同时具有 admin 和 super-user 角色
if (user.HasAllRoles("admin", "super-user"))
{
    // 执行超级管理员操作
}
```

#### IsAdmin()
快捷方法,检查用户是否是管理员。

```csharp
public static bool IsAdmin(this ClaimsPrincipal user)
```

**检查角色**: `"admin"` 或 `"Admin"` (不区分大小写)

**使用示例**:
```razor
<AuthorizeView>
    <Authorized>
        @if (context.User.IsAdmin())
        {
            <MudNavLink Href="/admin/services">
                <MudIcon Icon="@Icons.Material.Filled.Settings" /> 系统管理
            </MudNavLink>
        }
    </Authorized>
</AuthorizeView>
```

### 3. Claim 操作

#### GetClaimValue()
获取指定类型的单个 Claim 值。

```csharp
public static string? GetClaimValue(this ClaimsPrincipal user, string claimType)
```

**使用示例**:
```csharp
var organization = user.GetClaimValue("organization");
var department = user.GetClaimValue("department");
var tenantId = user.GetClaimValue("tenant_id");
```

#### GetClaimValues()
获取指定类型的所有 Claim 值。

```csharp
public static List<string> GetClaimValues(this ClaimsPrincipal user, string claimType)
```

**使用场景**: 当一个用户可能有多个相同类型的 Claims 时

**使用示例**:
```csharp
// 获取用户的所有组织
var organizations = user.GetClaimValues("organization");

// 获取所有权限
var permissions = user.GetClaimValues("permission");

foreach (var permission in permissions)
{
    Console.WriteLine($"Permission: {permission}");
}
```

## 完整使用示例

### 示例 1: Profile 页面

```razor
@page "/profile"
@attribute [Authorize]
@using Verdure.Mcp.Web.Extensions

<AuthorizeView>
    <Authorized>
        <MudPaper Class="pa-6">
            <MudStack Spacing="4">
                <!-- 用户基本信息 -->
                <MudText Typo="Typo.h5">@context.User.GetUsername()</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">
                    @context.User.GetEmail()
                </MudText>
                
                <!-- 用户 ID -->
                <MudDivider />
                <MudText Typo="Typo.body2">
                    <strong>用户 ID:</strong> @GetSafeUserId(context.User)
                </MudText>
                
                <!-- 角色列表 -->
                <MudDivider />
                <MudText Typo="Typo.body2"><strong>角色:</strong></MudText>
                <MudStack Row="true" Spacing="1">
                    @foreach (var role in context.User.GetRoles())
                    {
                        <MudChip Size="Size.Small" Color="Color.Primary">@role</MudChip>
                    }
                </MudStack>
                
                <!-- 管理员特殊功能 -->
                @if (context.User.IsAdmin())
                {
                    <MudDivider />
                    <MudAlert Severity="Severity.Info">
                        您具有管理员权限
                    </MudAlert>
                    <MudButton Variant="Variant.Filled" Color="Color.Primary"
                               Href="/admin/services">
                        进入管理控制台
                    </MudButton>
                }
            </MudStack>
        </MudPaper>
    </Authorized>
</AuthorizeView>

@code {
    private string GetSafeUserId(ClaimsPrincipal user)
    {
        try
        {
            return user.GetUserId();
        }
        catch
        {
            return "未知";
        }
    }
}
```

### 示例 2: 条件渲染菜单

```razor
@using Verdure.Mcp.Web.Extensions

<MudNavMenu>
    <!-- 所有用户可见 -->
    <MudNavLink Href="/" Icon="@Icons.Material.Filled.Home">
        首页
    </MudNavLink>
    
    <AuthorizeView>
        <Authorized>
            <!-- 认证用户可见 -->
            <MudNavLink Href="/profile" Icon="@Icons.Material.Filled.Person">
                个人中心
            </MudNavLink>
            
            <!-- 管理员或编辑者可见 -->
            @if (context.User.HasAnyRole("admin", "editor"))
            {
                <MudNavLink Href="/content/manage" Icon="@Icons.Material.Filled.Edit">
                    内容管理
                </MudNavLink>
            }
            
            <!-- 仅管理员可见 -->
            @if (context.User.IsAdmin())
            {
                <MudDivider Class="my-2" />
                <MudText Typo="Typo.subtitle2" Class="px-4">管理</MudText>
                <MudNavLink Href="/admin/services" Icon="@Icons.Material.Filled.Settings">
                    服务管理
                </MudNavLink>
                <MudNavLink Href="/admin/users" Icon="@Icons.Material.Filled.People">
                    用户管理
                </MudNavLink>
            }
        </Authorized>
    </AuthorizeView>
</MudNavMenu>
```

### 示例 3: 在服务中使用

```csharp
using System.Security.Claims;
using Verdure.Mcp.Web.Extensions;

public class UserContextService
{
    private readonly AuthenticationStateProvider _authStateProvider;

    public UserContextService(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    public async Task<UserContext> GetCurrentUserContextAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        return new UserContext
        {
            UserId = user.GetUserId(),
            Username = user.GetUsername(),
            Email = user.GetEmail(),
            Roles = user.GetRoles(),
            IsAdmin = user.IsAdmin()
        };
    }

    public async Task<bool> HasPermissionAsync(params string[] requiredRoles)
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.HasAnyRole(requiredRoles);
    }
}

public class UserContext
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = [];
    public bool IsAdmin { get; init; }
}
```

## 与 Keycloak 角色映射的配合

扩展方法会自动使用 `KeycloakRoleClaimsPrincipalFactory` 映射的角色:

```csharp
// KeycloakRoleClaimsPrincipalFactory 已将角色映射为标准 Claims
// GetRoles() 自动读取正确的 RoleClaimType

var roles = user.GetRoles();
// 返回: ["admin", "developer", "user"]

// 直接使用角色检查
if (user.IsAdmin())
{
    // admin 角色已由 Keycloak 映射
}
```

## 错误处理

### GetUserId() 异常处理

```csharp
try
{
    var userId = user.GetUserId();
    // 使用 userId
}
catch (InvalidOperationException ex)
{
    // 用户没有 ID claim
    _logger.LogWarning("User ID not found: {Message}", ex.Message);
    return "未知用户";
}
```

### 安全的角色检查

```csharp
// HasAnyRole 和 HasAllRoles 内置了空检查
// 即使 user 为 null 或未认证,也会安全返回 false
if (user.HasAnyRole("admin")) // 不会抛出异常
{
    // 安全执行
}
```

## 最佳实践

### 1. 使用 using 指令

在文件顶部添加:
```razor
@using Verdure.Mcp.Web.Extensions
```

或在 `_Imports.razor` 中全局添加:
```razor
@using Verdure.Mcp.Web.Extensions
```

### 2. 优先使用扩展方法

**不推荐**:
```csharp
var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? user.FindFirst("sub")?.Value
    ?? "unknown";
```

**推荐**:
```csharp
try
{
    var userId = user.GetUserId();
}
catch
{
    var userId = "unknown";
}
```

### 3. 角色检查的语义化

**不推荐**:
```csharp
var roles = user.GetRoles();
if (roles.Contains("admin") || roles.Contains("moderator"))
{
    // ...
}
```

**推荐**:
```csharp
if (user.HasAnyRole("admin", "moderator"))
{
    // ...
}
```

### 4. 管理员检查

**不推荐**:
```csharp
if (user.IsInRole("admin") || user.IsInRole("Admin"))
```

**推荐**:
```csharp
if (user.IsAdmin())
```

## 测试

### 单元测试示例

```csharp
using System.Security.Claims;
using Verdure.Mcp.Web.Extensions;
using Xunit;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ShouldReturnUserId_WhenSubClaimExists()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("name", "Test User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var userId = user.GetUserId();

        // Assert
        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void HasAnyRole_ShouldReturnTrue_WhenUserHasOneOfRoles()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "user"),
            new Claim(ClaimTypes.Role, "editor")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = user.HasAnyRole("admin", "editor");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAdmin_ShouldReturnTrue_WhenUserIsAdmin()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, "admin") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act & Assert
        Assert.True(user.IsAdmin());
    }
}
```

## 相关文件

- `src/Verdure.Mcp.Web/Extensions/ClaimsPrincipalExtensions.cs` - 扩展方法实现
- `src/Verdure.Mcp.Web/Services/KeycloakRoleClaimsPrincipalFactory.cs` - 角色映射工厂
- `src/Verdure.Mcp.Web/Pages/Profile.razor` - 使用示例
- `docs/ROLE_PARSING.md` - 角色解析文档

## 总结

`ClaimsPrincipalExtensions` 提供了一套简洁、类型安全的 API,用于处理用户身份和角色。通过使用这些扩展方法,你可以:

- ✅ 减少重复代码
- ✅ 提高代码可读性
- ✅ 统一 Claims 访问模式
- ✅ 简化角色检查逻辑
- ✅ 更好的错误处理
