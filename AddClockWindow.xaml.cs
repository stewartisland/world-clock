using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WorldClock;

/// <summary>
/// Adds a clock by searching for a city (which sets both location and time zone), or by picking a
/// time zone manually. In "set location" mode it only picks a place for an existing clock.
/// </summary>
public partial class AddClockWindow : Window
{
    private static readonly IReadOnlyList<TimeZoneInfo> AllZones = TimeZoneInfo.GetSystemTimeZones();

    private readonly bool _setLocationOnly;
    private readonly IPlaceSearch _placeSearch = OpenMeteoClient.Shared;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private CancellationTokenSource? _searchCts;
    private bool _manualZones;

    // Tracks whether the label was typed by the user, so selecting a result only fills it in when it hasn't been.
    private bool _labelEdited;
    private bool _settingLabel;

    /// <summary>The clock to add (add mode).</summary>
    public ClockConfig? ResultClock { get; private set; }

    /// <summary>The chosen place (set-location mode).</summary>
    public PlaceResult? ResultPlace { get; private set; }

    public AddClockWindow(string? setLocationFor = null)
    {
        InitializeComponent();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await RunPlaceSearchAsync(); };

        if (setLocationFor is not null)
        {
            _setLocationOnly = true;
            Title = $"Set location for {setLocationFor}";
            OkButton.Content = "Set";
            LabelPanel.Visibility = Visibility.Collapsed;
            ModeLinkText.Visibility = Visibility.Collapsed;
            Search.Text = setLocationFor;
            Search.SelectAll();
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_manualZones)
        {
            FilterZones();
            return;
        }
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RunPlaceSearchAsync()
    {
        _searchCts?.Cancel();
        var query = Search.Text.Trim();
        if (query.Length < 2)
        {
            Results.ItemsSource = null;
            Status.Text = "";
            return;
        }

        var cts = _searchCts = new CancellationTokenSource();
        Status.Text = "Searching…";
        try
        {
            var places = await _placeSearch.SearchAsync(query, cts.Token);
            if (cts.IsCancellationRequested) return;

            // A new clock needs a time zone we can use; setting a location doesn't.
            var usable = places.Where(p => _setLocationOnly || p.TimeZoneId is not null).ToList();
            Results.ItemsSource = usable;
            Status.Text = usable.Count == 0 ? "No places found. Try another spelling, or choose a time zone manually." : "";
            if (usable.Count > 0)
                Results.SelectedIndex = 0;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Results.ItemsSource = null;
            Status.Text = _setLocationOnly
                ? "Can't reach place search. Check your connection and try again."
                : "Can't reach place search. Check your connection, or choose a time zone manually.";
        }
        UpdateOkButton();
    }

    private void FilterZones()
    {
        var terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Results.ItemsSource = AllZones
            .Where(z => terms.All(t => z.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase)
                                    || z.Id.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (Results.Items.Count > 0)
            Results.SelectedIndex = 0;
    }

    private void ModeLink_Click(object sender, RoutedEventArgs e)
    {
        _manualZones = !_manualZones;
        _debounce.Stop();
        _searchCts?.Cancel();
        Status.Text = "";
        SearchPrompt.Text = _manualZones ? "Search time zones (city, country or UTC offset)" : "Search for a city";
        ModeLinkRun.Text = _manualZones ? "Search for a city instead" : "Choose time zone manually";
        Results.ItemsSource = null;
        Search_TextChanged(Search, null!);
        Search.Focus();
    }

    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && Results.Items.Count > 0)
        {
            var step = e.Key == Key.Down ? 1 : -1;
            Results.SelectedIndex = Math.Clamp(Results.SelectedIndex + step, 0, Results.Items.Count - 1);
            Results.ScrollIntoView(Results.SelectedItem);
            e.Handled = true;
        }
    }

    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var suggestion = Results.SelectedItem switch
        {
            PlaceResult place => place.Name,
            TimeZoneInfo zone => SuggestLabel(zone),
            _ => null,
        };
        if (suggestion is not null && !_labelEdited)
        {
            _settingLabel = true;
            LabelBox.Text = suggestion;
            _settingLabel = false;
        }
        UpdateOkButton();
    }

    private void Label_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingLabel)
            _labelEdited = LabelBox.Text.Length > 0;
        UpdateOkButton();
    }

    private void UpdateOkButton() =>
        OkButton.IsEnabled = Results.SelectedItem is PlaceResult or TimeZoneInfo
                             && (_setLocationOnly || !string.IsNullOrWhiteSpace(LabelBox.Text));

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (OkButton.IsEnabled)
            Ok_Click(sender, e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var label = LabelBox.Text.Trim();
        switch (Results.SelectedItem)
        {
            case PlaceResult place when _setLocationOnly:
                ResultPlace = place;
                break;
            case PlaceResult place:
                ResultClock = new ClockConfig(label, place.TimeZoneId!, place.Lat, place.Lon, place.DisplayName);
                break;
            case TimeZoneInfo zone:
                ResultClock = new ClockConfig(label, zone.Id);
                break;
            default:
                return;
        }
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
