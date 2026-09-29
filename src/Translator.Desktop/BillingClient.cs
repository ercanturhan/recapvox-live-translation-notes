using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Translator.Desktop;

internal static class BillingClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    // DeepSeek's published Flash token rates as of 2026-09-29; holidays and later price changes
    // can change the invoice, so this value must always be displayed as an estimate.
    public static decimal? EstimateDeepSeekFlash(SummaryClient.GenerationResult usage, DateTimeOffset utcTime)
    {
        if (usage.InputTokens is null || usage.OutputTokens is null) return null;
        var utc = utcTime.UtcDateTime;
        var peak = utc.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday &&
            (utc.Hour is >= 1 and < 4 or >= 6 and < 10);
        var factor = peak ? 1m : 0.5m;
        var hit = usage.CacheHitTokens ?? 0;
        var miss = usage.CacheMissTokens ?? Math.Max(0, usage.InputTokens.Value - hit);
        return (hit * 0.006m + miss * 0.3m + usage.OutputTokens.Value * 1.2m) * factor / 1_000_000m;
    }

    public static async Task<string> DeepSeekBalanceAsync(string key, CancellationToken cancellation, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await (client ?? Http).SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"DeepSeek HTTP {(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var root = document.RootElement;
        var balances = root.GetProperty("balance_infos").EnumerateArray()
            .Select(x => x.GetProperty("total_balance").GetString() + " " + x.GetProperty("currency").GetString())
            .ToArray();
        return balances.Length == 0 ? "0" : string.Join(" / ", balances);
    }

    public static async Task<decimal?> SonioxSessionCostAsync(string key, SessionRecord record,
        CancellationToken cancellation, HttpClient? client = null)
    {
        var start = record.StartedAt.AddMinutes(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var end = (record.EndedAt ?? DateTimeOffset.UtcNow).AddMinutes(2).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        string? cursor = null;
        decimal total = 0;
        var matched = false;
        for (var page = 0; page < 20; page++)
        {
            var url = "https://api.soniox.com/v1/usage-logs?start_time=" + Uri.EscapeDataString(start) +
                "&end_time=" + Uri.EscapeDataString(end) + "&limit=1000" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await (client ?? Http).SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Soniox kullanım kaydı HTTP {(int)response.StatusCode}");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            var root = document.RootElement;
            foreach (var log in root.GetProperty("usage_logs").EnumerateArray())
            {
                if (!log.TryGetProperty("client_reference_id", out var reference) ||
                    reference.GetString() != record.Id.ToString("N")) continue;
                total += decimal.Parse(log.GetProperty("cost_usd").GetString()!, CultureInfo.InvariantCulture);
                matched = true;
            }
            cursor = root.TryGetProperty("next_page_cursor", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString() : null;
            if (cursor is null) break;
        }
        return matched ? total : null;
    }
}
