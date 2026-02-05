using System.ComponentModel;
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

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                var storage = await imageStorageService.SaveImageAsync(
                    result.ImageBase64,
                    Guid.NewGuid(),
                    cancellationToken);

                pngUrl = storage.PngUrl;
                jpegUrl = storage.JpegUrl;
            }

            return new AiImageGenerationResponse
            {
                Success = true,
                Message = "图片生成成功",
                ImageUrl = pngUrl ?? result.ImageUrl,
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
