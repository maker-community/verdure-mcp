namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 图片存储服务接口
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// 保存 Base64 图片到本地文件系统
    /// </summary>
    /// <param name="base64Image">Base64 编码的图片数据</param>
    /// <param name="taskId">任务 ID，用于生成唯一文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>可公开访问的图片 URL</returns>
    Task<string> SaveImageAsync(string base64Image, Guid taskId, CancellationToken cancellationToken = default);

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
