using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Translator.Desktop;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _languageReady;
    internal event Action? LanguageChanged;
    internal SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        UiLanguageBox.SelectedIndex = settings.UiLanguage switch { "en" => 1, "de" => 2, "es" => 3, _ => 0 };
        LiveProviderBox.SelectedIndex = settings.LiveProvider switch
        {
            LiveProviders.Hybrid => 1,
            LiveProviders.OpenAi => 2,
            LiveProviders.Gemini => 3,
            _ => 0
        };
        ProviderBox.SelectedIndex = Math.Max(0, new[] { "DeepSeek", "OpenAI", "Gemini", "Claude" }.ToList().IndexOf(settings.SummaryProvider));
        ModelBox.Text = settings.SummaryModel;
        ArchivePathBox.Text = settings.ArchiveDirectory;
        UiLocalizer.Language = settings.UiLanguage;
        UiLocalizer.Apply(this);
        _languageReady = true;
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => UpdateApplyButton()));
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdateApplyButton()));
        AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler((_, _) => UpdateApplyButton()));
        AddHandler(RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((_, _) => UpdateApplyButton()));
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => Dispatcher.BeginInvoke(UpdateApplyButton)));
        UpdateApplyButton();
    }

    private void UpdateApplyButton()
    {
        if (!_languageReady) return;
        var changed = HasUnsavedPreferences();
        ApplyButton.IsEnabled = changed;
        ApplyButton.Background = new SolidColorBrush(changed ? Color.FromRgb(24, 117, 194) : Color.FromRgb(213, 222, 232));
        ApplyButton.Foreground = new SolidColorBrush(changed ? Colors.White : Color.FromRgb(107, 123, 141));
        ApplyButton.Effect = changed ? new DropShadowEffect { Color = Color.FromRgb(29, 123, 215), BlurRadius = 19, ShadowDepth = 0, Opacity = 0.75 } : null;
    }

    private void UiLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageReady) return;
        var language = (UiLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tr";
        FeedbackText.Text = _settings.UiLanguage == language ? "" : UiLocalizer.T("Değişiklikler kaydedilmedi. Kaydet ve uygula düğmesine basın.");
        UpdateApplyButton();
    }

    private string Provider => (ProviderBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "DeepSeek";
    private string LiveProvider => (LiveProviderBox.SelectedItem as ComboBoxItem) is { } item
        ? item.Tag?.ToString() ?? item.Content?.ToString() ?? LiveProviders.Soniox : LiveProviders.Soniox;
    private void LiveProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LiveKeyBox is null || LiveKeyLabel is null || LiveHintText is null || HybridPanel is null) return;
        var keyProvider = LiveProviders.CredentialProvider(LiveProvider);
        SingleLivePanel.Visibility = LiveProvider == LiveProviders.Hybrid ? Visibility.Collapsed : Visibility.Visible;
        HybridPanel.Visibility = LiveProvider == LiveProviders.Hybrid ? Visibility.Visible : Visibility.Collapsed;
        LiveKeyBox.Password = keyProvider == LiveProviders.Soniox ? CredentialStore.Read() ?? "" :
            LiveProvider == LiveProviders.Hybrid ? "" : CredentialStore.ReadForProvider(keyProvider) ?? "";
        HybridSonioxKeyBox.Password = LiveProvider == LiveProviders.Hybrid ? CredentialStore.Read() ?? "" : "";
        HybridDeepSeekKeyBox.Password = LiveProvider == LiveProviders.Hybrid ? CredentialStore.ReadLiveDeepSeek() ?? "" : "";
        LiveKeyLabel.Text = keyProvider + " " + UiLocalizer.T("API anahtarı");
        LiveHintText.Text = UiLocalizer.T(LiveProvider switch
        {
            LiveProviders.Hybrid => "Her anahtarı kendi bölümünden kaydedip deneyin. Canlı DeepSeek anahtarı özetleyiciden bağımsızdır.",
            LiveProviders.OpenAi => "OpenAI gpt-realtime-translate: ses ve iki dilde metin üretir. 24 kHz ses akışı kullanılır.",
            LiveProviders.Gemini => "Gemini 3.5 Live Translate: ses ve iki dilde metin üretir. 16 kHz ses akışı kullanılır.",
            _ => "Soniox konuşma ve çeviriyi tek bağlantıda üretir."
        });
    }
    private void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelBox is null || SummaryKeyBox is null) return;
        ModelBox.Text = Provider == _settings.SummaryProvider ? _settings.SummaryModel : SummaryClient.DefaultModel(Provider);
        SummaryKeyBox.Password = CredentialStore.ReadForProvider(Provider) ?? "";
    }
    private void SaveLiveKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var provider = LiveProviders.CredentialProvider(LiveProvider);
            var key = Required(LiveKeyBox.Password, provider + " anahtarı");
            if (provider == LiveProviders.Soniox) CredentialStore.Write(key);
            else CredentialStore.WriteForProvider(provider, key);
            _settings.LiveProvider = LiveProvider;
            ArchiveStore.SaveSettings(_settings);
            FeedbackText.Text = LiveProvider + " / " + provider + ": " + UiLocalizer.T("anahtar kaydedildi.");
        }
        catch (Exception ex) { Error(ex); }
    }
    private void SaveHybridSoniox_Click(object sender, RoutedEventArgs e)
    {
        try { CredentialStore.Write(Required(HybridSonioxKeyBox.Password, "Soniox anahtarı")); SaveLiveSelection(); FeedbackText.Text = UiLocalizer.T("Soniox anahtarı kaydedildi."); }
        catch (Exception ex) { Error(ex); }
    }
    private void SaveHybridDeepSeek_Click(object sender, RoutedEventArgs e)
    {
        try { CredentialStore.WriteLiveDeepSeek(Required(HybridDeepSeekKeyBox.Password, "DeepSeek anahtarı")); SaveLiveSelection(); FeedbackText.Text = UiLocalizer.T("Canlı DeepSeek anahtarı kaydedildi."); }
        catch (Exception ex) { Error(ex); }
    }
    private void SaveLiveSelection()
    {
        _settings.LiveProvider = LiveProvider;
        ArchiveStore.SaveSettings(_settings);
    }
    private void SaveSummary_Click(object sender, RoutedEventArgs e)
    {
        try { CredentialStore.WriteForProvider(Provider, Required(SummaryKeyBox.Password, Provider + " anahtarı")); SavePreferences(); FeedbackText.Text = Provider + ": " + UiLocalizer.T("anahtar ve model kaydedildi."); }
        catch (Exception ex) { Error(ex); }
    }
    private async void TestLive_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LiveTestButton.IsEnabled = false;
            var selected = LiveProvider;
            var provider = LiveProviders.CredentialProvider(selected);
            var key = Required(LiveKeyBox.Password, provider + " anahtarı");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var session = LiveProviders.Create(selected);
            try { await session.ConnectAsync(key, "tr", "en", cancel.Token); }
            finally { await session.StopAsync(); }
            FeedbackText.Text = selected + ": " + UiLocalizer.T("bağlantı başarılı; deneme kullanıma yazılabilir.");
        }
        catch (Exception ex) { Error(ex); }
        finally { LiveTestButton.IsEnabled = true; }
    }
    private async void TestHybridSoniox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HybridSonioxTestButton.IsEnabled = false;
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var soniox = new SonioxSession();
            try { await soniox.ConnectAsync(Required(HybridSonioxKeyBox.Password, "Soniox anahtarı"), "tr", "en", cancel.Token, false); }
            finally { await soniox.StopAsync(); }
            FeedbackText.Text = UiLocalizer.T("Soniox bağlantısı başarılı. Ses olmadan konuşma tanıma kalitesi ölçülmez.");
        }
        catch (Exception ex) { Error(ex); }
        finally { HybridSonioxTestButton.IsEnabled = true; }
    }
    private async void TestHybridDeepSeek_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HybridDeepSeekTestButton.IsEnabled = false;
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await SummaryClient.GenerateAsync("DeepSeek", "deepseek-flash", Required(HybridDeepSeekKeyBox.Password, "DeepSeek anahtarı"),
                "Translate into Turkish. Return only the translation: Hello.", 64, cancel.Token);
            FeedbackText.Text = UiLocalizer.T("DeepSeek çeviri denemesi başarılı. Deneme sağlayıcıda kullanıma yazılabilir.");
        }
        catch (Exception ex) { Error(ex); }
        finally { HybridDeepSeekTestButton.IsEnabled = true; }
    }
    private async void TestSummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SummaryTestButton.IsEnabled = false;
            await SummaryClient.GenerateAsync(Provider, Required(ModelBox.Text, "Model"), Required(SummaryKeyBox.Password, Provider + " anahtarı"), "Sadece OK yaz.", 32, CancellationToken.None);
            FeedbackText.Text = Provider + ": " + UiLocalizer.T("bağlantı başarılı; deneme kullanıma yazılabilir.");
        }
        catch (Exception ex) { Error(ex); }
        finally { SummaryTestButton.IsEnabled = true; }
    }
    private void LiveKeyPage_Click(object sender, RoutedEventArgs e) => Open(LiveProvider switch
    {
        LiveProviders.Hybrid => "https://platform.deepseek.com/api_keys",
        LiveProviders.OpenAi => "https://platform.openai.com/api-keys",
        LiveProviders.Gemini => "https://aistudio.google.com/api-keys",
        _ => "https://console.soniox.com/"
    });
    private void SonioxKeyPage_Click(object sender, RoutedEventArgs e) => Open("https://console.soniox.com/");
    private void DeepSeekKeyPage_Click(object sender, RoutedEventArgs e) => Open("https://platform.deepseek.com/api_keys");
    private void SummaryPage_Click(object sender, RoutedEventArgs e) => Open(Provider switch
    {
        "DeepSeek" => "https://platform.deepseek.com/api_keys",
        "OpenAI" => "https://platform.openai.com/api-keys",
        "Gemini" => "https://aistudio.google.com/api-keys",
        _ => "https://console.anthropic.com/settings/keys"
    });
    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = UiLocalizer.T("Kayıt klasörünü seçin") };
        if (picker.ShowDialog(this) == true) ArchivePathBox.Text = picker.FolderName;
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try { SavePreferences(); FeedbackText.Text = UiLocalizer.T("Ayarlar kaydedildi."); }
        catch (Exception ex) { Error(ex); }
    }

    private bool HasUnsavedPreferences() =>
        _settings.SummaryProvider != Provider || _settings.LiveProvider != LiveProvider ||
        _settings.SummaryModel != ModelBox.Text.Trim() ||
        _settings.ArchiveDirectory != ArchivePathBox.Text.Trim() ||
        _settings.UiLanguage != (UiLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ||
        HasUnsavedKeys();

    private bool HasUnsavedKeys()
    {
        if (SummaryKeyBox.Password.Length > 0 && SummaryKeyBox.Password != CredentialStore.ReadForProvider(Provider)) return true;
        if (LiveProvider == LiveProviders.Hybrid)
            return (HybridSonioxKeyBox.Password.Length > 0 && HybridSonioxKeyBox.Password != CredentialStore.Read()) ||
                (HybridDeepSeekKeyBox.Password.Length > 0 && HybridDeepSeekKeyBox.Password != CredentialStore.ReadLiveDeepSeek());
        var keyProvider = LiveProviders.CredentialProvider(LiveProvider);
        var saved = keyProvider == LiveProviders.Soniox ? CredentialStore.Read() : CredentialStore.ReadForProvider(keyProvider);
        return LiveKeyBox.Password.Length > 0 && LiveKeyBox.Password != saved;
    }

    private void SavePendingKeys()
    {
        if (SummaryKeyBox.Password.Length > 0 && SummaryKeyBox.Password != CredentialStore.ReadForProvider(Provider))
            CredentialStore.WriteForProvider(Provider, SummaryKeyBox.Password.Trim());
        if (LiveProvider == LiveProviders.Hybrid)
        {
            if (HybridSonioxKeyBox.Password.Length > 0 && HybridSonioxKeyBox.Password != CredentialStore.Read())
                CredentialStore.Write(HybridSonioxKeyBox.Password.Trim());
            if (HybridDeepSeekKeyBox.Password.Length > 0 && HybridDeepSeekKeyBox.Password != CredentialStore.ReadLiveDeepSeek())
                CredentialStore.WriteLiveDeepSeek(HybridDeepSeekKeyBox.Password.Trim());
            return;
        }
        var keyProvider = LiveProviders.CredentialProvider(LiveProvider);
        if (LiveKeyBox.Password.Length == 0) return;
        if (keyProvider == LiveProviders.Soniox)
        {
            if (LiveKeyBox.Password != CredentialStore.Read()) CredentialStore.Write(LiveKeyBox.Password.Trim());
        }
        else if (LiveKeyBox.Password != CredentialStore.ReadForProvider(keyProvider))
            CredentialStore.WriteForProvider(keyProvider, LiveKeyBox.Password.Trim());
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!HasUnsavedPreferences()) return;
        var answer = MessageBox.Show(this,
            UiLocalizer.T("Kaydedilmemiş ayarlar var. Kapatmadan önce kaydedip uygulamak ister misiniz?"),
            UiLocalizer.T("Kaydedilmemiş ayarlar"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (answer != MessageBoxResult.Yes) return;
        try { SavePreferences(); }
        catch (Exception ex) { e.Cancel = true; Error(ex); }
    }
    private void SavePreferences()
    {
        SavePendingKeys();
        _settings.SummaryProvider = Provider;
        _settings.LiveProvider = LiveProvider;
        _settings.SummaryModel = Required(ModelBox.Text, "Model");
        _settings.ArchiveDirectory = System.IO.Path.GetFullPath(Required(ArchivePathBox.Text, "Kayıt klasörü"));
        ArchivePathBox.Text = _settings.ArchiveDirectory;
        _settings.UiLanguage = (UiLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tr";
        ArchiveStore.SaveSettings(_settings);
        UiLocalizer.Language = _settings.UiLanguage;
        UiLocalizer.Apply(this);
        LiveProvider_SelectionChanged(this, null!);
        LanguageChanged?.Invoke();
        UpdateApplyButton();
    }
    private static string Required(string value, string field) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(field + " " + UiLocalizer.T("gerekli.")) : value.Trim();
    private void Error(Exception ex)
    {
        FeedbackText.Text = ex.Message;
        var dialog = new Window
        {
            Title = UiLocalizer.T("Ayarlar"), Owner = this, Width = 470, SizeToContent = SizeToContent.Height,
            MinHeight = 145, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        var close = new Button { Content = UiLocalizer.T("Tamam"), Width = 85,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0), IsDefault = true };
        close.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(close);
        dialog.Content = panel;
        dialog.ShowDialog();
    }
}
