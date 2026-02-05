namespace Verdure.Mcp.Server.Settings;

/// <summary>
/// Azure Speech (TTS) 配置
/// </summary>
public class AzureSpeechSettings
{
    public const string SectionName = "AzureSpeech";

    /// <summary>
    /// Azure Speech 订阅密钥
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure Speech 区域
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 语音合成的声音名称（例如: zh-CN-XiaoxiaoNeural）
    /// </summary>
    public string SpeechSynthesisVoiceName { get; set; } = string.Empty;

    /// <summary>
    /// 语音合成语言（可选，例如: zh-CN）
    /// </summary>
    public string? SpeechSynthesisLanguage { get; set; }
}
