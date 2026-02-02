using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Verdure.Mcp.Server.Settings;
using ModelContextProtocol.Server;
using Verdure.Mcp.Infrastructure.Services;
using Verdure.Mcp.Server.Services;
using Hangfire;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// MCP Tool to pick a random audio file from wwwroot and push its URL to device(s).
/// </summary>
[McpServerToolType]
public class MusicTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _env;
    private readonly IDevicePushService _devicePushService;
    private readonly ILogger<MusicTool> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ImageStorageSettings _imageStorageSettings;

    public MusicTool(
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment env,
        IDevicePushService devicePushService,
        ILogger<MusicTool> logger,
        IBackgroundJobClient backgroundJobClient,
        IOptions<ImageStorageSettings>? imageSettings = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _env = env;
        _devicePushService = devicePushService;
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
        _imageStorageSettings = imageSettings?.Value ?? new ImageStorageSettings();
    }

    /// <summary>
    /// Select a random audio file from the `wwwroot/audio` folder and push it to the user
    /// identified by the `X-User-Id` request header.
    /// The pushed message follows the same shape as used in `test-send-message.ps1` (action = "audio", url = "...").
    /// </summary>
    [McpServerTool(Name = "play_random_music")]
    [Description("Plays a random audio file from wwwroot/audio by pushing an audio message to the user's devices")]
    public async Task<MusicResponse> PlayRandomMusic(CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var effectiveUserId = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(effectiveUserId))
            {
                _logger.LogWarning("No userId provided and X-User-Id header is missing");
                return new MusicResponse { Success = false, Message = "Missing userId or X-User-Id header" };
            }

            var webRoot = _env.WebRootPath ?? _env.ContentRootPath;
            var folder = "audios";
            var audioFolder = Path.Combine(webRoot, folder);

            if (!Directory.Exists(audioFolder))
            {
                _logger.LogWarning("Audio folder does not exist: {AudioFolder}", audioFolder);
                return new MusicResponse { Success = false, Message = $"Audio folder not found: {folder}" };
            }

            // Find audio files (ogg, mp3) and pick a random one
            var files = Directory.GetFiles(audioFolder)
                .Where(f => f.EndsWith('.' + "ogg") || f.EndsWith('.' + "mp3") || f.EndsWith('.' + "wav"))
                .ToArray();

            if (files.Length == 0)
            {
                _logger.LogWarning("No audio files found in {AudioFolder}", audioFolder);
                return new MusicResponse { Success = false, Message = "No audio files found" };
            }

            var rnd = new Random();
            var chosen = files[rnd.Next(files.Length)];
            var fileName = Path.GetFileName(chosen);

            string url;
            // Prefer configured ImageStorage BaseUrl (keeps image and audio base URL consistent)
            if (!string.IsNullOrWhiteSpace(_imageStorageSettings.BaseUrl))
            {
                var cfgBase = _imageStorageSettings.BaseUrl.TrimEnd('/');
                url = $"{cfgBase}/{folder}/{Uri.EscapeDataString(fileName)}";
            }
            else
            {
                var req = httpContext?.Request;
                var hostBase = req != null ? $"{req.Scheme}://{req.Host.Value}" : string.Empty;
                url = string.IsNullOrEmpty(hostBase)
                    ? $"/{folder}/{Uri.EscapeDataString(fileName)}"
                    : $"{hostBase}/{folder}/{Uri.EscapeDataString(fileName)}";
            }

            var title = Path.GetFileNameWithoutExtension(fileName);

            var message = new
            {
                action = "audio",
                url,
                title
            };

            // Schedule push as a delayed background job so device can play result first.
            try
            {

                var jobDelay = TimeSpan.FromSeconds(1);

                _logger.LogInformation("Scheduling audio push to user {UserId} after {Delay}s: {Url}",
                    effectiveUserId, jobDelay.TotalSeconds, url);

                _backgroundJobClient.Schedule<MusicPushBackgroundJob>(
                    job => job.ExecuteAsync(effectiveUserId, url, title, CancellationToken.None),
                    jobDelay);

                return new MusicResponse { Success = true, Message = "Audio scheduled", Url = url, FileName = fileName };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule audio push for user {UserId}", effectiveUserId);
                return new MusicResponse { Success = false, Message = ex.Message };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play random music");
            return new MusicResponse { Success = false, Message = ex.Message };
        }
    }
}

public class MusicResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public string? Url { get; set; }
    public string? FileName { get; set; }
}
