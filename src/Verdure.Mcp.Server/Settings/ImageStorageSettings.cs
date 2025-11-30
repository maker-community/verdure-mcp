namespace Verdure.Mcp.Server.Settings;

/// <summary>
/// 图片存储配置
/// </summary>
public class ImageStorageSettings
{
    public const string SectionName = "ImageStorage";

    /// <summary>
    /// 图片存储的本地目录路径（相对于 wwwroot）
    /// 默认: "generated-images"
    /// </summary>
    public string StoragePath { get; set; } = "generated-images";

    /// <summary>
    /// 图片访问的基础 URL（域名部分）
    /// 例如: "https://api.example.com" 或 "https://localhost:5000"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用图片自动清理
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// 图片保留天数（超过此天数的图片会被清理）
    /// 默认: 7 天
    /// </summary>
    public int RetentionDays { get; set; } = 7;
}
