using Microsoft.Extensions.Options;
using Verdure.Mcp.Server.Settings;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 图片存储服务实现
/// </summary>
public class ImageStorageService : IImageStorageService
{
    private readonly ImageStorageSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(
        IOptions<ImageStorageSettings> settings,
        IWebHostEnvironment environment,
        ILogger<ImageStorageService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SaveImageAsync(string base64Image, Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 确保存储目录存在
            var storagePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, _settings.StoragePath);
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
                _logger.LogInformation("创建图片存储目录: {StoragePath}", storagePath);
            }

            // 将 Base64 转换为字节数组
            var imageBytes = Convert.FromBase64String(base64Image);

            // 生成文件名: {taskId}.png
            var fileName = $"{taskId}.png";
            var filePath = Path.Combine(storagePath, fileName);

            // 保存文件
            await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);
            _logger.LogInformation("图片已保存: {FilePath}, 大小: {Size} bytes", filePath, imageBytes.Length);

            // 构建可访问的 URL
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            var relativePath = $"/{_settings.StoragePath}/{fileName}";
            var imageUrl = $"{baseUrl}{relativePath}";

            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存图片失败，任务 ID: {TaskId}", taskId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteImageAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var storagePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, _settings.StoragePath);
            var fileName = $"{taskId}.png";
            var filePath = Path.Combine(storagePath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("图片已删除: {FilePath}", filePath);
                return Task.FromResult(true);
            }

            _logger.LogWarning("图片文件不存在: {FilePath}", filePath);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除图片失败，任务 ID: {TaskId}", taskId);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredImagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_settings.EnableAutoCleanup)
            {
                _logger.LogDebug("自动清理已禁用");
                return Task.FromResult(0);
            }

            var storagePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, _settings.StoragePath);
            
            if (!Directory.Exists(storagePath))
            {
                _logger.LogDebug("存储目录不存在: {StoragePath}", storagePath);
                return Task.FromResult(0);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-_settings.RetentionDays);
            var deletedCount = 0;

            var files = Directory.GetFiles(storagePath, "*.png");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("清理过期图片: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理图片失败: {FilePath}", file);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("清理完成，删除了 {Count} 个过期图片", deletedCount);
            }

            return Task.FromResult(deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期图片时出错");
            return Task.FromResult(0);
        }
    }
}
