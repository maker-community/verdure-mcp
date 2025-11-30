using System.Net;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// 异步处理图片生成任务的后台作业
/// </summary>
public class ImageGenerationBackgroundJob
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IEmailService _emailService;
    private readonly McpDbContext _dbContext;
    private readonly ILogger<ImageGenerationBackgroundJob> _logger;

    public ImageGenerationBackgroundJob(
        IImageGenerationService imageGenerationService,
        IEmailService emailService,
        McpDbContext dbContext,
        ILogger<ImageGenerationBackgroundJob> logger)
    {
        _imageGenerationService = imageGenerationService;
        _emailService = emailService;
        _dbContext = dbContext;
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
                task.ImageUrl = result.ImageUrl;
                task.CompletedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
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
