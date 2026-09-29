using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Translator.Desktop;

public partial class OverlayWindow : Window
{
    private AppSettings _settings;
    private bool _userMoved;
    private readonly OverlayCaptionStream _original = new();
    private readonly OverlayCaptionStream _translated = new();
    private readonly DispatcherTimer _captionTimer = new() { Interval = TimeSpan.FromMilliseconds(360) };
    private readonly TranslateTransform _originalMotion = new();
    private readonly TranslateTransform _translatedMotion = new();
    private IReadOnlyList<TranscriptSegment> _lastSegments = [];

    internal OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        OriginalLine.RenderTransform = _originalMotion;
        TranslatedLine.RenderTransform = _translatedMotion;
        _captionTimer.Tick += (_, _) => AdvanceCaptions();
        Closed += (_, _) => _captionTimer.Stop();
        ApplySettings(settings);
    }
    private void Window_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
        _userMoved = true;
    }

    internal void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Width = Math.Clamp(settings.OverlayWidth, 420, Math.Max(420, (int)SystemParameters.WorkArea.Width));
        OriginalLine.FontSize = Math.Clamp(settings.OverlayFontSize, 14, 34);
        TranslatedLine.FontSize = Math.Clamp(settings.OverlayFontSize, 14, 34);
        OriginalLine.Visibility = !settings.TranscriptionOnly && settings.OverlayContent == "Çeviri" ? Visibility.Collapsed : Visibility.Visible;
        TranslatedLine.Visibility = settings.TranscriptionOnly || settings.OverlayContent == "Orijinal" ? Visibility.Collapsed : Visibility.Visible;
        SubtitleGrid.RowDefinitions.Clear();
        SubtitleGrid.ColumnDefinitions.Clear();
        if (!settings.TranscriptionOnly && settings.OverlayContent == "İkisi" && settings.OverlayLayout == "Yan yana")
        {
            SubtitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SubtitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            OriginalLine.Margin = new Thickness(0, 0, 10, 0);
            TranslatedLine.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetRow(OriginalLine, 0); Grid.SetColumn(OriginalLine, 0);
            Grid.SetRow(TranslatedLine, 0); Grid.SetColumn(TranslatedLine, 1);
        }
        else
        {
            SubtitleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SubtitleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            OriginalLine.Margin = new Thickness(0);
            TranslatedLine.Margin = settings.OverlayContent == "İkisi" ? new Thickness(0, 9, 0, 0) : new Thickness(0);
            Grid.SetColumn(OriginalLine, 0); Grid.SetRow(OriginalLine, 0);
            Grid.SetColumn(TranslatedLine, 0); Grid.SetRow(TranslatedLine, 1);
        }
        RefreshSize();
        _original.Reset();
        _translated.Reset();
        UpdateSegments(_lastSegments);
    }

    internal void UpdateSegments(IReadOnlyList<TranscriptSegment> segments)
    {
        _lastSegments = segments;
        var sideBySide = !_settings.TranscriptionOnly && _settings.OverlayContent == "İkisi" && _settings.OverlayLayout == "Yan yana";
        var availableWidth = sideBySide ? (Width - 66) / 2 : Width - 38;
        var wordsPerChunk = Math.Clamp((int)(availableWidth / (OriginalLine.FontSize * 3.2)), 6, 24);
        if (_original.Update(OverlayText.Chunks(segments, false, wordsPerChunk))) OriginalLine.Text = _original.VisibleText;
        if (_translated.Update(OverlayText.Chunks(segments, true, wordsPerChunk))) TranslatedLine.Text = _translated.VisibleText;
        if (_original.HasPending || _translated.HasPending) _captionTimer.Start();
    }

    private void AdvanceCaptions()
    {
        if (_original.Advance())
        {
            OriginalLine.Text = _original.VisibleText;
            Slide(_originalMotion, OriginalLine.FontSize);
        }
        if (_translated.Advance())
        {
            TranslatedLine.Text = _translated.VisibleText;
            Slide(_translatedMotion, TranslatedLine.FontSize);
        }
        if (!_original.HasPending && !_translated.HasPending) _captionTimer.Stop();
    }

    private static void Slide(TranslateTransform transform, double fontSize)
    {
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = Math.Max(16, fontSize * 1.25), To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void RefreshSize()
    {
        var maxHeight = Math.Max(110, SystemParameters.WorkArea.Height - 30);
        var sideBySide = !_settings.TranscriptionOnly && _settings.OverlayContent == "İkisi" && _settings.OverlayLayout == "Yan yana";
        var textWidth = sideBySide ? (Width - 66) / 2 : Width - 38;
        var linesPerChunk = Math.Max(1, Math.Ceiling(54 * OriginalLine.FontSize * 0.52 / Math.Max(100, textWidth)));
        var laneHeight = 2 * linesPerChunk * OriginalLine.FontSize * 1.32;
        var bothVisible = OriginalLine.Visibility == Visibility.Visible && TranslatedLine.Visibility == Visibility.Visible;
        Height = Math.Min(maxHeight, Math.Max(76, laneHeight * (bothVisible && !sideBySide ? 2 : 1) + (bothVisible && !sideBySide ? 9 : 0) + 40));
        var area = SystemParameters.WorkArea;
        Left = _userMoved ? Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width))
            : area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = _userMoved ? Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height))
            : area.Bottom - Height - 24;
    }

}
