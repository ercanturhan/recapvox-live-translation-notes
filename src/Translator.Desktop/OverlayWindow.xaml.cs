using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Translator.Desktop;

public partial class OverlayWindow : Window
{
    private AppSettings _settings;
    private bool _userMoved;

    internal OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
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
        OriginalLine.Visibility = settings.OverlayContent == "Çeviri" ? Visibility.Collapsed : Visibility.Visible;
        TranslatedLine.Visibility = settings.OverlayContent == "Orijinal" ? Visibility.Collapsed : Visibility.Visible;
        SubtitleGrid.RowDefinitions.Clear();
        SubtitleGrid.ColumnDefinitions.Clear();
        if (settings.OverlayContent == "İkisi" && settings.OverlayLayout == "Yan yana")
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
    }

    internal void UpdateSegments(IReadOnlyList<TranscriptSegment> segments)
    {
        var current = OverlayText.Current(segments);
        OriginalLine.Text = current.Original;
        TranslatedLine.Text = current.Translation;
        RefreshSize();
    }

    private void RefreshSize()
    {
        var maxHeight = Math.Max(110, SystemParameters.WorkArea.Height - 30);
        var sideBySide = _settings.OverlayContent == "İkisi" && _settings.OverlayLayout == "Yan yana";
        var textWidth = sideBySide ? (Width - 66) / 2 : Width - 38;
        var originalHeight = OriginalLine.Visibility == Visibility.Visible ? Measure(OriginalLine.Text, textWidth, OriginalLine.FontSize) : 0;
        var translationHeight = TranslatedLine.Visibility == Visibility.Visible ? Measure(TranslatedLine.Text, textWidth, TranslatedLine.FontSize) : 0;
        Height = Math.Min(maxHeight, Math.Max(76, (sideBySide ? Math.Max(originalHeight, translationHeight) : originalHeight + translationHeight + 9) + 40));
        var area = SystemParameters.WorkArea;
        Left = _userMoved ? Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width))
            : area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = _userMoved ? Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height))
            : area.Bottom - Height - 24;
    }

    private static double Measure(string value, double width, double fontSize)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var text = new FormattedText(value, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), fontSize, Brushes.White, 1.0) { MaxTextWidth = Math.Max(100, width) };
        return text.Height;
    }
}
