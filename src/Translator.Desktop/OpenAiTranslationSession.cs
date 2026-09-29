using System.Net.WebSockets;
using System.Text.Json;

namespace Translator.Desktop;

internal sealed class OpenAiTranslationSession : RealtimeWebSocketSession
{
    private readonly Pcm16Upsampler _upsampler = new();
    protected override string ProviderName => "OpenAI GPT-Realtime-Translate";
    protected override Uri Endpoint(string key) => new("wss://api.openai.com/v1/realtime/translations?model=gpt-realtime-translate");
    protected override void ConfigureSocket(ClientWebSocketOptions options, string key) => options.SetRequestHeader("Authorization", "Bearer " + key);
    protected override string SetupMessage(string targetLanguage, string? sourceLanguage) => JsonSerializer.Serialize(new
    {
        type = "session.update",
        session = new { audio = new { output = new { language = targetLanguage } } }
    });
    protected override string AudioMessage(byte[] pcm16) => JsonSerializer.Serialize(new
    {
        type = "session.input_audio_buffer.append",
        audio = Convert.ToBase64String(_upsampler.Convert(pcm16))
    });
    protected override string? FinishMessage => "{\"type\":\"session.close\"}";
    protected override bool IsReady(JsonElement message) => message.TryGetProperty("type", out var type) && type.GetString() == "session.updated";
    protected override bool IsFinished(JsonElement message) => message.TryGetProperty("type", out var type) && type.GetString() == "session.closed";

    protected override void HandleContent(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var kind)) return;
        var type = kind.GetString();
        if (type is not ("session.input_transcript.delta" or "session.output_transcript.delta")
            || !message.TryGetProperty("delta", out var delta)) return;
        var text = delta.GetString() ?? "";
        if (type == "session.input_transcript.delta")
        {
            var start = message.TryGetProperty("start_ms", out var time) && time.TryGetInt64(out var ms)
                ? TimeSpan.FromMilliseconds(ms) : Elapsed;
            Original(text, start);
        }
        else Translation(text);
    }
}
