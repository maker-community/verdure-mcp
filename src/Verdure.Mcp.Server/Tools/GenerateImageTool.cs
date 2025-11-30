using System.ComponentModel;
using System.Net;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// 使用 Azure OpenAI DALL-E 生成图片的 MCP 工具
/// </summary>
[McpServerToolType]
public class GenerateImageTool
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IEmailService _emailService;
    private readonly McpDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GenerateImageTool> _logger;

    public GenerateImageTool(
        IImageGenerationService imageGenerationService,
        IEmailService emailService,
        McpDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GenerateImageTool> logger)
    {
        _imageGenerationService = imageGenerationService;
        _emailService = emailService;
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 使用 Azure OpenAI DALL-E 根据提示词生成图片。
    /// 如果提供邮箱地址，会将生成的图片发送到指定邮箱。
    /// 如果请求头中包含用户信息（X-User-Email 和 X-User-Id），任务将异步运行。
    /// </summary>
    /// <param name="prompt">描述要生成图片的文本提示词</param>
    /// <param name="size">图片尺寸："1024x1024"、"1792x1024" 或 "1024x1792"，默认为 "1024x1024"</param>
    /// <param name="quality">图片质量："standard" 或 "hd"，默认为 "standard"</param>
    /// <param name="style">图片风格："vivid" 或 "natural"，默认为 "vivid"</param>
    /// <param name="cancellationToken"></param>
    /// <returns>包含任务信息和图片数据的 JSON 对象（同步模式下）</returns>
    [McpServerTool(Name = "generate_image")]
    [Description("使用 DALL-E 模型，根据文本提示词生成图片。支持邮件通知和异步处理。")]
    public async Task<ImageGenerationResponse> GenerateImage(
        [Description("描述要生成图片的文本提示词")] string prompt,
        [Description("图片尺寸：'1024x1024'、'1792x1024' 或 '1024x1792'，默认为 '1024x1024'")] string? size = null,
        [Description("图片质量：'standard' 或 'hd'，默认为 'standard'")] string? quality = null,
        [Description("图片风格：'vivid' 或 'natural'，默认为 'vivid'")] string? style = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        // 从请求头提取邮箱地址 (X-User-Email)
        var email = httpContext?.Request.Headers["X-User-Email"].FirstOrDefault();
        
        // 从请求头提取用户 ID (X-User-Id)
        var userId = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault();

        _logger.LogInformation("收到图片生成请求。提示词: {Prompt}, 邮箱: {Email}, 用户ID: {UserId}", 
            prompt, email ?? "无", userId ?? "无");

        // 创建任务记录
        var task = new ImageGenerationTask
        {
            Id = Guid.NewGuid(),
            Prompt = prompt,
            Size = size ?? "1024x1024",
            Quality = quality ?? "standard",
            Style = style ?? "vivid",
            Status = ImageTaskStatus.Pending,
            Email = email,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ImageGenerationTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 如果存在用户信息（X-User-Email 和 X-User-Id），使用 Hangfire 异步处理
        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("检测到用户信息，使用异步处理任务 {TaskId}", task.Id);
            
            var jobId = _backgroundJobClient.Enqueue<ImageGenerationBackgroundJob>(
                job => job.ExecuteAsync(task.Id, CancellationToken.None));
            
            task.HangfireJobId = jobId;
            task.Status = ImageTaskStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ImageGenerationResponse
            {
                TaskId = task.Id,
                Status = "处理中",
                Message = "图片生成任务已加入队列。如果您提供了邮箱地址，稍后会收到生成结果。",
                IsAsync = true
            };
        }
        else
        {
            // 同步处理
            _logger.LogInformation("未检测到完整用户信息，使用同步处理任务 {TaskId}", task.Id);
            
            task.Status = ImageTaskStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var result = await _imageGenerationService.GenerateImageAsync(
                    prompt, size, quality, style, cancellationToken);

                if (result.Success)
                {
                    task.Status = ImageTaskStatus.Completed;
                    task.ImageData = result.ImageBase64;
                    task.ImageUrl = result.ImageUrl;
                    task.CompletedAt = DateTime.UtcNow;
                    task.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    // 如果提供了邮箱，发送邮件
                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(result.ImageBase64))
                    {
                        try
                        {
                            var imageBytes = Convert.FromBase64String(result.ImageBase64);
                            var encodedPrompt = WebUtility.HtmlEncode(prompt);
                            var encodedRevisedPrompt = WebUtility.HtmlEncode(result.RevisedPrompt ?? "无");
                            await _emailService.SendImageEmailAsync(
                                email,
                                "您的图片已生成",
                                $"<h1>您的图片已成功生成！</h1><p>提示词：{encodedPrompt}</p><p>修订后的提示词：{encodedRevisedPrompt}</p>",
                                imageBytes,
                                $"image_{task.Id}.png",
                                cancellationToken);
                            
                            task.EmailSent = true;
                            await _dbContext.SaveChangesAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "发送邮件失败，任务 {TaskId}", task.Id);
                        }
                    }

                    // 同步模式：只返回 URL，不返回 base64 数据
                    return new ImageGenerationResponse
                    {
                        TaskId = task.Id,
                        Status = "已完成",
                        Message = "图片生成成功",
                        ImageUrl = result.ImageUrl,
                        RevisedPrompt = result.RevisedPrompt,
                        IsAsync = false
                    };
                }
                else
                {
                    task.Status = ImageTaskStatus.Failed;
                    task.ErrorMessage = result.ErrorMessage;
                    task.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return new ImageGenerationResponse
                    {
                        TaskId = task.Id,
                        Status = "失败",
                        Message = result.ErrorMessage ?? "图片生成失败",
                        IsAsync = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步生成图片时出错，任务 {TaskId}", task.Id);
                
                task.Status = ImageTaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new ImageGenerationResponse
                {
                    TaskId = task.Id,
                    Status = "失败",
                    Message = ex.Message,
                    IsAsync = false
                };
            }
        }
    }

    /// <summary>
    /// 获取图片生成任务的状态
    /// </summary>
    /// <param name="taskId">要查询的任务 ID</param>
    /// <returns>任务状态和结果（如果已完成）</returns>
    [McpServerTool(Name = "get_image_task_status")]
    [Description("获取图片生成任务的状态")]
    public async Task<ImageGenerationResponse> GetImageTaskStatus(
        [Description("要查询的任务 ID")] string taskId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(taskId, out var id))
        {
            return new ImageGenerationResponse
            {
                Status = "错误",
                Message = "任务 ID 格式无效"
            };
        }

        var task = await _dbContext.ImageGenerationTasks.FindAsync(new object[] { id }, cancellationToken);
        
        if (task == null)
        {
            return new ImageGenerationResponse
            {
                Status = "错误",
                Message = "未找到任务"
            };
        }

        return new ImageGenerationResponse
        {
            TaskId = task.Id,
            Status = task.Status.ToString().ToLowerInvariant(),
            Message = task.ErrorMessage ?? GetStatusMessage(task.Status),
            ImageBase64 = task.ImageData,
            ImageUrl = task.ImageUrl,
            IsAsync = !string.IsNullOrEmpty(task.HangfireJobId)
        };
    }

    private static string GetStatusMessage(ImageTaskStatus status)
    {
        return status switch
        {
            ImageTaskStatus.Pending => "任务等待中",
            ImageTaskStatus.Processing => "任务处理中",
            ImageTaskStatus.Completed => "图片生成成功",
            ImageTaskStatus.Failed => "图片生成失败",
            ImageTaskStatus.Cancelled => "任务已取消",
            _ => "未知状态"
        };
    }
}

/// <summary>
/// 图片生成响应模型
/// </summary>
public class ImageGenerationResponse
{
    public Guid? TaskId { get; set; }
    public required string Status { get; set; }
    public string? Message { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageUrl { get; set; }
    public string? RevisedPrompt { get; set; }
    public bool IsAsync { get; set; }
}
