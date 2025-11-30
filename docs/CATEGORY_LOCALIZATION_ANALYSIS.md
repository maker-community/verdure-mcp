# MCP 分类多语言支持分析报告

## 📋 概述

本文档分析当前系统中 MCP 服务分类的实现方式，以及支持页面二维码弹窗的多语言支持情况。

---

## 🔍 问题分析

### 1. 分类标识与展示

#### 当前实现方式

**数据库存储：**
- `McpService.Category` 字段存储英文 key（如 "image", "email", "document", "data", "ai"）
- 这是正确的设计 ✅

**后端服务层硬编码：**
```csharp
// 📁 src/Verdure.Mcp.Server/Services/McpServiceService.cs (Line 214-222)

private static string GetCategoryDisplayName(string category) => category.ToLower() switch
{
    "image" => "图片生成",      // ❌ 硬编码中文
    "email" => "邮件服务",      // ❌ 硬编码中文
    "document" => "文档处理",   // ❌ 硬编码中文
    "data" => "数据服务",       // ❌ 硬编码中文
    "ai" => "AI 服务",          // ❌ 硬编码中文
    _ => category
};
```

**问题：**
1. ❌ 硬编码中文在后端服务层
2. ❌ 无法支持多语言切换
3. ❌ `GetCategoriesAsync()` 返回的 `McpCategoryDto.DisplayName` 是硬编码的中文

#### 前端展示

**MCP 广场页面 (`Dashboard.razor`):**
```razor
<!-- Line 57 -->
@category.DisplayName (@category.ServiceCount)
```
- 展示的是后端返回的硬编码中文 `DisplayName` ❌

**服务详情页面 (`ServiceDetails.razor`):**
```razor
<!-- Line 51 -->
@_service.Category
```
- 直接展示数据库中的英文 key ❌
- 应该通过本地化资源显示

**管理页面 (`Admin/Services.razor`):**
```razor
<!-- Line 54 -->
@context.Category
```
- 直接展示数据库中的英文 key ❌
- 应该通过本地化资源显示

**服务表单 (`ServiceFormDialog.razor`):**
```razor
<!-- Line 25-31 -->
<MudSelect @bind-Value="_model.Category" Label="@L["ServiceForm_Category"]" Required="true">
    <MudSelectItem Value="@("image")">@L["ServiceForm_CategoryImage"]</MudSelectItem>
    <MudSelectItem Value="@("email")">@L["ServiceForm_CategoryEmail"]</MudSelectItem>
    <MudSelectItem Value="@("document")">@L["ServiceForm_CategoryDocument"]</MudSelectItem>
    <MudSelectItem Value="@("data")">@L["ServiceForm_CategoryData"]</MudSelectItem>
    <MudSelectItem Value="@("ai")">@L["ServiceForm_CategoryAI"]</MudSelectItem>
    <MudSelectItem Value="@("other")">@L["ServiceForm_CategoryOther"]</MudSelectItem>
</MudSelect>
```
- ✅ 使用了本地化资源
- 已在 `SharedResources.zh-CN.resx` 中定义

---

### 2. 二维码弹窗硬编码问题

#### 发现的硬编码内容

**文件：** `src/Verdure.Mcp.Web/Components/QrCodeDialog.razor`

```razor
<!-- Line 13 - 硬编码 -->
<MudText Typo="Typo.h6" Style="font-weight: 500; color: #07C160;">微信支付</MudText>

<!-- Line 18 - 硬编码 -->
<MudText Typo="Typo.body2" Color="Color.Secondary" Class="text-center">
    打开微信扫一扫
</MudText>

<!-- Line 25 - 硬编码 -->
<MudText Typo="Typo.h6" Style="font-weight: 500; color: #1677FF;">支付宝</MudText>

<!-- Line 30 - 硬编码 -->
<MudText Typo="Typo.body2" Color="Color.Secondary" Class="text-center">
    打开支付宝扫一扫
</MudText>

<!-- Line 43 - 硬编码 -->
<MudButton OnClick="Cancel" Color="Color.Primary" Variant="Variant.Text">关闭</MudButton>
```

