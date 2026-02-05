using Microsoft.Extensions.Options;
using Verdure.Mcp.Server.Settings;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 音频存储服务接口
/// </summary>
public interface IAudioStorageService
{
    Task<AudioStorageResult> SaveOggAsync(byte[] audioBytes, Guid audioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 音频存储结果
/// </summary>
public class AudioStorageResult
{
    public string OggUrl { get; set; } = string.Empty;
    public int Size { get; set; }
}

/// <summary>
/// 音频存储服务实现
/// </summary>
public class AudioStorageService : IAudioStorageService
{
    private readonly AudioStorageSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AudioStorageService> _logger;

    public AudioStorageService(
        IOptions<AudioStorageSettings> settings,
        IWebHostEnvironment environment,
        ILogger<AudioStorageService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<AudioStorageResult> SaveOggAsync(
        byte[] audioBytes,
        Guid audioId,
        CancellationToken cancellationToken = default)
    {
        // 确保存储目录存在
        var storagePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, _settings.StoragePath);
        if (!Directory.Exists(storagePath))
        {
            Directory.CreateDirectory(storagePath);
            _logger.LogInformation("创建音频存储目录: {StoragePath}", storagePath);
        }

        var oggFileName = $"{audioId}.ogg";
        var oggFilePath = Path.Combine(storagePath, oggFileName);
        await File.WriteAllBytesAsync(oggFilePath, audioBytes, cancellationToken);

        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var oggUrl = $"{baseUrl}/{_settings.StoragePath}/{oggFileName}";

        _logger.LogInformation("音频已保存: {FilePath} ({Size} bytes)", oggFilePath, audioBytes.Length);

        return new AudioStorageResult
        {
            OggUrl = oggUrl,
            Size = audioBytes.Length
        };
    }
}
