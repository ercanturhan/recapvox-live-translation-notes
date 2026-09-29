using System.Net.WebSockets;
using System.Text.Json;

namespace Translator.Desktop;

internal sealed class GeminiTranslationSession : RealtimeWebSocketSession
{
    protected override string ProviderName => "Gemini 3.5 Live Translate";
    protected override Uri Endpoint(string key) => new("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" + Uri.EscapeDataString(key));
    protected override void ConfigureSocket(ClientWebSocketOptions options, string key) { }
    protected override string SetupMessage(string targetLanguage, string? sourceLanguage) => JsonSerializer.Serialize(new
    {
        setup = new
        {
            model = "models/gemini-3.5-live-translate-preview",
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                inputAudioTranscription = new { },
                outputAudioTranscription = new { },
                translationConfig = new { targetLanguageCode = targetLanguage, echoTargetLanguage = true }
            }
        }
    });
    protected override string AudioMessage(byte[] pcm16) => JsonSerializer.Serialize(new
    {
        realtimeInput = new { audio = new { data = Convert.ToBase64String(pcm16), mimeType = "audio/pcm;rate=16000" } }
    });
    protected override string? FinishMessage => "{\"realtimeInput\":{\"audioStreamEnd\":true}}";
    protected override bool IsReady(JsonElement message) => message.TryGetProperty("setupComplete", out _);

    protected override void HandleContent(JsonElement message)
    {
        if (!message.TryGetProperty("serverContent", out var content)) return;
        if (content.TryGetProperty("inputTranscription", out var original)
            && original.TryGetProperty("text", out var sourceText)) Original(sourceText.GetString() ?? "");
        if (content.TryGetProperty("outputTranscription", out var translated)
            && translated.TryGetProperty("text", out var targetText)) Translation(targetText.GetString() ?? "");
    }
}
