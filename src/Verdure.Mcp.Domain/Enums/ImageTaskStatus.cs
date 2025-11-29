namespace Verdure.Mcp.Domain.Enums;

/// <summary>
/// Represents the status of an image generation task
/// </summary>
public enum ImageTaskStatus
{
    /// <summary>
    /// Task is pending execution
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Task is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Task completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Task failed with an error
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Task was cancelled
    /// </summary>
    Cancelled = 4
}
