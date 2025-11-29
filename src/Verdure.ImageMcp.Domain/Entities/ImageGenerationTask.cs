using Verdure.ImageMcp.Domain.Enums;

namespace Verdure.ImageMcp.Domain.Entities;

/// <summary>
/// Represents an image generation task stored in the database
/// </summary>
public class ImageGenerationTask
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The prompt used to generate the image
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// The size of the generated image (e.g., "1024x1024")
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// The quality of the generated image (e.g., "standard", "hd")
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// The style of the generated image (e.g., "vivid", "natural")
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Current status of the task
    /// </summary>
    public ImageTaskStatus Status { get; set; }

    /// <summary>
    /// Base64 encoded image data when completed
    /// </summary>
    public string? ImageData { get; set; }

    /// <summary>
    /// URL of the generated image if available
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Error message if the task failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Email address to send the result to (optional)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Whether the email notification was sent
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// User identifier from the request header (optional)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Hangfire job ID for async tasks
    /// </summary>
    public string? HangfireJobId { get; set; }

    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the task was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the task was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
