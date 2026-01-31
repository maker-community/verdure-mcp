using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// Background job to push music/audio messages to user devices after a delay.
/// </summary>
public class MusicPushBackgroundJob
{
    private readonly IDevicePushService _devicePushService;
    private readonly ILogger<MusicPushBackgroundJob> _logger;

    public MusicPushBackgroundJob(IDevicePushService devicePushService, ILogger<MusicPushBackgroundJob> logger)
    {
        _devicePushService = devicePushService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string userId, string url, string title, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing MusicPushBackgroundJob: user={UserId}, url={Url}", userId, url);

        var message = new
        {
            action = "audio",
            url,
            title
        };

        try
        {
            await _devicePushService.SendCustomMessageAsync(userId, message, cancellationToken);
            _logger.LogInformation("Music pushed to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push music to user {UserId}", userId);
        }
    }
}
