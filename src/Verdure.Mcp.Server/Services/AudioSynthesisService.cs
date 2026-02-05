using System.Text.RegularExpressions;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Options;
using Verdure.Mcp.Server.Settings;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// 语音合成结果
/// </summary>
public class AudioSynthesisResult
{
    public bool Success { get; set; }
    public byte[]? AudioBytes { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 语音合成服务接口
/// </summary>
public interface IAudioSynthesisService
{
    Task<AudioSynthesisResult> SynthesizeOggAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Speech 语音合成服务实现
/// </summary>
public class AudioSynthesisService : IAudioSynthesisService
{
    private readonly AzureSpeechSettings _settings;
    private readonly ILogger<AudioSynthesisService> _logger;

    private static readonly Regex EmojiRegex = new(
        @"(\u00a9|\u00ae|[\u2000-\u3300]|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]|[\u2702-\u27b0])",
        RegexOptions.Compiled);

    public AudioSynthesisService(
        IOptions<AzureSpeechSettings> settings,
        ILogger<AudioSynthesisService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AudioSynthesisResult> SynthesizeOggAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AudioSynthesisResult
            {
                Success = false,
                ErrorMessage = "Text is empty"
            };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(_settings.SubscriptionKey) || string.IsNullOrWhiteSpace(_settings.Region))
            {
                return new AudioSynthesisResult
                {
                    Success = false,
                    ErrorMessage = "AzureSpeech settings are not configured"
                };
            }

            var speechConfig = SpeechConfig.FromSubscription(_settings.SubscriptionKey, _settings.Region);
            if (!string.IsNullOrWhiteSpace(_settings.SpeechSynthesisLanguage))
            {
                speechConfig.SpeechSynthesisLanguage = _settings.SpeechSynthesisLanguage;
            }

            if (!string.IsNullOrWhiteSpace(_settings.SpeechSynthesisVoiceName))
            {
                speechConfig.SpeechSynthesisVoiceName = _settings.SpeechSynthesisVoiceName;
            }

            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Ogg16Khz16BitMonoOpus);

            var cleanedText = RemoveEmojis(text);

            using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig: null);
            var result = await synthesizer.SpeakTextAsync(cleanedText).ConfigureAwait(false);

            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                var details = SpeechSynthesisCancellationDetails.FromResult(result);
                _logger.LogWarning("Speech synthesis failed: {Reason}, {ErrorDetails}", details.Reason, details.ErrorDetails);
                return new AudioSynthesisResult
                {
                    Success = false,
                    ErrorMessage = details.ErrorDetails
                };
            }

            return new AudioSynthesisResult
            {
                Success = true,
                AudioBytes = result.AudioData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synthesize speech");
            return new AudioSynthesisResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string RemoveEmojis(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return EmojiRegex.Replace(text, string.Empty);
    }
}
