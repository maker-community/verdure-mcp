using System.ComponentModel;
using System.Net;
using ModelContextProtocol.Server;
using Verdure.Mcp.Infrastructure.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// MCP Tool for sending emails
/// </summary>
[McpServerToolType]
public class EmailTool
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailTool> _logger;

    public EmailTool(
        IEmailService emailService,
        ILogger<EmailTool> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Sends an email with optional image attachment
    /// </summary>
    /// <param name="toEmail">The recipient email address</param>
    /// <param name="subject">The email subject</param>
    /// <param name="body">The email body (HTML format)</param>
    /// <param name="imageBase64">Optional base64-encoded image data to attach</param>
    /// <param name="imageName">Optional image filename (default: image.png)</param>
    /// <returns>A response indicating success or failure</returns>
    [McpServerTool(Name = "send_email")]
    [Description("Sends an email with optional image attachment")]
    public async Task<EmailResponse> SendEmail(
        [Description("The recipient email address")] string toEmail,
        [Description("The email subject")] string subject,
        [Description("The email body in HTML format")] string body,
        [Description("Optional base64-encoded image data to attach")] string? imageBase64 = null,
        [Description("Optional image filename (default: image.png)")] string? imageName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending email to {Email} with subject: {Subject}", toEmail, subject);

            byte[]? imageData = null;
            if (!string.IsNullOrEmpty(imageBase64))
            {
                try
                {
                    imageData = Convert.FromBase64String(imageBase64);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Invalid base64 image data provided");
                    return new EmailResponse
                    {
                        Success = false,
                        Message = "Invalid base64 image data format"
                    };
                }
            }

            // HTML encode the body to prevent XSS
            var safeBody = WebUtility.HtmlEncode(body);

            await _emailService.SendImageEmailAsync(
                toEmail,
                subject,
                safeBody,
                imageData,
                imageName ?? "image.png",
                cancellationToken);

            return new EmailResponse
            {
                Success = true,
                Message = $"Email sent successfully to {toEmail}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return new EmailResponse
            {
                Success = false,
                Message = $"Failed to send email: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Response model for email operations
/// </summary>
public class EmailResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
}
