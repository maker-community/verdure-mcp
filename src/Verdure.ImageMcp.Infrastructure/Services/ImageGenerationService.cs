using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Images;

namespace Verdure.ImageMcp.Infrastructure.Services;

/// <summary>
/// Settings for Azure OpenAI configuration
/// </summary>
public class AzureOpenAISettings
{
    public const string SectionName = "AzureOpenAI";
    
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "dall-e-3";
    public string ApiVersion { get; set; } = "2024-02-01";
}

/// <summary>
/// Result of image generation
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RevisedPrompt { get; set; }
}

/// <summary>
/// Interface for image generation service
/// </summary>
public interface IImageGenerationService
{
    Task<ImageGenerationResult> GenerateImageAsync(
        string prompt, 
        string? size = null, 
        string? quality = null, 
        string? style = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Image generation service using Azure OpenAI
/// </summary>
public class ImageGenerationService : IImageGenerationService
{
    private readonly AzureOpenAISettings _settings;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(IOptions<AzureOpenAISettings> settings, ILogger<ImageGenerationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(
        string prompt, 
        string? size = null, 
        string? quality = null, 
        string? style = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating image with prompt: {Prompt}", prompt);

            var credential = new AzureKeyCredential(_settings.ApiKey);
            var client = new AzureOpenAIClient(new Uri(_settings.Endpoint), credential);
            var imageClient = client.GetImageClient(_settings.DeploymentName);

            // Parse size
            var imageSize = ParseSize(size);
            var imageQuality = ParseQuality(quality);
            var imageStyle = ParseStyle(style);

            var options = new ImageGenerationOptions
            {
                Size = imageSize,
                Quality = imageQuality,
                Style = imageStyle,
                ResponseFormat = GeneratedImageFormat.Bytes
            };

            var response = await imageClient.GenerateImageAsync(prompt, options, cancellationToken);
            var generatedImage = response.Value;

            // Get the image data
            string? base64Data = null;
            if (generatedImage.ImageBytes != null)
            {
                base64Data = Convert.ToBase64String(generatedImage.ImageBytes.ToArray());
            }

            _logger.LogInformation("Image generated successfully");

            return new ImageGenerationResult
            {
                Success = true,
                ImageBase64 = base64Data,
                ImageUrl = generatedImage.ImageUri?.ToString(),
                RevisedPrompt = generatedImage.RevisedPrompt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image");
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static GeneratedImageSize ParseSize(string? size)
    {
        return size?.ToLowerInvariant() switch
        {
            "1024x1024" => GeneratedImageSize.W1024xH1024,
            "1792x1024" => GeneratedImageSize.W1792xH1024,
            "1024x1792" => GeneratedImageSize.W1024xH1792,
            _ => GeneratedImageSize.W1024xH1024
        };
    }

    private static GeneratedImageQuality ParseQuality(string? quality)
    {
        return quality?.ToLowerInvariant() switch
        {
            "hd" => GeneratedImageQuality.High,
            "high" => GeneratedImageQuality.High,
            "standard" => GeneratedImageQuality.Standard,
            _ => GeneratedImageQuality.Standard
        };
    }

    private static GeneratedImageStyle ParseStyle(string? style)
    {
        return style?.ToLowerInvariant() switch
        {
            "vivid" => GeneratedImageStyle.Vivid,
            "natural" => GeneratedImageStyle.Natural,
            _ => GeneratedImageStyle.Vivid
        };
    }
}
