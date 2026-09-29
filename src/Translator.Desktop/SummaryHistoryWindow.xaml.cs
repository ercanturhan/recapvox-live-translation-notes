using System.Windows;
using System.Windows.Controls;

namespace Translator.Desktop;

public partial class SummaryHistoryWindow : Window
{
    internal SummaryHistoryWindow(SessionRecord record)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(record.Title) ? record.Source : record.Title;
        SummaryBox.ItemsSource = record.Summaries.OrderByDescending(x => x.CreatedAt)
            .Select(x => new SummaryItem(x)).ToArray();
        SummaryBox.SelectedIndex = 0;
        UiLocalizer.Apply(this);
    }

    private void Summary_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SummaryText is not null && SummaryBox.SelectedItem is SummaryItem item) SummaryText.Text = item.Value.Text;
    }

    private sealed record SummaryItem(SummaryRecord Value)
    {
        public string DisplayName => $"{Value.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} · {Value.Language} · {Value.Provider} · {UiLocalizer.T(Value.Kind)}";
    }
}
