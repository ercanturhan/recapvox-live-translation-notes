namespace Translator.Desktop;

internal interface ILiveSession
{
    event Action<IReadOnlyList<TranscriptSegment>>? TextChanged;
    event Action<string>? Failed;
    int CommittedOriginalCount { get; }
    bool IsConnected { get; }
    Task ConnectAsync(string key, string targetLanguage, string? sourceLanguage, CancellationToken cancellation);
    bool TryQueueAudio(byte[] data);
    Task StopAsync();
}

internal static class LiveProviders
{
    public const string Soniox = "Soniox";
    public const string Hybrid = "Soniox + DeepSeek";
    public const string OpenAi = "OpenAI GPT-Realtime-Translate";
    public const string Gemini = "Gemini 3.5 Live Translate";

    public static string CredentialProvider(string liveProvider) => liveProvider switch
    {
        Soniox => Soniox,
        Hybrid => "DeepSeek",
        OpenAi => "OpenAI",
        Gemini => "Gemini",
        _ => throw new ArgumentException("Bilinmeyen canlı çeviri sağlayıcısı.", nameof(liveProvider))
    };

    public static ILiveSession Create(string liveProvider) => liveProvider switch
    {
        Soniox or Hybrid => new SonioxSession(),
        OpenAi => new OpenAiTranslationSession(),
        Gemini => new GeminiTranslationSession(),
        _ => throw new ArgumentException("Bilinmeyen canlı çeviri sağlayıcısı.", nameof(liveProvider))
    };

    public static ILiveSession CreateTranscription(string liveProvider) => liveProvider switch
    {
        Soniox or Hybrid => new SonioxSession(),
        OpenAi => new OpenAiTranscriptionSession(),
        Gemini => new GeminiTranscriptionSession(),
        _ => throw new ArgumentException("Bilinmeyen transkripsiyon sağlayıcısı.", nameof(liveProvider))
    };
}
