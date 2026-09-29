using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;

namespace Translator.Desktop;

public partial class HistoryWindow : Window
{
    private readonly AppSettings _settings;
    private IReadOnlyList<SessionRecord> _records = [];
    private SessionRecord? _copiedRecord;

    internal HistoryWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ShowTimeBox.IsChecked = settings.HistoryShowTimestamps;
        ApplyLanguage();
        Refresh();
    }

    internal void ApplyLanguage()
    {
        UiLocalizer.Apply(this);
        Refresh();
    }

    internal void Refresh()
    {
        var id = (RecordList.SelectedItem as SessionRecord)?.Id;
        _records = ArchiveStore.LoadSessions(_settings.ArchiveDirectory);
        Filter(id);
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (RecordList is not null) Filter((RecordList.SelectedItem as SessionRecord)?.Id);
    }

    private void Filter(Guid? selectedId)
    {
        var term = SearchBox.Text.Trim();
        var matches = _records.Where(x => term.Length == 0 || x.DisplayName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || x.Segments.Any(s => s.Original.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || s.Translation.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            || x.Summaries.Any(s => s.Text.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        RecordList.ItemsSource = matches;
        RecordList.SelectedItem = matches.FirstOrDefault(x => x.Id == selectedId) ?? matches.FirstOrDefault();
        ShowRecord();
    }

    private void Record_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowRecord();
    private void WithinSearch_Changed(object sender, TextChangedEventArgs e) => ShowRecord();
    private void ShowTime_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        _settings.HistoryShowTimestamps = ShowTimeBox.IsChecked == true;
        ArchiveStore.SaveSettings(_settings);
        ShowRecord();
    }

    private void ShowRecord()
    {
        if (RecordList is null || DetailBox is null || OpenSummaryButton is null) return;
        if (RecordList.SelectedItem is not SessionRecord record)
        {
            DetailBox.Text = UiLocalizer.T("Kayıt seçin."); OpenSummaryButton.IsEnabled = false; return;
        }
        OpenSummaryButton.IsEnabled = record.Summaries.Count > 0;
        var term = WithinSearchBox?.Text.Trim() ?? "";
        var lines = new List<string>
        {
            $"{record.StartedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} – {record.EndedAt?.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss") ?? UiLocalizer.T("Devam ediyor")}",
            record.Source, record.TranscriptionOnly ? UiLocalizer.T("Transkripsiyon") + " · " + record.SourceLanguage : $"{record.SourceLanguage} → {record.TargetLanguage}",
            UiLocalizer.T(record.TranscriptionOnly ? "Transkripsiyon maliyeti" : "Canlı çeviri maliyeti") + ": " + (record.LiveCostUsd is { } liveCost
                ? $"${liveCost:0.000000} USD · {UiLocalizer.T(record.LiveCostStatus)}"
                : string.IsNullOrWhiteSpace(record.LiveCostStatus) ? UiLocalizer.T("Bilinmiyor") : UiLocalizer.T(record.LiveCostStatus)),
            record.LiveProvider == LiveProviders.Hybrid ? "DeepSeek: " + (record.HybridCostEstimateUsd is { } hybridCost
                ? $"≈ ${hybridCost:0.000000} USD · " + UiLocalizer.T("Tahmini") : UiLocalizer.T("Maliyet bilinmiyor")) : "",
            UiLocalizer.T("Özetleyici kullanımı") + ": " + (record.Summaries.Count == 0 ? UiLocalizer.T("Özet yok")
                : string.Join("; ", record.Summaries.Select(s => s.Provider + " · " +
                    (s.CostUsd is { } summaryCost ? $"≈ ${summaryCost:0.000000} USD · " + UiLocalizer.T("Tahmini") :
                    s.InputTokens is { } input && s.OutputTokens is { } output
                        ? $"{input}+{output} token · " + UiLocalizer.T("Maliyet sağlayıcıdan alınamıyor")
                        : UiLocalizer.T("Maliyet bilinmiyor"))))), ""
        };
        foreach (var segment in record.Segments.Where(x => term.Length == 0 || x.Original.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || x.Translation.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
        {
            var time = segment.Start is null ? "--:--" : $"{(int)segment.Start.Value.TotalMinutes:00}:{segment.Start.Value.Seconds:00}";
            lines.Add((ShowTimeBox?.IsChecked == true ? $"[{time}] " : "") + segment.Original);
            if (!record.TranscriptionOnly) lines.Add(segment.Translation);
            lines.Add("");
        }
        DetailBox.Text = string.Join("\n", lines);
    }

    private void Record_RightClick(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        while (hit is not null && hit is not ListBoxItem) hit = VisualTreeHelper.GetParent(hit);
        if (hit is ListBoxItem item) RecordList.SelectedItem = item.DataContext;
    }

    private void RecordMenu_Opening(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var hasRecord = RecordList.SelectedItem is SessionRecord;
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = UiLocalizer.T(item.Tag?.ToString() switch { "rename" => "Yeniden adlandır", "copy" => "Kopyala", "paste" => "Yapıştır", "export" => "Dışa aktar", "cost" => "Maliyeti yenile", "delete" => "Sil", _ => "" });
            item.IsEnabled = item.Tag?.ToString() == "paste" ? _copiedRecord is not null :
                item.Tag?.ToString() == "cost" ? (RecordList.SelectedItem as SessionRecord)?.LiveProvider is LiveProviders.Soniox or LiveProviders.Hybrid : hasRecord;
        }
        var export = menu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Tag?.ToString() == "export");
        if (export is not null) foreach (var child in export.Items.OfType<MenuItem>())
            child.Header = UiLocalizer.T(child.Tag?.ToString() switch { "record" => "Kaydı", "summary" => "Özeti", _ => "Kayıt ve özeti" });
        var hasSummary = (RecordList.SelectedItem as SessionRecord)?.Summaries.Count > 0;
        if (export?.Items[1] is MenuItem summary) summary.IsEnabled = hasSummary;
        if (export?.Items[2] is MenuItem both) both.IsEnabled = hasSummary;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (RecordList.SelectedItem is not SessionRecord record) return;
        var dialog = new Window { Title = UiLocalizer.T("Yeniden adlandır"), Owner = this, Width = 440, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(18) };
        var title = new TextBox { Text = string.IsNullOrWhiteSpace(record.Title) ? record.Source : record.Title,
            Height = 32, VerticalContentAlignment = VerticalAlignment.Center, MaxLength = 120 };
        var save = new Button { Content = UiLocalizer.T("Kaydet"), Width = 95, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(title); panel.Children.Add(save); dialog.Content = panel;
        dialog.Loaded += (_, _) => { title.Focus(); title.SelectAll(); };
        if (dialog.ShowDialog() != true) return;
        record.Title = title.Text.Trim();
        ArchiveStore.SaveSession(_settings.ArchiveDirectory, record);
        Refresh();
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => _copiedRecord = RecordList.SelectedItem as SessionRecord;

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (_copiedRecord is null) return;
        var copy = ArchiveStore.CopySession(_settings.ArchiveDirectory, _copiedRecord);
        Refresh();
        RecordList.SelectedItem = RecordList.Items.OfType<SessionRecord>().FirstOrDefault(x => x.Id == copy.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RecordList.SelectedItem is not SessionRecord record) return;
        if (MessageBox.Show(this, UiLocalizer.T("Seçili kaydı silmek istiyor musunuz?"), UiLocalizer.T("Sil"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        ArchiveStore.DeleteSession(_settings.ArchiveDirectory, record.Id);
        Refresh();
    }

    private async void RefreshCost_Click(object sender, RoutedEventArgs e)
    {
        if (RecordList.SelectedItem is not SessionRecord selected) return;
        var key = CredentialStore.Read();
        if (string.IsNullOrWhiteSpace(key)) { MessageBox.Show(this, UiLocalizer.T("Soniox anahtarı yok")); return; }
        try
        {
            var cost = await BillingClient.SonioxSessionCostAsync(key, selected, CancellationToken.None);
            var record = ArchiveStore.LoadSessions(_settings.ArchiveDirectory).FirstOrDefault(x => x.Id == selected.Id);
            if (record is null) return;
            if (cost is not null) record.LiveCostUsd = cost;
            record.LiveCostStatus = cost is null ?
                record.LiveCostUsd is null ? "Soniox kullanım kaydı henüz yok" : record.LiveCostStatus :
                record.LiveProvider == LiveProviders.Hybrid ? "Soniox kesin; DeepSeek maliyeti dahil değil" : "Soniox kesin tutar";
            ArchiveStore.SaveSession(_settings.ArchiveDirectory, record);
            Refresh();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, UiLocalizer.T("Maliyeti yenile")); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (RecordList.SelectedItem is not SessionRecord record || sender is not MenuItem item) return;
        var mode = item.Tag?.ToString();
        var picker = new SaveFileDialog { Title = UiLocalizer.T("Dışa aktarma biçimini ve konumunu seçin"),
            Filter = "PDF (*.pdf)|*.pdf|TXT (*.txt)|*.txt|Markdown (*.md)|*.md", AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(record.Title) ? $"translator-{record.StartedAt:yyyyMMdd-HHmm}" : string.Join("_", record.Title.Split(Path.GetInvalidFileNameChars())) };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var markdown = new StringBuilder($"# {(string.IsNullOrWhiteSpace(record.Title) ? "RecapVox" : record.Title)}\n\n{record.StartedAt.LocalDateTime:dd.MM.yyyy HH:mm}\n\n");
            var plain = new StringBuilder($"{record.DisplayName}\n\n");
            if (mode is "record" or "both")
                foreach (var segment in record.Segments)
                {
                    var stamp = segment.Start is null ? "--:--" : $"{(int)segment.Start.Value.TotalMinutes:00}:{segment.Start.Value.Seconds:00}";
                    markdown.Append($"### {stamp}\n\n**{UiLocalizer.T("Orijinal")}:** {segment.Original}\n\n");
                    plain.Append($"[{stamp}] {segment.Original}\n");
                    if (!record.TranscriptionOnly)
                    {
                        markdown.Append($"**{UiLocalizer.T("Çeviri")}:** {segment.Translation}\n\n");
                        plain.Append(segment.Translation + "\n");
                    }
                    plain.Append('\n');
                }
            if (mode is "summary" or "both")
                foreach (var summary in record.Summaries)
                {
                    markdown.Append($"## {UiLocalizer.T("Özet")} · {summary.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm}\n\n{summary.Text}\n\n");
                    plain.Append($"{UiLocalizer.T("Özet")} · {summary.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm}\n{summary.Text}\n\n");
                }
            if (picker.FilterIndex == 1) PdfExporter.Save(picker.FileName, plain.ToString());
            else File.WriteAllText(picker.FileName, picker.FilterIndex == 2 ? plain.ToString() : markdown.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, UiLocalizer.T("Dışa aktar")); }
    }

    private void OpenSummary_Click(object sender, RoutedEventArgs e)
    {
        if (RecordList.SelectedItem is not SessionRecord record || record.Summaries.Count == 0) return;
        new SummaryHistoryWindow(record) { Owner = this }.ShowDialog();
    }
}
