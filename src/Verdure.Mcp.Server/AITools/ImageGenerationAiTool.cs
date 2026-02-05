using System.ComponentModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;

namespace Verdure.Mcp.Server.AITools;

/// <summary>
/// 同步 AI 生图工具（面向 Agent Framework）
/// </summary>
public class ImageGenerationAiTool
{
    /// <summary>
    /// 同步生成图片并返回可访问的 URL
    /// </summary>
    [Description("同步生成图片并返回可访问的 URL。")]
    public static async Task<AiImageGenerationResponse> GenerateImageAsync(
        [Description("描述要生成图片的文本提示词")] string prompt,
        [Description("图片尺寸：'1024x1024'、'1792x1024' 或 '1024x1792'，默认为 '1024x1024'")] string? size = null,
        [Description("图片质量：'standard' 或 'hd'，默认为 'standard'")] string? quality = null,
        [Description("图片风格：'vivid' 或 'natural'，默认为 'vivid'")] string? style = null,
        IServiceProvider serviceProvider = default!,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var imageGenerationService = scope.ServiceProvider.GetRequiredService<IImageGenerationService>();
        var imageStorageService = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var devicePushService = scope.ServiceProvider.GetRequiredService<IDevicePushService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ImageGenerationAiTool>>();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new AiImageGenerationResponse
            {
                Success = false,
                Message = "提示词不能为空"
            };
        }

        try
        {
            logger.LogInformation("AI 生图工具调用。Prompt: {Prompt}", prompt);

            var result = await imageGenerationService.GenerateImageAsync(
                prompt, size, quality, style, cancellationToken);

            if (!result.Success)
            {
                return new AiImageGenerationResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "图片生成失败"
                };
            }

            string? pngUrl = null;
            string? jpegUrl = null;
            ImageStorageResult? storageResult = null;
            var imageId = Guid.NewGuid();

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                storageResult = await imageStorageService.SaveImageAsync(
                    result.ImageBase64,
                    imageId,
                    cancellationToken);

                pngUrl = storageResult.PngUrl;
                jpegUrl = storageResult.JpegUrl;
            }

            var userId = UserContext.Current?.UserId;
            var userEmail = UserContext.Current?.UserEmail;

            if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(result.ImageBase64))
            {
                try
                {
                    var imageBytes = Convert.FromBase64String(result.ImageBase64);
                    var encodedPrompt = WebUtility.HtmlEncode(prompt);
                    var encodedRevisedPrompt = WebUtility.HtmlEncode(result.RevisedPrompt ?? "无");
                    await emailService.SendImageEmailAsync(
                        userEmail,
                        "您的图片已生成",
                        $"<h1>您的图片已成功生成！</h1><p>提示词：{encodedPrompt}</p><p>修订后的提示词：{encodedRevisedPrompt}</p>",
                        imageBytes,
                        $"image_{imageId}.png",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "发送邮件失败，UserEmail={UserEmail}", userEmail);
                }
            }

            if (!string.IsNullOrEmpty(userId) && storageResult != null)
            {
                try
                {
                    var notificationMessage = new
                    {
                        action = "notification",
                        title = "图片生成完成",
                        content = $"您的图片已生成：{prompt.Substring(0, Math.Min(30, prompt.Length))}...",
                        emotion = "happy",
                        sound = "success"
                    };
                    await devicePushService.SendCustomMessageAsync(userId, notificationMessage, cancellationToken);

                    var imageMessage = new
                    {
                        action = "image",
                        url = storageResult.JpegUrl,
                        taskId = imageId.ToString(),
                        pngUrl = storageResult.PngUrl,
                        prompt = prompt,
                        jpegSize = storageResult.JpegSize,
                        timestamp = DateTime.UtcNow
                    };

                    await devicePushService.SendCustomMessageAsync(userId, imageMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "推送消息到设备失败，UserId={UserId}", userId);
                }
            }

            return new AiImageGenerationResponse
            {
                Success = true,
                Message = "图片生成成功",
                //ImageUrl = pngUrl ?? result.ImageUrl,
                JpegUrl = jpegUrl,
                RevisedPrompt = result.RevisedPrompt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI 生图工具执行失败");
            return new AiImageGenerationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}

/// <summary>
/// AI 生图工具返回结果
/// </summary>
public class AiImageGenerationResponse
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public string? ImageUrl { get; set; }

    public string? JpegUrl { get; set; }

    public string? RevisedPrompt { get; set; }
}
