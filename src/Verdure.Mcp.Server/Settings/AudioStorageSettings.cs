namespace Verdure.Mcp.Server.Settings;

/// <summary>
/// 音频存储配置
/// </summary>
public class AudioStorageSettings
{
    public const string SectionName = "AudioStorage";

    /// <summary>
    /// 音频存储的本地目录路径（相对于 wwwroot）
    /// 默认: "generated-audio"
    /// </summary>
    public string StoragePath { get; set; } = "generated-audio";

    /// <summary>
    /// 音频访问的基础 URL（域名部分）
    /// 例如: "https://api.example.com" 或 "https://localhost:5000"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
