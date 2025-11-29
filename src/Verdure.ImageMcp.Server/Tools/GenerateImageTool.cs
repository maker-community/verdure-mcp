using System.ComponentModel;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Verdure.ImageMcp.Domain.Entities;
using Verdure.ImageMcp.Domain.Enums;
using Verdure.ImageMcp.Infrastructure.Data;
using Verdure.ImageMcp.Infrastructure.Services;

namespace Verdure.ImageMcp.Server.Tools;

/// <summary>
/// MCP Tool for generating images using Azure OpenAI DALL-E
/// </summary>
[McpServerToolType]
public class GenerateImageTool
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IEmailService _emailService;
    private readonly ImageMcpDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GenerateImageTool> _logger;

    public GenerateImageTool(
        IImageGenerationService imageGenerationService,
        IEmailService emailService,
        ImageMcpDbContext dbContext,
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
    /// Generates an image based on the provided prompt using Azure OpenAI DALL-E.
    /// If email is provided, sends the generated image to the specified email address.
    /// If user information is present in the request header, the task runs asynchronously.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate</param>
    /// <param name="size">Image size: "1024x1024", "1792x1024", or "1024x1792". Default is "1024x1024"</param>
    /// <param name="quality">Image quality: "standard" or "hd". Default is "standard"</param>
    /// <param name="style">Image style: "vivid" or "natural". Default is "vivid"</param>
    /// <returns>A JSON object containing the task information and image data (if sync mode)</returns>
    [McpServerTool(Name = "generate_image")]
    [Description("Generates an image based on the provided text prompt using Azure OpenAI DALL-E. Supports email notification and async processing.")]
    public async Task<ImageGenerationResponse> GenerateImage(
        [Description("The text prompt describing the image to generate")] string prompt,
        [Description("Image size: '1024x1024', '1792x1024', or '1024x1792'. Default is '1024x1024'")] string? size = null,
        [Description("Image quality: 'standard' or 'hd'. Default is 'standard'")] string? quality = null,
        [Description("Image style: 'vivid' or 'natural'. Default is 'vivid'")] string? style = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        // Extract email from request header (X-User-Email)
        var email = httpContext?.Request.Headers["X-User-Email"].FirstOrDefault();
        
        // Extract user ID from request header (X-User-Id)
        var userId = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault();

        _logger.LogInformation("Image generation requested. Prompt: {Prompt}, Email: {Email}, UserId: {UserId}", 
            prompt, email ?? "none", userId ?? "none");

        // Create task record
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
        await _dbContext.SaveChangesAsync();

        // If user info is present, use async processing with Hangfire
        if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("User ID present, using async processing for task {TaskId}", task.Id);
            
            var jobId = _backgroundJobClient.Enqueue<ImageGenerationBackgroundJob>(
                job => job.ExecuteAsync(task.Id, CancellationToken.None));
            
            task.HangfireJobId = jobId;
            task.Status = ImageTaskStatus.Processing;
            await _dbContext.SaveChangesAsync();

            return new ImageGenerationResponse
            {
                TaskId = task.Id,
                Status = "processing",
                Message = "Image generation task has been queued. You will receive the result via email if provided.",
                IsAsync = true
            };
        }
        else
        {
            // Sync processing
            _logger.LogInformation("No user ID, using sync processing for task {TaskId}", task.Id);
            
            task.Status = ImageTaskStatus.Processing;
            await _dbContext.SaveChangesAsync();

            try
            {
                var result = await _imageGenerationService.GenerateImageAsync(
                    prompt, size, quality, style);

                if (result.Success)
                {
                    task.Status = ImageTaskStatus.Completed;
                    task.ImageData = result.ImageBase64;
                    task.ImageUrl = result.ImageUrl;
                    task.CompletedAt = DateTime.UtcNow;
                    task.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();

                    // Send email if provided
                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(result.ImageBase64))
                    {
                        try
                        {
                            var imageBytes = Convert.FromBase64String(result.ImageBase64);
                            await _emailService.SendImageEmailAsync(
                                email,
                                "Your Generated Image",
                                $"<h1>Your image has been generated!</h1><p>Prompt: {prompt}</p><p>Revised prompt: {result.RevisedPrompt ?? "N/A"}</p>",
                                imageBytes,
                                $"image_{task.Id}.png");
                            
                            task.EmailSent = true;
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send email for task {TaskId}", task.Id);
                        }
                    }

                    return new ImageGenerationResponse
                    {
                        TaskId = task.Id,
                        Status = "completed",
                        Message = "Image generated successfully",
                        ImageBase64 = result.ImageBase64,
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
                    await _dbContext.SaveChangesAsync();

                    return new ImageGenerationResponse
                    {
                        TaskId = task.Id,
                        Status = "failed",
                        Message = result.ErrorMessage ?? "Image generation failed",
                        IsAsync = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync image generation for task {TaskId}", task.Id);
                
                task.Status = ImageTaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                return new ImageGenerationResponse
                {
                    TaskId = task.Id,
                    Status = "failed",
                    Message = ex.Message,
                    IsAsync = false
                };
            }
        }
    }

    /// <summary>
    /// Gets the status of an image generation task
    /// </summary>
    /// <param name="taskId">The ID of the task to check</param>
    /// <returns>Task status and result if completed</returns>
    [McpServerTool(Name = "get_image_task_status")]
    [Description("Gets the status of an image generation task")]
    public async Task<ImageGenerationResponse> GetImageTaskStatus(
        [Description("The ID of the task to check")] string taskId)
    {
        if (!Guid.TryParse(taskId, out var id))
        {
            return new ImageGenerationResponse
            {
                Status = "error",
                Message = "Invalid task ID format"
            };
        }

        var task = await _dbContext.ImageGenerationTasks.FindAsync(id);
        
        if (task == null)
        {
            return new ImageGenerationResponse
            {
                Status = "error",
                Message = "Task not found"
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
            ImageTaskStatus.Pending => "Task is pending",
            ImageTaskStatus.Processing => "Task is being processed",
            ImageTaskStatus.Completed => "Image generated successfully",
            ImageTaskStatus.Failed => "Image generation failed",
            ImageTaskStatus.Cancelled => "Task was cancelled",
            _ => "Unknown status"
        };
    }
}

/// <summary>
/// Response model for image generation
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
