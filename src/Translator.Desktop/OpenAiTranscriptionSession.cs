using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Translator.Desktop;

internal sealed class OpenAiTranscriptionSession : RealtimeWebSocketSession
{
    private readonly Pcm16Upsampler _upsampler = new();
    private readonly HashSet<string> _deltaItems = [];
    private int _pendingBytes;

    protected override string ProviderName => "OpenAI GPT-Live-Transcribe";
    protected override Uri Endpoint(string key) => new("wss://api.openai.com/v1/realtime?model=gpt-live-transcribe");
    protected override void ConfigureSocket(ClientWebSocketOptions options, string key) => options.SetRequestHeader("Authorization", "Bearer " + key);
    protected override string SetupMessage(string targetLanguage, string? sourceLanguage) => JsonSerializer.Serialize(new
    {
        type = "session.update",
        session = new
        {
            type = "transcription",
            audio = new
            {
                input = new
                {
                    format = new { type = "audio/pcm", rate = 24000 },
                    transcription = new { model = "gpt-live-transcribe", delay = "low", languages = string.IsNullOrWhiteSpace(sourceLanguage) ? null : new[] { sourceLanguage } },
                    turn_detection = (object?)null
                }
            }
        }
    }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    protected override string AudioMessage(byte[] pcm16)
    {
        var upsampled = _upsampler.Convert(pcm16);
        _pendingBytes += upsampled.Length;
        return JsonSerializer.Serialize(new { type = "input_audio_buffer.append", audio = Convert.ToBase64String(upsampled) });
    }
    protected override string? AfterAudioChunk(byte[] pcm16)
    {
        if (_pendingBytes < 24000 * 2 * 4) return null;
        _pendingBytes = 0;
        return "{\"type\":\"input_audio_buffer.commit\"}";
    }
    protected override string? FinishMessage => _pendingBytes > 0 ? "{\"type\":\"input_audio_buffer.commit\"}" : null;
    protected override bool IsReady(JsonElement message) => message.TryGetProperty("type", out var type) && type.GetString() == "session.updated";
    protected override void HandleContent(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var kind)) return;
        var itemId = message.TryGetProperty("item_id", out var id) ? id.GetString() ?? "" : "";
        if (kind.GetString() == "conversation.item.input_audio_transcription.delta" && message.TryGetProperty("delta", out var delta))
        {
            _deltaItems.Add(itemId);
            Original(delta.GetString() ?? "");
        }
        else if (kind.GetString() == "conversation.item.input_audio_transcription.completed"
            && !_deltaItems.Contains(itemId) && message.TryGetProperty("transcript", out var final))
            Original(final.GetString() ?? "");
    }
}
