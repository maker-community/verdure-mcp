using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Verdure.Mcp.Infrastructure.Services;

/// <summary>
/// Settings for email configuration
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";
    
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Verdure MCP";
}

/// <summary>
/// Interface for email service
/// </summary>
public interface IEmailService
{
    Task SendImageEmailAsync(string toEmail, string subject, string body, byte[]? imageData, string? imageName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Email service using MailKit
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendImageEmailAsync(string toEmail, string subject, string body, byte[]? imageData, string? imageName, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };

            // Attach the image if provided
            if (imageData != null && imageData.Length > 0)
            {
                var fileName = imageName ?? "generated_image.png";
                builder.Attachments.Add(fileName, imageData, new ContentType("image", "png"));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            
            var secureSocketOptions = _settings.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.None;
            
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, cancellationToken);
            
            if (!string.IsNullOrEmpty(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
            }
            
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}
