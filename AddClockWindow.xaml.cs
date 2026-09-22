using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorldClock;

public partial class AddClockWindow : Window
{
    private static readonly IReadOnlyList<TimeZoneInfo> AllZones = TimeZoneInfo.GetSystemTimeZones();

    // Tracks whether the label was typed by the user, so selecting a zone only fills it in when it hasn't been.
    private bool _labelEdited;
    private bool _settingLabel;

    public ClockConfig? Result { get; private set; }

    public AddClockWindow()
    {
        InitializeComponent();
        Zones.ItemsSource = AllZones;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        var terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Zones.ItemsSource = AllZones
            .Where(z => terms.All(t => z.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase)
                                    || z.Id.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (Zones.Items.Count > 0)
            Zones.SelectedIndex = 0;
    }

    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && Zones.Items.Count > 0)
        {
            var step = e.Key == Key.Down ? 1 : -1;
            Zones.SelectedIndex = Math.Clamp(Zones.SelectedIndex + step, 0, Zones.Items.Count - 1);
            Zones.ScrollIntoView(Zones.SelectedItem);
            e.Handled = true;
        }
    }

    private void Zones_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Zones.SelectedItem is TimeZoneInfo zone && !_labelEdited)
        {
            _settingLabel = true;
            LabelBox.Text = SuggestLabel(zone);
            _settingLabel = false;
        }
        UpdateAddButton();
    }

    private void Label_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingLabel)
            _labelEdited = LabelBox.Text.Length > 0;
        UpdateAddButton();
    }

    private void UpdateAddButton() =>
        AddButton.IsEnabled = Zones.SelectedItem is TimeZoneInfo && !string.IsNullOrWhiteSpace(LabelBox.Text);

    private void Zones_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AddButton.IsEnabled)
            Add_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (Zones.SelectedItem is not TimeZoneInfo zone) return;
        Result = new ClockConfig(LabelBox.Text.Trim(), zone.Id);
        DialogResult = true;
    }

    // "(UTC+12:00) Auckland, Wellington" -> "Auckland"
    private static string SuggestLabel(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var close = name.IndexOf(')');
        if (close >= 0)
            name = name[(close + 1)..];
        return name.Split(',')[0].Trim();
    }
}