**问题：**
- ❌ "微信支付" - 硬编码
- ❌ "打开微信扫一扫" - 硬编码
- ❌ "支付宝" - 硬编码  
- ❌ "打开支付宝扫一扫" - 硬编码
- ❌ "关闭" - 硬编码

**已有本地化资源：**
```xml
<!-- SharedResources.zh-CN.resx -->
<data name="QrCode_DialogTitle" xml:space="preserve">
    <value>扫码支持</value>
</data>
<data name="QrCode_ScanHint" xml:space="preserve">
    <value>使用微信或支付宝扫描下方二维码请我喝杯咖啡或饮料</value>
</data>
<data name="QrCode_Thanks" xml:space="preserve">
    <value>感谢您的支持！</value>
</data>
```

---

## ✅ 解决方案

### 方案一：完全移除后端 DisplayName（推荐）

#### 1. 修改后端服务

```csharp
// src/Verdure.Mcp.Server/Services/McpServiceService.cs

public async Task<List<McpCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
{
    var categories = await _dbContext.McpServices
        .Where(s => s.IsEnabled)
        .GroupBy(s => s.Category)
        .Select(g => new McpCategoryDto
        {
            Name = g.Key,                    // 仅返回英文 key
            DisplayName = g.Key,             // 与 Name 相同，或前端不使用
            IconName = GetCategoryIcon(g.Key),
            ServiceCount = g.Count()
        })
        .ToListAsync(cancellationToken);

    return categories;
}

// 删除 GetCategoryDisplayName 方法
```

#### 2. 修改前端展示

**Dashboard.razor:**
```razor
<!-- 使用本地化资源展示分类名称 -->
@GetCategoryDisplayName(category.Name) (@category.ServiceCount)

@code {
    private string GetCategoryDisplayName(string categoryKey) => categoryKey.ToLower() switch
    {
        "image" => L["Category_Image"],
        "email" => L["Category_Email"],
        "document" => L["Category_Document"],
        "data" => L["Category_Data"],
        "ai" => L["Category_AI"],
        _ => categoryKey
    };
}
```

**ServiceDetails.razor:**
```razor
<!-- Line 51 改为 -->
@GetCategoryDisplayName(_service.Category)

@code {
    private string GetCategoryDisplayName(string categoryKey) => categoryKey.ToLower() switch
    {
        "image" => L["Category_Image"],
        "email" => L["Category_Email"],
        "document" => L["Category_Document"],
        "data" => L["Category_Data"],
        "ai" => L["Category_AI"],
        _ => categoryKey
    };
}
```

**Admin/Services.razor:**
```razor
<!-- Line 54 改为 -->
@GetCategoryDisplayName(context.Category)

@code {
    private string GetCategoryDisplayName(string categoryKey) => categoryKey.ToLower() switch
    {
        "image" => L["Category_Image"],
        "email" => L["Category_Email"],
        "document" => L["Category_Document"],
        "data" => L["Category_Data"],
        "ai" => L["Category_AI"],
        _ => categoryKey
    };
}
```

#### 3. 添加本地化资源

**SharedResources.zh-CN.resx:**
```xml
<data name="Category_Image" xml:space="preserve">
    <value>图片生成</value>
</data>
<data name="Category_Email" xml:space="preserve">
    <value>邮件服务</value>
</data>
<data name="Category_Document" xml:space="preserve">
    <value>文档处理</value>
</data>
<data name="Category_Data" xml:space="preserve">
    <value>数据服务</value>
</data>
<data name="Category_AI" xml:space="preserve">
    <value>AI 服务</value>
</data>
<data name="Category_Other" xml:space="preserve">
    <value>其他</value>
</data>
```

