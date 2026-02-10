using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Options;
using OpusSharp.Core;
using OpusSharp.Core.Extensions;
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
    Task<AudioSynthesisResult> SynthesizeOggAsync(
        string text,
        string? voiceName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Speech 语音合成服务实现
/// </summary>
public class AudioSynthesisService : IAudioSynthesisService
{
    private readonly AzureSpeechSettings _settings;
    private readonly ILogger<AudioSynthesisService> _logger;

    private const int MaxSpeechSeconds = 30;
    private const double EstimatedCharsPerSecond = 6.0;
    private const int TargetSampleRate = 16000;
    private const int TargetChannels = 1;
    private const int OpusFrameDurationMs = 60;
    private const int OpusMaxPacketSize = 4000;
    private const int OpusGranuleSampleRate = 48000;
    private const int OpusFramesPerPage = 17;
    private const ushort OpusPreSkip = 312;
    private const int OpusTargetBitrate = 16000;

    private static readonly Regex EmojiRegex = new(
        @"(\u00a9|\u00ae|[\u2000-\u3300]|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]|[\u2702-\u27b0])",
        RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"https?://\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MarkdownLinkRegex = new(
        @"\[([^\]]+)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownCodeBlockRegex = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownBlockquoteRegex = new(
        @"^\s*>+\s?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MarkdownHeadingRegex = new(
        @"^\s*#{1,6}\s+",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MarkdownListPrefixRegex = new(
        @"^\s*([-*+]\s+|\d+\.\s+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MarkdownStrongEmRegex = new(
        @"(\*\*|__|\*|_)",
        RegexOptions.Compiled);

    private static readonly Regex InlineCodeRegex = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    private static readonly Regex MaskedContentRegex = new(
        @"[\*＊]{2,}",
        RegexOptions.Compiled);

    public AudioSynthesisService(
        IOptions<AzureSpeechSettings> settings,
        ILogger<AudioSynthesisService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AudioSynthesisResult> SynthesizeOggAsync(
        string text,
        string? voiceName = null,
        CancellationToken cancellationToken = default)
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

            var selectedVoice = string.IsNullOrWhiteSpace(voiceName)
                ? _settings.SpeechSynthesisVoiceName
                : voiceName;

            if (!string.IsNullOrWhiteSpace(selectedVoice))
            {
                speechConfig.SpeechSynthesisVoiceName = selectedVoice;
            }

            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);

            var cleanedText = NormalizeForSpeech(text);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                return new AudioSynthesisResult
                {
                    Success = false,
                    ErrorMessage = "Text is empty after cleanup"
                };
            }

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

            var oggBytes = EncodePcmToOggOpus(result.AudioData, TargetSampleRate, TargetChannels);
            if (oggBytes.Length == 0)
            {
                return new AudioSynthesisResult
                {
                    Success = false,
                    ErrorMessage = "Failed to encode audio"
                };
            }

            return new AudioSynthesisResult
            {
                Success = true,
                AudioBytes = oggBytes
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

    private static string NormalizeForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text;
        cleaned = MarkdownCodeBlockRegex.Replace(cleaned, " ");
        cleaned = InlineCodeRegex.Replace(cleaned, " ");
        cleaned = MarkdownLinkRegex.Replace(cleaned, "$1");
        cleaned = MarkdownBlockquoteRegex.Replace(cleaned, " ");
        cleaned = MarkdownHeadingRegex.Replace(cleaned, " ");
        cleaned = MarkdownListPrefixRegex.Replace(cleaned, " ");
        cleaned = MarkdownStrongEmRegex.Replace(cleaned, " ");
        cleaned = UrlRegex.Replace(cleaned, " ");
        cleaned = RemoveEmojis(cleaned);
        cleaned = MaskedContentRegex.Replace(cleaned, " ");
        cleaned = MultiSpaceRegex.Replace(cleaned, " ").Trim();

        return TruncateForSpeech(cleaned);
    }

    private static byte[] EncodePcmToOggOpus(byte[] pcmBytes, int sampleRate, int channels)
    {
        if (pcmBytes is not { Length: > 0 })
        {
            return Array.Empty<byte>();
        }

        if (pcmBytes.Length % 2 != 0)
        {
            return Array.Empty<byte>();
        }

        if (!IsSupportedSampleRate(sampleRate) || channels is < 1 or > 2)
        {
            return Array.Empty<byte>();
        }

        var samplesPerChannel = sampleRate * OpusFrameDurationMs / 1000;
        if (samplesPerChannel <= 0)
        {
            return Array.Empty<byte>();
        }

        var bytesPerFrame = samplesPerChannel * channels * sizeof(short);

        using var output = new MemoryStream();
        using var encoder = new OpusEncoder(sampleRate, channels, OpusPredefinedValues.OPUS_APPLICATION_AUDIO);
        encoder.SetBitRate(OpusTargetBitrate);
        encoder.SetVbr(true);
        using var oggWriter = new OggOpusWriter(output, sampleRate, channels);

        oggWriter.WriteOpusHead();
        oggWriter.WriteOpusTags();

        var encodedBuffer = new byte[OpusMaxPacketSize];
        var offset = 0;
        while (offset < pcmBytes.Length)
        {
            var remaining = pcmBytes.Length - offset;
            var bytesToCopy = Math.Min(remaining, bytesPerFrame);
            var frameBuffer = new byte[bytesPerFrame];
            Buffer.BlockCopy(pcmBytes, offset, frameBuffer, 0, bytesToCopy);

            var encodedBytes = encoder.Encode(frameBuffer, samplesPerChannel, encodedBuffer, encodedBuffer.Length);
            if (encodedBytes <= 0)
            {
                return Array.Empty<byte>();
            }

            var packet = new byte[encodedBytes];
            Buffer.BlockCopy(encodedBuffer, 0, packet, 0, encodedBytes);

            var actualSamplesPerChannel = bytesToCopy / (sizeof(short) * channels);
            var isLast = offset + bytesToCopy >= pcmBytes.Length;
            oggWriter.WriteAudioPacket(packet, actualSamplesPerChannel, isLast);

            offset += bytesToCopy;
        }

        return output.ToArray();
    }

    private static bool IsSupportedSampleRate(int sampleRate)
        => sampleRate is 8000 or 12000 or 16000 or 24000 or 48000;

    private sealed class OggOpusWriter : IDisposable
    {
        private static readonly byte[] OpusHeadMagic = Encoding.ASCII.GetBytes("OpusHead");
        private static readonly byte[] OpusTagsMagic = Encoding.ASCII.GetBytes("OpusTags");
        private static readonly uint[] CrcTable = BuildCrcTable();

        private static readonly byte[] TagVendorBytes = Encoding.ASCII.GetBytes("Lavf62.3.100");
        private static readonly byte[][] TagComments =
        {
            Encoding.ASCII.GetBytes("encoder=Lavc62.11.100 libopus")
        };

        private readonly Stream _output;
        private readonly int _inputSampleRate;
        private readonly int _channels;
        private readonly int _serial;
        private int _pageSequence;
        private long _granulePosition;

        public OggOpusWriter(Stream output, int inputSampleRate, int channels)
        {
            _output = output;
            _inputSampleRate = inputSampleRate;
            _channels = channels;
            _serial = Random.Shared.Next();
            _pageSequence = 0;
            _granulePosition = 0;
        }

        public void WriteOpusHead()
        {
            using var packetStream = new MemoryStream();
            using var writer = new BinaryWriter(packetStream, Encoding.UTF8, leaveOpen: true);

            writer.Write(OpusHeadMagic);
            writer.Write((byte)1);
            writer.Write((byte)_channels);
            writer.Write(OpusPreSkip);
            writer.Write(_inputSampleRate);
            writer.Write((short)0); // output gain
            writer.Write((byte)0); // channel mapping family

            writer.Flush();
            WritePage(new[] { packetStream.ToArray() }, headerType: 0x02, granulePosition: 0);
        }

        public void WriteOpusTags()
        {
            using var packetStream = new MemoryStream();
            using var writer = new BinaryWriter(packetStream, Encoding.UTF8, leaveOpen: true);

            writer.Write(OpusTagsMagic);
            writer.Write(TagVendorBytes.Length);
            writer.Write(TagVendorBytes);
            writer.Write(TagComments.Length);
            foreach (var comment in TagComments)
            {
                writer.Write(comment.Length);
                writer.Write(comment);
            }

            writer.Flush();
            WritePage(new[] { packetStream.ToArray() }, headerType: 0x00, granulePosition: 0);
        }

        public void WriteAudioPacket(byte[] packet, int samplesPerChannel, bool isLast)
        {
            if (samplesPerChannel < 0)
            {
                samplesPerChannel = 0;
            }

            var granuleIncrement = (long)samplesPerChannel * OpusGranuleSampleRate / _inputSampleRate;
            _granulePosition += granuleIncrement;

            _packetQueue ??= new List<byte[]>();
            _packetQueue.Add(packet);
            _queuedFrames++;

            if (_queuedFrames >= OpusFramesPerPage || isLast)
            {
                var headerType = isLast ? (byte)0x04 : (byte)0x00;
                WritePage(_packetQueue, headerType, _granulePosition);
                _packetQueue.Clear();
                _queuedFrames = 0;
            }
        }

        public void Dispose()
        {
        }

        private List<byte[]>? _packetQueue;
        private int _queuedFrames;

        private void WritePage(IReadOnlyList<byte[]> packets, byte headerType, long granulePosition)
        {
            var totalSegmentCount = 0;
            foreach (var packet in packets)
            {
                totalSegmentCount += Math.Max(1, (packet.Length + 254) / 255);
            }

            var segmentTable = new byte[totalSegmentCount];
            var segmentIndex = 0;
            foreach (var packet in packets)
            {
                var remaining = packet.Length;
                var segments = Math.Max(1, (packet.Length + 254) / 255);
                for (var i = 0; i < segments; i++)
                {
                    var segmentSize = Math.Min(255, remaining);
                    segmentTable[segmentIndex++] = (byte)segmentSize;
                    remaining -= segmentSize;
                }
            }

            using var pageStream = new MemoryStream();
            using var writer = new BinaryWriter(pageStream, Encoding.UTF8, leaveOpen: true);

            writer.Write(Encoding.ASCII.GetBytes("OggS"));
            writer.Write((byte)0);
            writer.Write(headerType);
            writer.Write(granulePosition);
            writer.Write(_serial);
            writer.Write(_pageSequence++);
            writer.Write(0u);
            writer.Write((byte)segmentTable.Length);
            writer.Write(segmentTable);
            foreach (var packet in packets)
            {
                if (packet.Length > 0)
                {
                    writer.Write(packet);
                }
            }

            writer.Flush();
            var pageData = pageStream.ToArray();
            var crc = ComputeCrc(pageData);
            pageData[22] = (byte)(crc & 0xFF);
            pageData[23] = (byte)((crc >> 8) & 0xFF);
            pageData[24] = (byte)((crc >> 16) & 0xFF);
            pageData[25] = (byte)((crc >> 24) & 0xFF);

            _output.Write(pageData, 0, pageData.Length);
        }

        private static uint ComputeCrc(byte[] data)
        {
            uint crc = 0;
            for (var i = 0; i < data.Length; i++)
            {
                var index = (byte)((crc >> 24) ^ data[i]);
                crc = (crc << 8) ^ CrcTable[index];
            }

            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var r = i << 24;
                for (var j = 0; j < 8; j++)
                {
                    r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
                }

                table[i] = r;
            }

            return table;
        }
    }

    private static string TruncateForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var maxChars = (int)Math.Floor(MaxSpeechSeconds * EstimatedCharsPerSecond);
        if (text.Length <= maxChars)
        {
            return text;
        }

        var slice = text[..maxChars];
        var lastBoundary = slice.LastIndexOfAny(new[] { '。', '！', '？', '!', '?', '；', ';', '.', '…' });
        if (lastBoundary >= Math.Max(20, maxChars / 2))
        {
            return slice[..(lastBoundary + 1)].Trim();
        }

        return slice.Trim();
    }
}
