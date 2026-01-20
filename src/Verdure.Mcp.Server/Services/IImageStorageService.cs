namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 图片存储服务接口
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// 保存 Base64 图片到本地文件系统（同时保存 PNG 原始版本和 JPEG 压缩版本）
    /// </summary>
    /// <param name="base64Image">Base64 编码的图片数据</param>
    /// <param name="taskId">任务 ID，用于生成唯一文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含 PNG 和 JPEG URL 的结果</returns>
    Task<ImageStorageResult> SaveImageAsync(string base64Image, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定的图片文件
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteImageAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期的图片文件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的文件数量</returns>
    Task<int> CleanupExpiredImagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 图片存储结果
/// </summary>
public class ImageStorageResult
{
    /// <summary>
    /// PNG 原始图片 URL
    /// </summary>
    public required string PngUrl { get; init; }

    /// <summary>
    /// JPEG 压缩图片 URL（用于推送到设备）
    /// </summary>
    public required string JpegUrl { get; init; }

    /// <summary>
    /// 原始文件大小（字节）
    /// </summary>
    public long OriginalSize { get; init; }

    /// <summary>
    /// JPEG 文件大小（字节）
    /// </summary>
    public long JpegSize { get; init; }

    /// <summary>
    /// 压缩比例（百分比）
    /// </summary>
    public double CompressionRatio { get; init; }
}
