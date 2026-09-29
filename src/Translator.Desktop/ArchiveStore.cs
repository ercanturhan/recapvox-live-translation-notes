using System.IO;
using System.Text.Json;

namespace Translator.Desktop;

internal sealed class AppSettings
{
    public string ArchiveDirectory { get; set; } = Path.Combine(ArchiveStore.DataRoot, "records");
    public string SummaryProvider { get; set; } = "DeepSeek";
    public string SummaryModel { get; set; } = SummaryClient.DefaultModel("DeepSeek");
    public string LiveProvider { get; set; } = "Soniox";
    public string SourceLanguageCode { get; set; } = "en";
    public string TargetLanguageCode { get; set; } = "tr";
    public string SummaryLanguageCode { get; set; } = "tr";
    public bool ShowTimestamps { get; set; } = true;
    public bool HistoryShowTimestamps { get; set; } = true;
    public bool ShowOverlay { get; set; } = true;
    public bool IncludeMicrophoneWithApplication { get; set; }
    public string OverlayContent { get; set; } = "İkisi";
    public string OverlayLayout { get; set; } = "Alt alta";
    public int OverlayWidth { get; set; } = 760;
    public int OverlayFontSize { get; set; } = 21;
    public string UiLanguage { get; set; } = "tr";
}

internal sealed class SummaryRecord
{
    public DateTimeOffset CreatedAt { get; set; }
    public string Language { get; set; } = "Türkçe";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public decimal? CostUsd { get; set; }
    public bool CostIsEstimate { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

internal sealed class SessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Source { get; set; } = "";
    public string SourceLanguage { get; set; } = "";
    public string TargetLanguage { get; set; } = "";
    public string LiveProvider { get; set; } = "Soniox";
    public string? AudioFile { get; set; }
    public List<TranscriptSegment> Segments { get; set; } = [];
    public List<SummaryRecord> Summaries { get; set; } = [];
    public decimal? LiveCostUsd { get; set; }
    public decimal? HybridCostEstimateUsd { get; set; }
    public string LiveCostStatus { get; set; } = "";
    public string DisplayName => $"{StartedAt.LocalDateTime:dd.MM.yyyy HH:mm} · " +
        (string.IsNullOrWhiteSpace(Title) ? Source : Title) +
        $" · {Segments.Count} {UiLocalizer.T("blok")}" + (Summaries.Count > 0 ? " · 📝 " + UiLocalizer.T("Özet var") : "") +
        (LiveCostUsd is { } cost ? $" · ${cost:0.0000}" : "");
}

internal static class ArchiveStore
{
    private static readonly object SessionWriteLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Codex", "translator");
    private static string SettingsPath => Path.Combine(DataRoot, ".local", "settings.json");

    public static AppSettings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (JsonException) { return new AppSettings(); }
    }

    public static void SaveSettings(AppSettings settings)
    {
        WriteAtomic(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static void SaveSession(string directory, SessionRecord record)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Kayıt klasörü seçin.");
        var path = Path.Combine(directory, record.Id.ToString("N") + ".json");
        lock (SessionWriteLock)
        {
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(path), JsonOptions);
                if (saved is not null)
                {
                    record.LiveCostUsd ??= saved.LiveCostUsd;
                    record.HybridCostEstimateUsd ??= saved.HybridCostEstimateUsd;
                    if (record.LiveCostUsd is not null && saved.LiveCostUsd is not null &&
                        record.LiveCostStatus == "Soniox maliyeti sorgulanıyor")
                        record.LiveCostStatus = saved.LiveCostStatus;
                }
            }
            WriteAtomic(path, JsonSerializer.Serialize(record, JsonOptions));
        }
    }

    public static SessionRecord CopySession(string directory, SessionRecord source)
    {
        var copy = JsonSerializer.Deserialize<SessionRecord>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
        copy.Id = Guid.NewGuid();
        copy.Title = (string.IsNullOrWhiteSpace(copy.Title) ? copy.Source : copy.Title) + " (" + UiLocalizer.T("Kopya") + ")";
        SaveSession(directory, copy);
        return copy;
    }

    public static void DeleteSession(string directory, Guid id)
    {
        File.Delete(Path.Combine(directory, id.ToString("N") + ".json"));
    }

    public static IReadOnlyList<SessionRecord> LoadSessions(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        var records = new List<SessionRecord>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var record = JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(file), JsonOptions);
                if (record is not null) records.Add(record);
            }
            catch (IOException) { }
            catch (JsonException) { }
        }
        return records.OrderByDescending(x => x.StartedAt).ToArray();
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }
}
