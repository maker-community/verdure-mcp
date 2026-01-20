using System.Net;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// 异步处理图片生成任务的后台作业
/// </summary>
public class ImageGenerationBackgroundJob
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IEmailService _emailService;
    private readonly McpDbContext _dbContext;
    private readonly IImageStorageService _imageStorageService;
    private readonly IDevicePushService _devicePushService;
    private readonly ILogger<ImageGenerationBackgroundJob> _logger;

    public ImageGenerationBackgroundJob(
        IImageGenerationService imageGenerationService,
        IEmailService emailService,
        McpDbContext dbContext,
        IImageStorageService imageStorageService,
        IDevicePushService devicePushService,
        ILogger<ImageGenerationBackgroundJob> logger)
    {
        _imageGenerationService = imageGenerationService;
        _emailService = emailService;
        _dbContext = dbContext;
        _imageStorageService = imageStorageService;
        _devicePushService = devicePushService;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid taskId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始后台生成图片，任务 {TaskId}", taskId);

        var task = await _dbContext.ImageGenerationTasks.FindAsync(new object[] { taskId }, cancellationToken);
        
        if (task == null)
        {
            _logger.LogWarning("未找到任务 {TaskId}", taskId);
            return;
        }

        try
        {
            task.Status = ImageTaskStatus.Processing;
            task.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = await _imageGenerationService.GenerateImageAsync(
                task.Prompt,
                task.Size,
                task.Quality,
                task.Style,
                cancellationToken);

            if (result.Success)
            {
                task.Status = ImageTaskStatus.Completed;
                task.ImageData = result.ImageBase64;
                task.CompletedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;

                // 保存图片到本地文件系统（PNG + JPEG）
                ImageStorageResult? storageResult = null;
                if (!string.IsNullOrEmpty(result.ImageBase64))
                {
                    try
                    {
                        storageResult = await _imageStorageService.SaveImageAsync(
                            result.ImageBase64, 
                            task.Id, 
                            cancellationToken);
                        task.ImageUrl = storageResult.PngUrl; // 数据库保存 PNG URL
                        _logger.LogInformation(
                            "图片已保存 - PNG: {PngUrl}, JPEG: {JpegUrl}, 压缩率: {CompressionRatio:F1}%",
                            storageResult.PngUrl, storageResult.JpegUrl, storageResult.CompressionRatio);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "保存图片到本地失败，任务 {TaskId}", taskId);
                        // 即使保存失败，仍然继续流程
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("任务 {TaskId} 成功完成", taskId);

                // 如果提供了邮箱，发送邮件
                if (!string.IsNullOrEmpty(task.Email) && !string.IsNullOrEmpty(result.ImageBase64))
                {
                    try
                    {
                        var imageBytes = Convert.FromBase64String(result.ImageBase64);
                        var encodedPrompt = WebUtility.HtmlEncode(task.Prompt);
                        var encodedRevisedPrompt = WebUtility.HtmlEncode(result.RevisedPrompt ?? "无");
                        await _emailService.SendImageEmailAsync(
                            task.Email,
                            "您的图片已生成",
                            $"<h1>您的图片已成功生成！</h1><p>提示词：{encodedPrompt}</p><p>修订后的提示词：{encodedRevisedPrompt}</p>",
                            imageBytes,
                            $"image_{task.Id}.png",
                            cancellationToken);
                        
                        task.EmailSent = true;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        
                        _logger.LogInformation("邮件已发送，任务 {TaskId}", taskId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "发送邮件失败，任务 {TaskId}", taskId);
                    }
                }

                // 如果有用户 ID，推送到用户设备（使用 JPEG 版本，符合 xiaozhi 协议）
                if (!string.IsNullOrEmpty(task.UserId) && storageResult != null)
                {
                    try
                    {
                        // 1. 先发送通知消息
                        var notificationMessage = new
                        {
                            action = "notification",
                            title = "图片生成完成",
                            content = $"您的图片已生成：{task.Prompt.Substring(0, Math.Min(30, task.Prompt.Length))}...",
                            emotion = "happy",
                            sound = "success"
                        };
                        await _devicePushService.SendCustomMessageAsync(task.UserId, notificationMessage, cancellationToken);
                        
                        // 2. 再发送图片消息（ESP32 期望的格式 - xiaozhi 协议）
                        var imageMessage = new
                        {
                            action = "image",
                            url = storageResult.JpegUrl,  // 使用 JPEG URL（体积小）
                            // 扩展信息（可选，ESP32 可以忽略）
                            taskId = task.Id.ToString(),
                            pngUrl = storageResult.PngUrl,
                            prompt = task.Prompt,
                            jpegSize = storageResult.JpegSize,
                            timestamp = DateTime.UtcNow
                        };

                        await _devicePushService.SendCustomMessageAsync(task.UserId, imageMessage, cancellationToken);
                        _logger.LogInformation(
                            "已推送图片到用户 {UserId} 的设备，任务 {TaskId}，JPEG URL: {JpegUrl} ({JpegSize} bytes)", 
                            task.UserId, taskId, storageResult.JpegUrl, storageResult.JpegSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "推送消息到设备失败，用户 {UserId}，任务 {TaskId}", task.UserId, taskId);
                    }
                }
            }
            else
            {
                task.Status = ImageTaskStatus.Failed;
                task.ErrorMessage = result.ErrorMessage;
                task.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("任务 {TaskId} 失败：{Error}", taskId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理任务 {TaskId} 时出错", taskId);
            
            task.Status = ImageTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
