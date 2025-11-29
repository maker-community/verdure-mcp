using System.Net;
using Microsoft.EntityFrameworkCore;
using Verdure.Mcp.Domain.Enums;
using Verdure.Mcp.Infrastructure.Data;
using Verdure.Mcp.Infrastructure.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// Background job for processing image generation tasks asynchronously
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
        _logger.LogInformation("Starting background image generation for task {TaskId}", taskId);

        var task = await _dbContext.ImageGenerationTasks.FindAsync(new object[] { taskId }, cancellationToken);
        
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found", taskId);
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

                _logger.LogInformation("Task {TaskId} completed successfully", taskId);

                // Send email if provided
                if (!string.IsNullOrEmpty(task.Email) && !string.IsNullOrEmpty(result.ImageBase64))
                {
                    try
                    {
                        var imageBytes = Convert.FromBase64String(result.ImageBase64);
                        var encodedPrompt = WebUtility.HtmlEncode(task.Prompt);
                        var encodedRevisedPrompt = WebUtility.HtmlEncode(result.RevisedPrompt ?? "N/A");
                        await _emailService.SendImageEmailAsync(
                            task.Email,
                            "Your Generated Image",
                            $"<h1>Your image has been generated!</h1><p>Prompt: {encodedPrompt}</p><p>Revised prompt: {encodedRevisedPrompt}</p>",
                            imageBytes,
                            $"image_{task.Id}.png",
                            cancellationToken);
                        
                        task.EmailSent = true;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        
                        _logger.LogInformation("Email sent for task {TaskId}", taskId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send email for task {TaskId}", taskId);
                    }
                }
            }
            else
            {
                task.Status = ImageTaskStatus.Failed;
                task.ErrorMessage = result.ErrorMessage;
                task.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("Task {TaskId} failed: {Error}", taskId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task {TaskId}", taskId);
            
            task.Status = ImageTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
