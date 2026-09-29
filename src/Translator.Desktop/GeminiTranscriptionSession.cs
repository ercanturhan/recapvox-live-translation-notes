using System.Net.WebSockets;
using System.Text.Json;
using System.Globalization;

namespace Translator.Desktop;

internal sealed class GeminiTranscriptionSession : RealtimeWebSocketSession
{
    protected override string ProviderName => "Gemini 3.5 Transcribe Live";
    protected override Uri Endpoint(string key) => new("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" + Uri.EscapeDataString(key));
    protected override void ConfigureSocket(ClientWebSocketOptions options, string key) { }
    protected override string SetupMessage(string targetLanguage, string? sourceLanguage) => JsonSerializer.Serialize(new
    {
        setup = new
        {
            model = "models/gemini-3.5-transcribe-live",
            generationConfig = new { responseModalities = new[] { "TEXT" } },
            inputAudioTranscription = new { languageCodes = string.IsNullOrWhiteSpace(sourceLanguage) ? Array.Empty<string>() : new[] { SpecificLanguage(sourceLanguage) } }
        }
    });
    protected override string AudioMessage(byte[] pcm16) => JsonSerializer.Serialize(new
    {
        realtimeInput = new { audio = new { data = Convert.ToBase64String(pcm16), mimeType = "audio/pcm;rate=16000" } }
    });
    protected override string? FinishMessage => "{\"realtimeInput\":{\"audioStreamEnd\":true}}";
    protected override bool IsReady(JsonElement message) => message.TryGetProperty("setupComplete", out _);
    private static string SpecificLanguage(string code)
    {
        try { return CultureInfo.CreateSpecificCulture(code).Name; }
        catch (CultureNotFoundException) { return code; }
    }
    protected override void HandleContent(JsonElement message)
    {
        if (!message.TryGetProperty("serverContent", out var content)) return;
        if (content.TryGetProperty("inputTranscription", out var final)
            && final.TryGetProperty("text", out var text)) Original(text.GetString() ?? "");
    }
}