**SharedResources.resx (英文默认):**
```xml
<data name="Category_Image" xml:space="preserve">
    <value>Image Generation</value>
</data>
<data name="Category_Email" xml:space="preserve">
    <value>Email Service</value>
</data>
<data name="Category_Document" xml:space="preserve">
    <value>Document Processing</value>
</data>
<data name="Category_Data" xml:space="preserve">
    <value>Data Service</value>
</data>
<data name="Category_AI" xml:space="preserve">
    <value>AI Service</value>
</data>
<data name="Category_Other" xml:space="preserve">
    <value>Other</value>
</data>
```

---

### 方案二：二维码弹窗修复

**修改 QrCodeDialog.razor:**

```razor
@inject IStringLocalizer<SharedResources> L

<MudDialog>
    <DialogContent>
        <MudStack AlignItems="AlignItems.Center" Spacing="4">
            <MudText Typo="Typo.body1" Class="text-center" Style="font-weight: 500;">
                @L["QrCode_ScanHint"]
            </MudText>
            
            <MudGrid Spacing="4" Justify="Justify.Center">
                <MudItem xs="12" sm="6">
                    <MudStack AlignItems="AlignItems.Center" Spacing="2">
                        <MudText Typo="Typo.h6" Style="font-weight: 500; color: #07C160;">@L["QrCode_Wechat"]</MudText>
                        <MudPaper Elevation="2" Class="pa-3" Style="border-radius: 12px; background-color: #ffffff;">
                            <MudImage Src="/wechat.JPG" Alt="@L["QrCode_WechatQRCode"]" Width="240" Height="240" 
                                      Style="display: block; border-radius: 8px;" />
                        </MudPaper>
                        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="text-center">
                            @L["QrCode_WechatHint"]
                        </MudText>
                    </MudStack>
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudStack AlignItems="AlignItems.Center" Spacing="2">
                        <MudText Typo="Typo.h6" Style="font-weight: 500; color: #1677FF;">@L["QrCode_Alipay"]</MudText>
                        <MudPaper Elevation="2" Class="pa-3" Style="border-radius: 12px; background-color: #ffffff;">
                            <MudImage Src="/alipay.JPG" Alt="@L["QrCode_AlipayQRCode"]" Width="240" Height="240" 
                                      Style="display: block; border-radius: 8px;" />
                        </MudPaper>
                        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="text-center">
                            @L["QrCode_AlipayHint"]
                        </MudText>
                    </MudStack>
                </MudItem>
            </MudGrid>
            
            <MudText Typo="Typo.body2" Color="Color.Secondary" Class="text-center" Style="line-height: 1.6;">
                @L["QrCode_Thanks"]
            </MudText>
        </MudStack>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel" Color="Color.Primary" Variant="Variant.Text">@L["Close"]</MudButton>
    </DialogActions>
</MudDialog>
```

**添加本地化资源：**

**SharedResources.zh-CN.resx:**
```xml
<data name="QrCode_Wechat" xml:space="preserve">
    <value>微信支付</value>
</data>
<data name="QrCode_WechatQRCode" xml:space="preserve">
    <value>微信收款码</value>
</data>
<data name="QrCode_WechatHint" xml:space="preserve">
    <value>打开微信扫一扫</value>
</data>
<data name="QrCode_Alipay" xml:space="preserve">
    <value>支付宝</value>
</data>
<data name="QrCode_AlipayQRCode" xml:space="preserve">
    <value>支付宝收款码</value>
</data>
<data name="QrCode_AlipayHint" xml:space="preserve">
    <value>打开支付宝扫一扫</value>
</data>
<data name="Close" xml:space="preserve">
    <value>关闭</value>
</data>
```

