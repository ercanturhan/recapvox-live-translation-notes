using System.Windows;
using System.Windows.Controls;

namespace Translator.Desktop;

public partial class OverlaySettingsWindow : Window
{
    private readonly AppSettings _settings;

    internal OverlaySettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ContentBox.SelectedIndex = settings.OverlayContent switch { "Orijinal" => 1, "Çeviri" => 2, _ => 0 };
        LayoutBox.SelectedIndex = settings.OverlayLayout == "Yan yana" ? 1 : 0;
        WidthSlider.Value = settings.OverlayWidth;
        FontSlider.Value = settings.OverlayFontSize;
        UiLocalizer.Apply(this);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.OverlayContent = ((ComboBoxItem)ContentBox.SelectedItem).Tag.ToString()!;
        _settings.OverlayLayout = ((ComboBoxItem)LayoutBox.SelectedItem).Tag.ToString()!;
        _settings.OverlayWidth = (int)WidthSlider.Value;
        _settings.OverlayFontSize = (int)FontSlider.Value;
        ArchiveStore.SaveSettings(_settings);
        DialogResult = true;
    }
}
