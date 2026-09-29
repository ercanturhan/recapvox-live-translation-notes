using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Translator.Desktop;

public partial class MainWindow : Window
{
    private ILiveSession? _session;
    private CancellationTokenSource? _cancellation;
    private MicrophoneCapture? _microphone;
    private ApplicationAudioCapture? _applicationAudio;
    private Pcm16AudioMixer? _audioMixer;
    private WaveRecorder? _recorder;
    private OverlayWindow? _overlay;
    private IReadOnlyList<TranscriptSegment> _segments = [];
    private IReadOnlyList<TranscriptSegment> _sonioxSegments = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _hybridResults = new();
    private HybridTranslator? _hybrid;
    private readonly AppSettings _settings;
    private SessionRecord? _currentRecord;
    private HistoryWindow? _historyWindow;
    private readonly Stopwatch _recordClock = new();
    private readonly DispatcherTimer _recordTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _billingTimer = new() { Interval = TimeSpan.FromMinutes(2) };
    private bool _billingBusy;
    private volatile bool _paused;
    private bool _stopping;
    private IReadOnlyList<SessionRecord> _history = [];
    private bool _initializing = true;

    public MainWindow()
    {
        InitializeComponent();
        _recordTimer.Tick += (_, _) => RecordTimerText.Text = (_paused ? "⏸ " : "● ") + _recordClock.Elapsed.ToString(@"hh\:mm\:ss");
        _settings = ArchiveStore.LoadSettings();
        UiLocalizer.Language = _settings.UiLanguage;
        RefreshLanguages();
        TimeBox.IsChecked = _settings.ShowTimestamps;
        OverlayBox.IsChecked = _settings.ShowOverlay;
        IncludeMicrophoneBox.IsChecked = _settings.IncludeMicrophoneWithApplication;
        _initializing = false;
        RefreshApplications();
        ApplicationBox.SelectionChanged += (_, _) => UpdateSourceInfo();
        UpdateSourceInfo();
        RefreshProviderLabels();
        RefreshHistory();
        UiLocalizer.Apply(this);
        StatusText.Text = UiLocalizer.T("Hazır. Kaynağı seçip başlatabilirsiniz.");
        _billingTimer.Tick += async (_, _) => await RefreshBalancesAsync();
        _billingTimer.Start();
        _ = RefreshBalancesAsync();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };
        settingsWindow.LanguageChanged += ApplyLanguage;
        settingsWindow.ShowDialog();
        ApplyLanguage();
        _overlay?.ApplySettings(_settings);
        _overlay?.UpdateSegments(_segments);
    }

    private void ApplyLanguage()
    {
        UiLocalizer.Language = _settings.UiLanguage;
        UiLocalizer.Apply(this);
        StatusText.Text = UiLocalizer.TranslateCurrent(StatusText.Text);
        SummaryDateText.Text = UiLocalizer.TranslateCurrent(SummaryDateText.Text);
        RefreshProviderLabels();
        _ = RefreshBalancesAsync();
        RefreshLanguages();
        UpdateSourceInfo();
        RefreshHistory();
        _historyWindow?.ApplyLanguage();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        if (_historyWindow is null || !_historyWindow.IsVisible)
        {
            _historyWindow = new HistoryWindow(_settings) { Owner = this };
            _historyWindow.Closed += (_, _) => _historyWindow = null;
            _historyWindow.Show();
        }
        else { _historyWindow.Refresh(); _historyWindow.Activate(); }
    }

    private void RefreshProviderLabels()
    {
        LiveProviderText.Text = UiLocalizer.T("Canlı çeviri") + ": " + (_settings.LiveProvider == LiveProviders.Soniox ? "Soniox Real-Time Speech" : _settings.LiveProvider);
        SummaryProviderText.Text = UiLocalizer.T("Özetleyici") + ": " + _settings.SummaryProvider + " · " + _settings.SummaryModel;
    }

    private async void RefreshBalances_Click(object sender, RoutedEventArgs e) => await RefreshBalancesAsync();

    private async Task RefreshBalancesAsync()
    {
        if (_billingBusy) return;
        _billingBusy = true;
        var live = _settings.LiveProvider;
        var summary = _settings.SummaryProvider;
        LiveBalanceText.Text = UiLocalizer.T("Canlı bakiye") + ": " + UiLocalizer.T("Sorgulanıyor…");
        SummaryBalanceText.Text = UiLocalizer.T("Özet bakiyesi") + ": " + UiLocalizer.T("Sorgulanıyor…");
        try
        {
            async Task<string> ReadDeepSeekAsync(string? key)
            {
                if (string.IsNullOrWhiteSpace(key)) return UiLocalizer.T("Anahtar yok");
                try { return await BillingClient.DeepSeekBalanceAsync(key, CancellationToken.None); }
                catch { return UiLocalizer.T("Bakiye alınamadı"); }
            }
            var liveDeepSeekBalance = live == LiveProviders.Hybrid ? await ReadDeepSeekAsync(CredentialStore.ReadLiveDeepSeek()) : null;
            var summaryDeepSeekBalance = summary == "DeepSeek" ? await ReadDeepSeekAsync(CredentialStore.ReadForProvider("DeepSeek")) : null;
            if (live != _settings.LiveProvider || summary != _settings.SummaryProvider) return;
            LiveBalanceText.Text = UiLocalizer.T("Canlı bakiye") + ": " +
                (live == LiveProviders.Hybrid ? "Soniox: " + UiLocalizer.T("API'den alınamıyor") + " · DeepSeek: " + liveDeepSeekBalance
                : UiLocalizer.T("API'den alınamıyor"));
            SummaryBalanceText.Text = UiLocalizer.T("Özet bakiyesi") + ": " +
                (summary == "DeepSeek" ? summaryDeepSeekBalance : UiLocalizer.T("API'den alınamıyor"));
        }
        finally { _billingBusy = false; }
    }

    private void RefreshLanguages()
    {
        _initializing = true;
        SetLanguageOptions(SourceLanguageBox, LanguageCatalog.Live(_settings.LiveProvider, true), _settings.SourceLanguageCode, "");
        SetLanguageOptions(LanguageBox, LanguageCatalog.Live(_settings.LiveProvider, false), _settings.TargetLanguageCode, "tr", _settings.LiveProvider == LiveProviders.OpenAi);
        SetLanguageOptions(SummaryLanguageBox, LanguageCatalog.Summary(), _settings.SummaryLanguageCode, "tr", true);
        if (_settings.LiveProvider is LiveProviders.OpenAi or LiveProviders.Gemini)
        {
            SourceLanguageBox.SelectedItem = LanguageCatalog.Find(SourceLanguageBox.Items.OfType<LanguageOption>(), "");
            SourceLanguageBox.IsEnabled = false;
            SourceLanguageBox.ToolTip = UiLocalizer.T("Bu sağlayıcı konuşma dilini otomatik algılar; elle kaynak dili seçme ayarı yok.");
        }
        else
        {
            SourceLanguageBox.IsEnabled = true;
            SourceLanguageBox.ToolTip = UiLocalizer.T("Yazarak dil adı veya kodu arayın; otomatik algılama da seçilebilir.");
        }
        _initializing = false;
    }

    private static void SetLanguageOptions(ComboBox box, IReadOnlyList<LanguageOption> options, string code, string fallback, bool allowCustom = false)
    {
        var chosen = LanguageCatalog.Find(options, code);
        if (chosen is null && allowCustom && !string.IsNullOrWhiteSpace(code))
        {
            chosen = new LanguageOption(code, code);
            box.ItemsSource = new[] { chosen }.Concat(options).ToArray();
        }
        else box.ItemsSource = options;
        box.SelectedItem = chosen ?? LanguageCatalog.Find(options, fallback) ?? options.First();
        if (box.IsEditable) box.Text = box.SelectedItem?.ToString() ?? "";
    }

    private LanguageOption SelectedLanguage(ComboBox box, string field)
    {
        var option = LanguageCatalog.Resolve(box.Items.OfType<LanguageOption>(), box.Text, box.SelectedItem);
        if (option is null && box == SummaryLanguageBox && !string.IsNullOrWhiteSpace(box.Text) && box.Text.Trim().Length <= 80)
            option = new LanguageOption(box.Text.Trim(), box.Text.Trim());
        if (option is null && box == LanguageBox && _settings.LiveProvider == LiveProviders.OpenAi
            && System.Text.RegularExpressions.Regex.IsMatch(box.Text.Trim(), "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8}){0,2}$"))
            option = new LanguageOption(box.Text.Trim(), box.Text.Trim());
        if (option is null) throw new InvalidOperationException(field + " listesinden geçerli bir dil seçin.");
        if (!box.Items.OfType<LanguageOption>().Any(x => x.Code.Equals(option.Code, StringComparison.OrdinalIgnoreCase)))
            box.ItemsSource = new[] { option }.Concat(box.Items.OfType<LanguageOption>()).ToArray();
        box.SelectedItem = option;
        return option;
    }

    private void LanguageSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || sender is not ComboBox box || box.SelectedItem is not LanguageOption option) return;
        SaveLanguageChoice(box, option);
    }

    private void LanguagePicker_LostFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is not ComboBox box) return;
        LanguageOption? option;
        try { option = SelectedLanguage(box, "Dil"); }
        catch (InvalidOperationException) { return; }
        if (option is null) return;
        box.SelectedItem = option;
        SaveLanguageChoice(box, option);
    }

    private void SaveLanguageChoice(ComboBox box, LanguageOption option)
    {
        if (box == SourceLanguageBox) _settings.SourceLanguageCode = option.Code;
        else if (box == LanguageBox) _settings.TargetLanguageCode = option.Code;
        else if (box == SummaryLanguageBox) _settings.SummaryLanguageCode = option.Code;
        ArchiveStore.SaveSettings(_settings);
    }

    private async void Summarize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var provider = _settings.SummaryProvider;
            var key = CredentialStore.ReadForProvider(provider) ?? "";
            if (key.Length == 0) throw new InvalidOperationException(provider + " API anahtarını Ayarlar bölümüne girin.");
            if (!_segments.Any(x => !string.IsNullOrWhiteSpace(x.Original)))
                throw new InvalidOperationException(UiLocalizer.T("Önce bir konuşma oturumu oluşturun."));
            SummarizeButton.IsEnabled = false;
            StatusText.Text = provider + " " + UiLocalizer.T("ile özet hazırlanıyor…");
            var kind = SummaryKindBox.SelectedIndex == 1 ? "Toplantı" : "Video/eğitim";
            var language = SelectedLanguage(SummaryLanguageBox, "Özet dili").Name;
            var generation = await SummaryClient.SummarizeWithUsageAsync(provider, _settings.SummaryModel, key,
                kind, SummaryLengthBox.SelectedIndex == 1, language, _segments.ToArray(), CancellationToken.None);
            SummaryText.Text = generation.Text;
            var summary = new SummaryRecord { CreatedAt = DateTimeOffset.Now, Language = language, Provider = provider, Model = _settings.SummaryModel, Kind = kind, Text = SummaryText.Text, InputTokens = generation.InputTokens, OutputTokens = generation.OutputTokens };
            if (provider == "DeepSeek" && _settings.SummaryModel == "deepseek-flash")
            {
                summary.CostUsd = BillingClient.EstimateDeepSeekFlash(generation, summary.CreatedAt);
                summary.CostIsEstimate = summary.CostUsd is not null;
            }
            SummaryDateText.Text = $"{summary.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} · {language}";
            _currentRecord ??= CreateRecord();
            var storedRecord = ArchiveStore.LoadSessions(_settings.ArchiveDirectory).FirstOrDefault(x => x.Id == _currentRecord.Id);
            if (storedRecord is not null)
            {
                _currentRecord.Title = storedRecord.Title;
                _currentRecord.LiveCostUsd = storedRecord.LiveCostUsd;
                _currentRecord.HybridCostEstimateUsd = storedRecord.HybridCostEstimateUsd;
                _currentRecord.LiveCostStatus = storedRecord.LiveCostStatus;
            }
            _currentRecord.Segments = _segments.ToList();
            _currentRecord.Summaries.Add(summary);
            ArchiveStore.SaveSession(_settings.ArchiveDirectory, _currentRecord);
            RefreshHistory();
            _historyWindow?.Refresh();
            StatusText.Text = UiLocalizer.T("Özet, ilgili konuşma kaydına eklendi.");
            _ = RefreshBalancesAsync();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SummarizeButton.IsEnabled = true; }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not null) { await StopAsync(); return; }
        try
        {
            var liveProvider = _settings.LiveProvider;
            var credentialProvider = LiveProviders.CredentialProvider(liveProvider);
            var key = liveProvider is LiveProviders.Soniox or LiveProviders.Hybrid
                ? CredentialStore.Read() ?? ""
                : CredentialStore.ReadForProvider(credentialProvider) ?? "";
            if (key.Length == 0) throw new InvalidOperationException(liveProvider + " API anahtarını Ayarlar bölümüne girin.");
            var hybridKey = liveProvider == LiveProviders.Hybrid ? CredentialStore.ReadLiveDeepSeek() : null;
            if (liveProvider == LiveProviders.Hybrid && string.IsNullOrWhiteSpace(hybridKey))
                throw new InvalidOperationException(UiLocalizer.T("Hibrit canlı çeviri için DeepSeek anahtarını Ayarlar bölümüne girin."));
            string? wavPath = null;
            string? recordPath = null;
            int? selectedProcessId = null;
            if (SourceBox.SelectedIndex == 1)
            {
                selectedProcessId = (ApplicationBox.SelectedItem as ApplicationOption)?.ProcessId
                    ?? throw new InvalidOperationException(UiLocalizer.T("Önce sesini dinlemek istediğiniz uygulamayı seçin."));
                try { using var process = Process.GetProcessById(selectedProcessId.Value); }
                catch (ArgumentException) { throw new InvalidOperationException(UiLocalizer.T("Seçilen uygulama kapandı. Listeyi yenileyin.")); }
            }
            else if (SourceBox.SelectedIndex == 2)
            {
                var picker = new OpenFileDialog { Filter = "PCM WAV (*.wav)|*.wav" };
                if (picker.ShowDialog() != true) return;
                wavPath = picker.FileName;
                WaveFile.Validate(wavPath);
            }
            if (wavPath is null && RecordAudioBox.IsChecked == true)
            {
                Directory.CreateDirectory(_settings.ArchiveDirectory);
                recordPath = Path.Combine(_settings.ArchiveDirectory, $"translator-ses-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.wav");
            }
            _segments = [];
            _sonioxSegments = [];
            _hybridResults.Clear();
            _currentRecord = null;
            TranscriptList.ItemsSource = null;
            SummaryText.Text = "";
            SummaryDateText.Text = UiLocalizer.T("Henüz özet yok");
            SourceInfoText.Text = selectedProcessId is not null
                ? $"{UiLocalizer.T("Dinlenen kaynak:")} {(ApplicationBox.SelectedItem as ApplicationOption)?.ProcessName} (PID {selectedProcessId})" +
                  (IncludeMicrophoneBox.IsChecked == true ? " + " + UiLocalizer.T("mikrofon") + ". " : ". ") +
                  UiLocalizer.T("Bu uygulamanın tüm pencereleri ve alt süreçleri; tek sekme seçimi yok.")
                : wavPath is not null ? $"{UiLocalizer.T("Dinlenen kaynak:")} WAV — {Path.GetFileName(wavPath)}"
                : UiLocalizer.T("Dinlenen kaynak: Mikrofon");
            _cancellation = new CancellationTokenSource();
            if (liveProvider == LiveProviders.Hybrid)
            {
                var targetName = SelectedLanguage(LanguageBox, "Hedef dil").Name;
                _hybrid = new HybridTranslator(hybridKey!, targetName);
                _hybrid.Translated += (index, value) =>
                {
                    _hybridResults[index] = value;
                    Dispatcher.BeginInvoke(ApplySonioxSegments);
                };
                _hybrid.Failed += message => Dispatcher.BeginInvoke(() => StatusText.Text = message);
            }
            _session = LiveProviders.Create(liveProvider);
            _session.TextChanged += segments => Dispatcher.Invoke(() =>
            {
                _sonioxSegments = segments;
                _hybrid?.Observe(segments, _session?.CommittedOriginalCount ?? 0);
                ApplySonioxSegments();
            });
            _session.Failed += message => Dispatcher.Invoke(() => StatusText.Text = message);
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            SourceBox.IsEnabled = false;
            SourceLanguageBox.IsEnabled = false;
            LanguageBox.IsEnabled = false;
            ApplicationBox.IsEnabled = false;
            IncludeMicrophoneBox.IsEnabled = false;
            StatusText.Text = UiLocalizer.T("Bağlanıyor…");
            var language = SelectedLanguage(LanguageBox, "Hedef dil").Code;
            var sourceLanguage = SelectedLanguage(SourceLanguageBox, "Konuşma dili").Code;
            if (selectedProcessId is not null && IncludeMicrophoneBox.IsChecked == true)
                sourceLanguage = ""; // Each Teams participant may speak a different language.
            _currentRecord = CreateRecord();
            _currentRecord.AudioFile = recordPath;
            if (_session is SonioxSession soniox)
                await soniox.ConnectAsync(key, language, sourceLanguage, _cancellation.Token, _hybrid is null, _currentRecord.Id.ToString("N"));
            else await _session.ConnectAsync(key, language, sourceLanguage, _cancellation.Token);
            if (OverlayBox.IsChecked == true) ShowOverlay();
            if (wavPath is null && selectedProcessId is null)
            {
                if (recordPath is not null) _recorder = new WaveRecorder(recordPath);
                _microphone = new MicrophoneCapture();
                _microphone.AudioAvailable += data =>
                {
                    if (_paused) return;
                    _recorder?.Write(data);
                    _session.TryQueueAudio(data);
                };
                _microphone.Start();
                StatusText.Text = UiLocalizer.T("Mikrofon dinleniyor;") + " " + liveProvider + " " + UiLocalizer.T("bağlantısı açık.");
            }
            else if (selectedProcessId is not null)
            {
                if (recordPath is not null) _recorder = new WaveRecorder(recordPath);
                var includeMicrophone = IncludeMicrophoneBox.IsChecked == true;
                if (includeMicrophone)
                {
                    _audioMixer = new Pcm16AudioMixer();
                    _audioMixer.AudioAvailable += data =>
                    {
                        if (_paused) return;
                        _recorder?.Write(data);
                        _session?.TryQueueAudio(data);
                    };
                    _microphone = new MicrophoneCapture();
                    _microphone.AudioAvailable += data => { if (!_paused) _audioMixer?.AddMicrophone(data); };
                }
                _applicationAudio = new ApplicationAudioCapture();
                _applicationAudio.AudioAvailable += data =>
                {
                    if (_paused) return;
                    if (includeMicrophone) _audioMixer?.AddApplication(data);
                    else
                    {
                        _recorder?.Write(data);
                        _session?.TryQueueAudio(data);
                    }
                };
                _applicationAudio.Stopped += error => Dispatcher.Invoke(() =>
                    StatusText.Text = error is null ? UiLocalizer.T("Uygulama sesi durdu.") : UiLocalizer.T("Uygulama sesi hatası:") + " " + error.Message);
                await _applicationAudio.StartAsync(selectedProcessId.Value);
                if (includeMicrophone)
                {
                    _microphone!.Start();
                    _audioMixer!.Start();
                }
                StatusText.Text = includeMicrophone
                    ? UiLocalizer.T("Seçilen uygulama ve mikrofon birlikte dinleniyor; konuşma dili otomatik algılanıyor.")
                    : UiLocalizer.T("Seçilen uygulamanın sesi dinleniyor. Ses üretmiyorsa metin görünmez.");
            }
            else if (wavPath is not null)
            {
                StatusText.Text = UiLocalizer.T("WAV dosyası gerçek zaman hızında gönderiliyor.");
                _ = SendFileAsync(wavPath, _cancellation.Token);
            }
            _paused = false;
            _recordClock.Restart();
            _recordTimer.Start();
            RecordTimerText.Visibility = Visibility.Visible;
            RecordTimerText.Text = "● 00:00:00";
            StartButton.Content = UiLocalizer.T("Kaydı durdur");
            StartButton.IsEnabled = true;
            PauseButton.Content = UiLocalizer.T("⏸ Duraklat");
            PauseButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            await StopAsync(false);
        }
    }

    private async Task SendFileAsync(string path, CancellationToken cancellation)
    {
        try
        {
            await foreach (var data in WaveFile.ReadPacedAsync(path, cancellation))
            {
                while (_paused) await Task.Delay(100, cancellation);
                if (_session?.TryQueueAudio(data) != true) break;
            }
            if (!cancellation.IsCancellationRequested) await Dispatcher.InvokeAsync(() => StopAsync()).Task.Unwrap();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await Dispatcher.InvokeAsync(() => ShowError(ex)); }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        _paused = !_paused;
        _audioMixer?.SetPaused(_paused);
        if (_paused) _recordClock.Stop(); else _recordClock.Start();
        PauseButton.Content = UiLocalizer.T(_paused ? "▶ Devam et" : "⏸ Duraklat");
        RecordTimerText.Text = (_paused ? "⏸ " : "● ") + _recordClock.Elapsed.ToString(@"hh\:mm\:ss");
        StatusText.Text = UiLocalizer.T(_paused ? "Kayıt duraklatıldı." : "Kayıt devam ediyor.");
    }

    private async Task StopAsync(bool showSummary = true)
    {
        if (_stopping) return;
        _stopping = true;
        _overlay?.Close();
        StartButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        _recordClock.Stop();
        _recordTimer.Stop();
        _paused = true;
        _audioMixer?.SetPaused(true);
        _microphone?.Dispose();
        _microphone = null;
        if (_applicationAudio is not null)
        {
            await _applicationAudio.DisposeAsync();
            _applicationAudio = null;
        }
        _audioMixer?.Dispose();
        _audioMixer = null;
        _recorder?.Dispose();
        _recorder = null;
        _paused = false;
        _cancellation?.Cancel();
        if (_session is not null)
        {
            try { await _session.StopAsync(); }
            catch (Exception ex) { ShowError(ex); }
            _session = null;
        }
        if (_hybrid is not null)
        {
            await _hybrid.CompleteAsync(_sonioxSegments);
            if (_currentRecord is not null) _currentRecord.HybridCostEstimateUsd = _hybrid.EstimatedCostUsd;
            _hybrid = null;
            ApplySonioxSegments();
        }
        _cancellation?.Dispose();
        _cancellation = null;
        if (_currentRecord is not null)
        {
            try
            {
                _currentRecord.EndedAt ??= DateTimeOffset.Now;
                _currentRecord.Segments = _segments.ToList();
            if (_currentRecord.LiveProvider is LiveProviders.Soniox or LiveProviders.Hybrid)
                _currentRecord.LiveCostStatus = "Soniox maliyeti sorgulanıyor";
            else _currentRecord.LiveCostStatus = "Sağlayıcı maliyeti API'den alınamıyor";
                if (_segments.Count > 0 || _currentRecord.Summaries.Count > 0)
                {
                    ArchiveStore.SaveSession(_settings.ArchiveDirectory, _currentRecord);
                    RefreshHistory();
                    _historyWindow?.Refresh();
                if (_currentRecord.LiveProvider is LiveProviders.Soniox or LiveProviders.Hybrid)
                    _ = RefreshSessionCostAsync(_currentRecord.Id, _settings.ArchiveDirectory);
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }
        StartButton.IsEnabled = true;
        StartButton.Content = UiLocalizer.T("Kaydı başlat");
        PauseButton.Content = UiLocalizer.T("⏸ Duraklat");
        RecordTimerText.Visibility = Visibility.Collapsed;
        SettingsButton.IsEnabled = true;
        SourceBox.IsEnabled = true;
        SourceLanguageBox.IsEnabled = _settings.LiveProvider is LiveProviders.Soniox or LiveProviders.Hybrid;
        LanguageBox.IsEnabled = true;
        ApplicationBox.IsEnabled = true;
        IncludeMicrophoneBox.IsEnabled = true;
        StatusText.Text = UiLocalizer.T("Oturum bitti. Konuşma ve özetler Geçmiş bölümünde.");
        if (showSummary && _segments.Any(x => !string.IsNullOrWhiteSpace(x.Original))) MainTabs.SelectedIndex = 1;
        _stopping = false;
        _ = RefreshBalancesAsync();
    }

    private async Task RefreshSessionCostAsync(Guid id, string directory)
    {
        var key = CredentialStore.Read();
        if (string.IsNullOrWhiteSpace(key)) return;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(attempt * 3));
                var record = ArchiveStore.LoadSessions(directory).FirstOrDefault(x => x.Id == id);
                if (record is null) return;
                var cost = await BillingClient.SonioxSessionCostAsync(key, record, CancellationToken.None);
                if (cost is null) continue;
                record.LiveCostUsd = cost;
                record.LiveCostStatus = record.LiveProvider == LiveProviders.Hybrid
                    ? "Soniox kesin; DeepSeek maliyeti dahil değil" : "Soniox kesin tutar";
                ArchiveStore.SaveSession(directory, record);
                _historyWindow?.Refresh();
                return;
            }
            catch (Exception)
            {
                if (attempt == 2)
                {
                    var record = ArchiveStore.LoadSessions(directory).FirstOrDefault(x => x.Id == id);
                    if (record is null) return;
                    record.LiveCostStatus = "Soniox kullanım verisi alınamadı";
                    ArchiveStore.SaveSession(directory, record);
                    _historyWindow?.Refresh();
                    return;
                }
            }
        }
        var pending = ArchiveStore.LoadSessions(directory).FirstOrDefault(x => x.Id == id);
        if (pending is not null && pending.LiveCostUsd is null)
        {
            pending.LiveCostStatus = "Soniox kullanım kaydı henüz yok; sonra yenileyin";
            ArchiveStore.SaveSession(directory, pending);
            _historyWindow?.Refresh();
        }
    }

    private void ShowOverlay()
    {
        if (_overlay is null || !_overlay.IsVisible)
        {
            _overlay = new OverlayWindow(_settings);
            _overlay.Closed += (_, _) => _overlay = null;
            _overlay.Show();
            _overlay.UpdateSegments(_segments);
        }
    }

    private void Overlay_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _initializing) return;
        _settings.ShowOverlay = OverlayBox.IsChecked == true;
        ArchiveStore.SaveSettings(_settings);
        if (_settings.ShowOverlay && _session is not null) ShowOverlay();
        else _overlay?.Close();
    }

    private void OverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        if (new OverlaySettingsWindow(_settings) { Owner = this }.ShowDialog() != true) return;
        _overlay?.ApplySettings(_settings);
        _overlay?.UpdateSegments(_segments);
    }

    private void Time_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _initializing) return;
        _settings.ShowTimestamps = TimeBox.IsChecked == true;
        ArchiveStore.SaveSettings(_settings);
        RefreshTranscript();
    }

    private void RefreshTranscript()
    {
        TranscriptList.ItemsSource = _segments.Select(x => new TranscriptRow(
            _settings.ShowTimestamps && x.Start is not null ? $"{(int)x.Start.Value.TotalMinutes:00}:{x.Start.Value.Seconds:00}" : "",
            x.Original, x.Translation, _settings.ShowTimestamps ? new GridLength(72) : new GridLength(0))).ToArray();
    }

    private void ApplySonioxSegments()
    {
        _segments = _hybridResults.Count == 0 ? _sonioxSegments : _sonioxSegments.Select((x, i) =>
            _hybridResults.TryGetValue(i, out var result) ? x with { Translation = result } : x).ToArray();
        RefreshTranscript();
        _overlay?.UpdateSegments(_segments);
    }

    private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordAudioBox is null) return;
        RecordAudioBox.IsEnabled = SourceBox.SelectedIndex != 2;
        if (!RecordAudioBox.IsEnabled) RecordAudioBox.IsChecked = false;
        ApplicationPicker.Visibility = SourceBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSourceInfo();
    }

    private void IncludeMicrophone_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _initializing) return;
        _settings.IncludeMicrophoneWithApplication = IncludeMicrophoneBox.IsChecked == true;
        ArchiveStore.SaveSettings(_settings);
        UpdateSourceInfo();
    }

    private void RefreshApplications_Click(object sender, RoutedEventArgs e) => RefreshApplications();

    private void RefreshApplications()
    {
        var options = new List<ApplicationOption>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                    if (process.Id == Environment.ProcessId) continue;
                    options.Add(new ApplicationOption(process.Id, process.ProcessName, process.MainWindowTitle));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        var previousId = (ApplicationBox.SelectedItem as ApplicationOption)?.ProcessId;
        ApplicationBox.ItemsSource = options.OrderBy(x => x.ProcessName).ThenBy(x => x.WindowTitle).ToArray();
        if (ApplicationBox.Items.Count > 0)
            ApplicationBox.SelectedItem = options.FirstOrDefault(x => x.ProcessId == previousId) ?? ApplicationBox.Items[0];
        UpdateSourceInfo();
    }

    private sealed record ApplicationOption(int ProcessId, string ProcessName, string WindowTitle)
    {
        public string DisplayName => $"{ProcessName} — {WindowTitle} (PID {ProcessId})";
    }

    private sealed record TranscriptRow(string Timestamp, string Original, string Translation, GridLength TimeWidth);

    private SessionRecord CreateRecord() => new()
    {
        StartedAt = DateTimeOffset.Now,
        Source = SourceInfoText.Text,
        SourceLanguage = SourceBox.SelectedIndex == 1 && IncludeMicrophoneBox.IsChecked == true
            ? "Otomatik algıla (uygulama + mikrofon)"
            : (SourceLanguageBox.SelectedItem as LanguageOption)?.Name ?? "",
        TargetLanguage = (LanguageBox.SelectedItem as LanguageOption)?.Name ?? ""
        ,LiveProvider = _settings.LiveProvider
    };

    private void RefreshHistory()
    {
        _history = ArchiveStore.LoadSessions(_settings.ArchiveDirectory);
        FilterHistory();
    }

    private void HistorySearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (_history is not null) FilterHistory();
    }

    private void FilterHistory()
    {
        if (HistoryList is null || HistorySearchBox is null) return;
        var term = HistorySearchBox.Text.Trim();
        var selectedId = (HistoryList.SelectedItem as SessionRecord)?.Id;
        var rows = _history.Where(x => term.Length == 0 || x.DisplayName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || x.Segments.Any(s => s.Original.Contains(term, StringComparison.CurrentCultureIgnoreCase) || s.Translation.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            || x.Summaries.Any(s => s.Text.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        HistoryList.ItemsSource = rows;
        HistoryList.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId) ?? rows.FirstOrDefault();
        ShowSelectedHistory();
    }

    private void HistorySelection_Changed(object sender, SelectionChangedEventArgs e) => ShowSelectedHistory();
    private void RecordSearch_Changed(object sender, TextChangedEventArgs e) => ShowSelectedHistory();

    private void ShowSelectedHistory()
    {
        if (HistoryDetailText is null || RecordSearchBox is null || HistoryList is null) return;
        if (HistoryList.SelectedItem is not SessionRecord record) { HistoryDetailText.Text = "Kayıt seçin."; return; }
        var term = RecordSearchBox.Text.Trim();
        var lines = new List<string> { $"{record.StartedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} – {record.EndedAt?.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss") ?? "Devam ediyor"}", record.Source, $"{record.SourceLanguage} → {record.TargetLanguage}", "" };
        foreach (var segment in record.Segments.Where(x => term.Length == 0 || x.Original.Contains(term, StringComparison.CurrentCultureIgnoreCase) || x.Translation.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
        {
            lines.Add($"[{(segment.Start is null ? "--:--" : $"{(int)segment.Start.Value.TotalMinutes:00}:{segment.Start.Value.Seconds:00}")}] {segment.Original}");
            lines.Add(segment.Translation);
            lines.Add("");
        }
        foreach (var summary in record.Summaries.Where(x => term.Length == 0 || x.Text.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
        {
            lines.Add($"ÖZET · {summary.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} · {summary.Language} · {summary.Provider}");
            lines.Add(summary.Text);
            lines.Add("");
        }
        HistoryDetailText.Text = string.Join("\n", lines);
    }

    private void UpdateSourceInfo()
    {
        if (SourceInfoText is null || SourceBox is null) return;
        if (SourceBox.SelectedIndex == 1)
        {
            var app = ApplicationBox?.SelectedItem as ApplicationOption;
            SourceInfoText.Text = app is null ? UiLocalizer.T("Dinlenen kaynak: Uygulama seçilmedi.")
                : $"{UiLocalizer.T("Dinlenecek uygulama:")} {app.ProcessName} (PID {app.ProcessId})" +
                  (IncludeMicrophoneBox?.IsChecked == true ? " + " + UiLocalizer.T("mikrofon; konuşma dili otomatik algılanır;") + " " : "; ") +
                  UiLocalizer.T("tüm pencereleri ve alt süreçleri. Pencere başlığı sadece seçim içindir.");
        }
        else SourceInfoText.Text = SourceBox.SelectedIndex == 2 ? UiLocalizer.T("Dinlenecek kaynak: Seçilecek WAV dosyası") : UiLocalizer.T("Dinlenecek kaynak: Mikrofon");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_segments.Count == 0 && string.IsNullOrWhiteSpace(SummaryText.Text))
                throw new InvalidOperationException(UiLocalizer.T("Dışa aktarılacak konuşma veya özet yok."));
            var picker = new SaveFileDialog
            {
                Title = UiLocalizer.T("Dışa aktarma biçimini ve konumunu seçin"),
                Filter = UiLocalizer.T("PDF belgesi") + " (*.pdf)|*.pdf|" + UiLocalizer.T("Düz metin") + " (*.txt)|*.txt|Markdown (*.md)|*.md",
                FilterIndex = 1, AddExtension = true, FileName = $"translator-{DateTime.Now:yyyyMMdd-HHmm}"
            };
            if (picker.ShowDialog() != true) return;
            var markdown = new StringBuilder($"# RecapVox oturumu\n\nTarih: {DateTime.Now:yyyy-MM-dd HH:mm}\n\nKaynak: {SourceInfoText.Text}\n\nÇeviri eşleşmesi konuşma sırasına göredir; cümleler bire bir hizalı olmayabilir.\n");
            var plain = new StringBuilder($"RecapVox oturumu\nTarih: {DateTime.Now:yyyy-MM-dd HH:mm}\nKaynak: {SourceInfoText.Text}\n\n");
            foreach (var segment in _segments)
            {
                var time = segment.Start is null ? "--:--" : $"{(int)segment.Start.Value.TotalMinutes:00}:{segment.Start.Value.Seconds:00}";
                markdown.Append($"\n### {time}\n\n**Orijinal:** {segment.Original}\n\n**Çeviri:** {segment.Translation}\n");
                plain.Append($"[{time}] Orijinal: {segment.Original}\nÇeviri: {segment.Translation}\n\n");
            }
            if (!string.IsNullOrWhiteSpace(SummaryText.Text))
            {
                markdown.Append("\n## AI özeti\n\n").Append(SummaryText.Text).Append('\n');
                plain.Append("ÖZET\n").Append(SummaryText.Text).Append('\n');
            }
            if (picker.FilterIndex == 1) PdfExporter.Save(picker.FileName, plain.ToString());
            else File.WriteAllText(picker.FileName, picker.FilterIndex == 2 ? plain.ToString() : markdown.ToString(), new UTF8Encoding(false));
            StatusText.Text = UiLocalizer.T("Oturum") + " " + (picker.FilterIndex == 1 ? "PDF" : picker.FilterIndex == 2 ? "TXT" : "MD") + " " + UiLocalizer.T("olarak dışa aktarıldı.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception ex)
    {
        StatusText.Text = ex.Message;
        MessageBox.Show(this, ex.Message, "RecapVox", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override async void OnClosed(EventArgs e)
    {
        await StopAsync(false);
        base.OnClosed(e);
    }
}