**SharedResources.resx (英文默认):**
```xml
<data name="QrCode_Wechat" xml:space="preserve">
    <value>WeChat Pay</value>
</data>
<data name="QrCode_WechatQRCode" xml:space="preserve">
    <value>WeChat QR Code</value>
</data>
<data name="QrCode_WechatHint" xml:space="preserve">
    <value>Open WeChat and scan</value>
</data>
<data name="QrCode_Alipay" xml:space="preserve">
    <value>Alipay</value>
</data>
<data name="QrCode_AlipayQRCode" xml:space="preserve">
    <value>Alipay QR Code</value>
</data>
<data name="QrCode_AlipayHint" xml:space="preserve">
    <value>Open Alipay and scan</value>
</data>
<data name="Close" xml:space="preserve">
    <value>Close</value>
</data>
```

---

## 📊 影响范围总结

### 需要修改的文件

#### 后端文件
1. ✏️ `src/Verdure.Mcp.Server/Services/McpServiceService.cs`
   - 移除或修改 `GetCategoryDisplayName` 方法
   - 修改 `GetCategoriesAsync` 方法

#### 前端文件
2. ✏️ `src/Verdure.Mcp.Web/Pages/Dashboard.razor`
   - 添加 `GetCategoryDisplayName` 方法
   - 修改分类展示逻辑

3. ✏️ `src/Verdure.Mcp.Web/Pages/ServiceDetails.razor`
   - 添加 `GetCategoryDisplayName` 方法
   - 修改 Line 51 分类展示

4. ✏️ `src/Verdure.Mcp.Web/Pages/Admin/Services.razor`
   - 添加 `GetCategoryDisplayName` 方法
   - 修改 Line 54 分类展示

5. ✏️ `src/Verdure.Mcp.Web/Components/QrCodeDialog.razor`
   - 替换所有硬编码文本为本地化资源

#### 资源文件
6. ✏️ `src/Verdure.Mcp.Web/Resources/SharedResources.zh-CN.resx`
   - 添加分类本地化资源
   - 添加二维码相关本地化资源

7. ✏️ `src/Verdure.Mcp.Web/Resources/SharedResources.resx`
   - 添加英文默认资源

---

## 🎯 实施优先级

### 高优先级
1. ✅ 修复二维码弹窗硬编码（用户可见，影响体验）
2. ✅ 修复服务详情页和管理页面分类展示（显示英文 key 不友好）

### 中优先级
3. ✅ 优化后端服务层（移除硬编码中文）
4. ✅ 完善 Dashboard 分类展示

---

## 📝 测试清单

- [ ] 验证所有页面分类显示正确的本地化文本
- [ ] 验证切换语言后分类名称正确更新
- [ ] 验证二维码弹窗所有文本支持多语言
- [ ] 验证管理页面创建/编辑服务时分类选择正常
- [ ] 验证 API 返回的分类数据结构正确

---

## 🔗 相关文件清单

### 核心文件
- `src/Verdure.Mcp.Domain/Entities/McpService.cs` - 实体定义
- `src/Verdure.Mcp.Shared/Models/Dtos.cs` - DTO 定义
- `src/Verdure.Mcp.Server/Services/McpServiceService.cs` - 服务层
- `src/Verdure.Mcp.Web/Pages/Dashboard.razor` - MCP 广场
- `src/Verdure.Mcp.Web/Pages/ServiceDetails.razor` - 服务详情
- `src/Verdure.Mcp.Web/Pages/Admin/Services.razor` - 服务管理
- `src/Verdure.Mcp.Web/Components/ServiceFormDialog.razor` - 服务表单
- `src/Verdure.Mcp.Web/Components/QrCodeDialog.razor` - 二维码弹窗
- `src/Verdure.Mcp.Web/Resources/SharedResources.zh-CN.resx` - 中文资源
- `src/Verdure.Mcp.Web/Resources/SharedResources.resx` - 英文资源

---

## 💡 建议

1. **统一本地化策略**：所有用户可见文本都应使用本地化资源，避免硬编码
2. **前端负责展示逻辑**：后端只返回数据标识（key），前端根据当前语言展示对应文本
3. **可扩展性**：新增分类时，只需在资源文件中添加对应翻译
4. **代码复用**：考虑创建共享的 `CategoryHelper` 类来统一处理分类相关逻辑

---

生成时间：2025-11-30
