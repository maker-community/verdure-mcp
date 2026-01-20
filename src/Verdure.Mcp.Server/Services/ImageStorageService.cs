using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
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
    public async Task<ImageStorageResult> SaveImageAsync(string base64Image, Guid taskId, CancellationToken cancellationToken = default)
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

            // 1. 保存原始 PNG 文件
            var pngFileName = $"{taskId}.png";
            var pngFilePath = Path.Combine(storagePath, pngFileName);
            await File.WriteAllBytesAsync(pngFilePath, imageBytes, cancellationToken);

            // 2. 使用 ImageSharp 转换为 JPEG 格式（用于推送到设备）
            byte[] jpegBytes;
            using (var inputStream = new MemoryStream(imageBytes))
            using (var outputStream = new MemoryStream())
            {
                using (var image = await Image.LoadAsync(inputStream, cancellationToken))
                {
                    // 配置 JPEG 编码器
                    var encoder = new JpegEncoder
                    {
                        Quality = 85 // 设置质量（1-100），85 是平衡质量和大小的推荐值
                    };

                    await image.SaveAsync(outputStream, encoder, cancellationToken);
                }
                
                jpegBytes = outputStream.ToArray();
            }

            // 3. 保存 JPEG 文件
            var jpegFileName = $"{taskId}.jpg";
            var jpegFilePath = Path.Combine(storagePath, jpegFileName);
            await File.WriteAllBytesAsync(jpegFilePath, jpegBytes, cancellationToken);
            
            var originalSize = imageBytes.Length;
            var jpegSize = jpegBytes.Length;
            var compressionRatio = (1 - (double)jpegSize / originalSize) * 100;
            
            _logger.LogInformation(
                "图片已保存 - PNG: {PngFilePath} ({OriginalSize} bytes), JPEG: {JpegFilePath} ({JpegSize} bytes), 压缩率: {Ratio:F1}%", 
                pngFilePath, originalSize, jpegFilePath, jpegSize, compressionRatio);

            // 构建可访问的 URL
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            var pngUrl = $"{baseUrl}/{_settings.StoragePath}/{pngFileName}";
            var jpegUrl = $"{baseUrl}/{_settings.StoragePath}/{jpegFileName}";

            return new ImageStorageResult
            {
                PngUrl = pngUrl,
                JpegUrl = jpegUrl,
                OriginalSize = originalSize,
                JpegSize = jpegSize,
                CompressionRatio = compressionRatio
            };
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
            
            // 尝试删除 .jpg 文件
            var jpgFileName = $"{taskId}.jpg";
            var jpgFilePath = Path.Combine(storagePath, jpgFileName);
            
            // 也尝试删除旧的 .png 文件（向后兼容）
            var pngFileName = $"{taskId}.png";
            var pngFilePath = Path.Combine(storagePath, pngFileName);

            var deleted = false;
            
            if (File.Exists(jpgFilePath))
            {
                File.Delete(jpgFilePath);
                _logger.LogInformation("图片已删除: {FilePath}", jpgFilePath);
                deleted = true;
            }
            
            if (File.Exists(pngFilePath))
            {
                File.Delete(pngFilePath);
                _logger.LogInformation("图片已删除: {FilePath}", pngFilePath);
                deleted = true;
            }

            if (!deleted)
            {
                _logger.LogWarning("图片文件不存在，任务 ID: {TaskId}", taskId);
            }
            
            return Task.FromResult(deleted);
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

            // 清理 .jpg 文件
            var jpgFiles = Directory.GetFiles(storagePath, "*.jpg");
            foreach (var file in jpgFiles)
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
            
            // 清理旧的 .png 文件（向后兼容）
            var pngFiles = Directory.GetFiles(storagePath, "*.png");
            foreach (var file in pngFiles)
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
