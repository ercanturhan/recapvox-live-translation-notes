using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Translator.Desktop;

internal static class SummaryClient
{
    internal sealed record GenerationResult(string Text, int? InputTokens, int? OutputTokens,
        int? CacheHitTokens = null, int? CacheMissTokens = null);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static string DefaultModel(string provider) => provider switch
    {
        "DeepSeek" => "deepseek-flash",
        "OpenAI" => "gpt-5.6-luna",
        "Gemini" => "gemini-3.5-flash",
        "Claude" => "claude-sonnet-5",
        _ => throw new ArgumentException("Bilinmeyen sağlayıcı.", nameof(provider))
    };

    public static async Task<string> SummarizeAsync(string provider, string model, string key,
        string kind, bool detailed, string summaryLanguage, IReadOnlyList<TranscriptSegment> segments, CancellationToken cancellation)
        => (await SummarizeWithUsageAsync(provider, model, key, kind, detailed, summaryLanguage, segments, cancellation)).Text;

    public static async Task<GenerationResult> SummarizeWithUsageAsync(string provider, string model, string key,
        string kind, bool detailed, string summaryLanguage, IReadOnlyList<TranscriptSegment> segments, CancellationToken cancellation)
    {
        if (segments.Count == 0) throw new InvalidOperationException("Özetlenecek konuşma henüz yok.");
        var prompt = BuildSummaryPrompt(kind, detailed, summaryLanguage, segments);
        return await GenerateWithUsageAsync(provider, model, key, prompt, detailed ? 2400 : 1200, cancellation);
    }

    internal static string BuildSummaryPrompt(string kind, bool detailed, string summaryLanguage, IReadOnlyList<TranscriptSegment> segments)
    {
        var source = string.Join("\n", segments.Where(x => !string.IsNullOrWhiteSpace(x.Original))
            .Select(x => x.Original));
        if (source.Length > 60000)
            throw new InvalidOperationException("Bu oturum tek istek için çok uzun. Uzun oturumları parçalayan özetleme sonraki aşamada eklenecek.");
        var prompt = $"Aşağıdaki {kind} konuşmasını {summaryLanguage} dilinde özetle. " +
            (detailed ? "Ayrıntılı ama tekrarsız yaz. " : "Kısa ve öz yaz. ") +
            (kind == "Toplantı" ? "Başlıklar: ana konular, kararlar, görevler ve sahipleri, tarihler, açık sorular. "
                : "Başlıklar: ana fikir, önemli noktalar, öğrenilenler, açık sorular. ") +
            "Özet metnine zaman damgası, bölüm numarası veya kaynak referansı ekleme. Konuşmada olmayan karar, görev, tarih veya olguyu uydurma; yoksa 'Belirtilmedi' yaz.\n\n" + source;
        return prompt;
    }

    internal static async Task<string> GenerateAsync(string provider, string model, string key,
        string prompt, int maxTokens, CancellationToken cancellation, HttpClient? client = null)
        => (await GenerateWithUsageAsync(provider, model, key, prompt, maxTokens, cancellation, client)).Text;

    internal static async Task<GenerationResult> GenerateWithUsageAsync(string provider, string model, string key,
        string prompt, int maxTokens, CancellationToken cancellation, HttpClient? client = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException(provider + " API anahtarı gerekli.");
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Model adı gerekli.");
        var (url, body) = provider switch
        {
            "DeepSeek" => ("https://api.deepseek.com/chat/completions", new { model, messages = new[] { new { role = "user", content = prompt } }, max_tokens = maxTokens, thinking = new { type = "disabled" } } as object),
            "OpenAI" => ("https://api.openai.com/v1/responses", new { model, input = prompt, max_output_tokens = maxTokens, store = false } as object),
            "Gemini" => ($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent", new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { maxOutputTokens = maxTokens } } as object),
            "Claude" => ("https://api.anthropic.com/v1/messages", new { model, max_tokens = maxTokens, messages = new[] { new { role = "user", content = prompt } } } as object),
            _ => throw new ArgumentException("Bilinmeyen sağlayıcı.", nameof(provider))
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (provider is "OpenAI" or "DeepSeek") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        else if (provider == "Gemini") request.Headers.Add("x-goog-api-key", key);
        else { request.Headers.Add("x-api-key", key); request.Headers.Add("anthropic-version", "2023-06-01"); }
        using var response = await (client ?? Http).SendAsync(request, cancellation);
        var json = await response.Content.ReadAsStringAsync(cancellation);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{provider} HTTP {(int)response.StatusCode}: {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var result = provider switch
        {
            "DeepSeek" => root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString(),
            "Gemini" => string.Join("", root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")
                .EnumerateArray().Select(x => x.TryGetProperty("text", out var t) ? t.GetString() : "")),
            "Claude" => string.Join("", root.GetProperty("content").EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(x => x.GetProperty("text").GetString())),
            "OpenAI" => string.Join("", root.GetProperty("output").EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "message")
                .SelectMany(x => x.GetProperty("content").EnumerateArray())
                .Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "output_text")
                .Select(x => x.GetProperty("text").GetString())),
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(result))
        {
            var usageName = provider == "Gemini" ? "usageMetadata" : "usage";
            int? input = null, output = null;
            int? hit = null, miss = null;
            if (root.TryGetProperty(usageName, out var usage))
            {
                var inputName = provider switch { "DeepSeek" => "prompt_tokens", "Gemini" => "promptTokenCount", _ => "input_tokens" };
                var outputName = provider switch { "DeepSeek" => "completion_tokens", "Gemini" => "candidatesTokenCount", _ => "output_tokens" };
                if (usage.TryGetProperty(inputName, out var inputValue) && inputValue.TryGetInt32(out var i)) input = i;
                if (usage.TryGetProperty(outputName, out var outputValue) && outputValue.TryGetInt32(out var o)) output = o;
                if (provider == "DeepSeek")
                {
                    if (usage.TryGetProperty("prompt_cache_hit_tokens", out var hitValue) && hitValue.TryGetInt32(out var h)) hit = h;
                    if (usage.TryGetProperty("prompt_cache_miss_tokens", out var missValue) && missValue.TryGetInt32(out var m)) miss = m;
                }
            }
            return new GenerationResult(result.Trim(), input, output, hit, miss);
        }
        if (provider == "DeepSeek" && root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("finish_reason", out var reason) && reason.GetString() == "length")
            throw new InvalidOperationException("DeepSeek yanıt sınırına ulaştı. Daha kısa oturum veya ayrıntılı özet deneyin.");
        throw new InvalidOperationException(provider + " metin içermeyen yanıt verdi. Model ve sağlayıcı durumunu kontrol edin.");
    }

    private static string ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var error = document.RootElement.GetProperty("error");
            return error.TryGetProperty("message", out var message) ? (message.GetString() ?? "İstek reddedildi.")[..Math.Min(message.GetString()?.Length ?? 0, 240)] : "İstek reddedildi.";
        }
        catch { return "İstek reddedildi. Anahtarı ve model adını kontrol edin."; }
    }
}
