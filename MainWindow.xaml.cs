using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace WorldClock;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty ColumnCountProperty =
        DependencyProperty.Register(nameof(ColumnCount), typeof(int), typeof(MainWindow), new PropertyMetadata(3));

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private readonly IWeatherService _weather = OpenMeteoClient.Shared;
    private readonly IPlaceSearch _placeSearch = OpenMeteoClient.Shared;
    private readonly ObservableCollection<CityClock> _clocks = [];
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _weatherTimer = new() { Interval = TimeSpan.FromMinutes(15) };
    private bool _weatherBusy;
    private Point? _dragStart;

    public MainWindow()
    {
        _settings = _settingsService.Load();
        ThemeManager.SetPreference(_settings.Theme);
        ThemeManager.Attach(this);

        InitializeComponent();
        foreach (var config in _settings.Clocks)
        {
            if (CityClock.TryCreate(config, out var clock))
                _clocks.Add(clock);
        }

        Topmost = _settings.AlwaysOnTop;
        Clocks.ItemsSource = _clocks;
        _clocks.CollectionChanged += (_, _) => { SaveConfig(); Refresh(); };

        _clockTimer.Tick += (_, _) => Refresh();
        _clockTimer.Start();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _weatherTimer.Start();

        SizeChanged += (_, _) => ColumnCount = Math.Max(1, (int)(ActualWidth / 250));
        Loaded += async (_, _) =>
        {
            if (!_settings.LocationLookupDone)
                await LookUpMissingLocationsAsync();
            await RefreshWeatherAsync();
        };

        Refresh();
    }

    private void Refresh()
    {
        var now = DateTime.UtcNow;
        foreach (var clock in _clocks)
            clock.Update(now, _settings.TemperatureUnit);
    }

    private void SaveConfig()
    {
        _settings.Clocks = _clocks.Select(c => c.ToConfig()).ToList();
        _settingsService.Save(_settings);
    }

    // Weather

    private async Task RefreshWeatherAsync()
    {
        if (_weatherBusy) return;
        var located = _clocks.Where(c => c.Location is not null).ToList();
        if (located.Count == 0) return;

        _weatherBusy = true;
        try
        {
            var results = await _weather.GetCurrentAsync(located.Select(c => c.Location!.Value).ToList());
            var now = DateTime.UtcNow;
            for (var i = 0; i < located.Count && i < results.Count; i++)
                located[i].SetWeather(results[i], now);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                          or KeyNotFoundException or InvalidOperationException)
        {
            // Keep showing the last known weather; it greys out, then clears, as it ages.
        }
        finally
        {
            _weatherBusy = false;
        }
        Refresh();
    }

    /// <summary>
    /// One-off lookup for clocks saved before locations existed. Clocks matching a default get the default's
    /// place; others are looked up by label, and the result is kept only if it's in the clock's time zone.
    /// </summary>
    private async Task LookUpMissingLocationsAsync()
    {
        try
        {
            foreach (var clock in _clocks.Where(c => c.Location is null).ToList())
            {
                var match = SettingsService.Defaults.FirstOrDefault(d =>
                    d.TimeZoneId == clock.TimeZoneId && string.Equals(d.City, clock.City, StringComparison.OrdinalIgnoreCase));
                if (match is { Lat: { } lat, Lon: { } lon, Place: { } place })
                {
                    clock.SetLocation(lat, lon, place);
                    continue;
                }

                var top = (await _placeSearch.SearchAsync(clock.City)).FirstOrDefault();
                if (top is not null && top.TimeZoneId == clock.TimeZoneId)
                    clock.SetLocation(top.Lat, top.Lon, top.DisplayName);
            }
            _settings.LocationLookupDone = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Offline: keep what was found and try the rest on the next launch.
        }
        SaveConfig();
    }

    // Header: + Add clock and the ⋯ menu

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        // Right-align the menu under the button so it opens into the window, not off its edge.
        MoreMenu.CustomPopupPlacementCallback = (popup, target, _) =>
            [new CustomPopupPlacement(new Point(target.Width - popup.Width, target.Height + 4), PopupPrimaryAxis.Horizontal)];
        MoreMenu.IsOpen = true;
    }

    private void MoreMenu_Opened(object sender, RoutedEventArgs e)
    {
        CelsiusItem.IsChecked = _settings.TemperatureUnit == TemperatureUnit.Celsius;
        FahrenheitItem.IsChecked = _settings.TemperatureUnit == TemperatureUnit.Fahrenheit;
        LightItem.IsChecked = _settings.Theme == AppTheme.Light;
        DarkItem.IsChecked = _settings.Theme == AppTheme.Dark;
        SystemThemeItem.IsChecked = _settings.Theme is null;
        TopmostItem.IsChecked = Topmost;
        VersionItem.Header = $"Version {AppInfo.Version}";
    }

    private void Celsius_Click(object sender, RoutedEventArgs e) => SetUnit(TemperatureUnit.Celsius);

    private void Fahrenheit_Click(object sender, RoutedEventArgs e) => SetUnit(TemperatureUnit.Fahrenheit);

    private void SetUnit(TemperatureUnit unit)
    {
        _settings.TemperatureUnit = unit;
        SaveConfig();
        Refresh();
    }

    private void Light_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Light);

    private void Dark_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Dark);

    private void SystemTheme_Click(object sender, RoutedEventArgs e) => SetTheme(null);

    private void SetTheme(AppTheme? theme)
    {
        _settings.Theme = theme;
        ThemeManager.SetPreference(theme);
        SaveConfig();
    }

    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settings.AlwaysOnTop = Topmost;
        SaveConfig();
    }

    private async void AddClock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddClockWindow { Owner = this };
        if (dialog.ShowDialog() == true && CityClock.TryCreate(dialog.ResultClock!, out var clock))
        {
            _clocks.Add(clock);
            await RefreshWeatherAsync();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // Card commands

    private static CityClock? ClockFrom(object sender) => (sender as FrameworkElement)?.DataContext as CityClock;

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (ClockFrom(sender) is not { } clock) return;
        var dialog = new RenameWindow(clock.City) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            clock.Rename(dialog.NewName);
            SaveConfig();
        }
    }

    private async void SetLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ClockFrom(sender) is not { } clock) return;
        var dialog = new AddClockWindow(setLocationFor: clock.City) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultPlace is { } place)
        {
            clock.SetLocation(place.Lat, place.Lon, place.DisplayName);
            SaveConfig();
            Refresh();
            await RefreshWeatherAsync();
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ClockFrom(sender) is { } clock)
            _clocks.Remove(clock);
    }

    private void MoveEarlier_Click(object sender, RoutedEventArgs e) => MoveBy(ClockFrom(sender), -1);

    private void MoveLater_Click(object sender, RoutedEventArgs e) => MoveBy(ClockFrom(sender), 1);

    private void MoveBy(CityClock? clock, int delta)
    {
        if (clock is null) return;
        var from = _clocks.IndexOf(clock);
        var to = from + delta;
        if (to >= 0 && to < _clocks.Count)
            _clocks.Move(from, to);
    }

    // Drag-and-drop reordering: cards shuffle live as the dragged card passes over them.

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = IsInsideButton(e.OriginalSource as DependencyObject) ? null : e.GetPosition(this);
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed || ClockFrom(sender) is not { } clock)
            return;

        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragStart = null;
        clock.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, clock, DragDropEffects.Move);
        }
        finally
        {
            clock.IsDragging = false;
            SaveConfig();
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (e.Data.GetData(typeof(CityClock)) is not CityClock dragged || ClockFrom(sender) is not { } target)
            return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        if (dragged != target)
            _clocks.Move(_clocks.IndexOf(dragged), _clocks.IndexOf(target));
    }

    private void Card_Drop(object sender, DragEventArgs e) => e.Handled = true;

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : null)
        {
            if (element is ButtonBase) return true;
            if (element is System.Windows.Controls.Border { Name: "Card" }) return false;
        }
        return false;
    }
}
