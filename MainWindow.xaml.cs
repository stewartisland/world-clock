using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldClock", "clocks.json");

    private static readonly ClockConfig[] Defaults =
    [
        new("New Zealand", "New Zealand Standard Time"),
        new("Croatia", "Central European Standard Time"),
        new("UK", "GMT Standard Time"),
        new("New York", "Eastern Standard Time"),
        new("Dallas", "Central Standard Time"),
        new("Seattle", "Pacific Standard Time"),
    ];

    private readonly ObservableCollection<CityClock> _clocks = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Point? _dragStart;

    public MainWindow()
    {
        InitializeComponent();

        foreach (var config in LoadConfig())
        {
            if (CityClock.TryCreate(config, out var clock))
                _clocks.Add(clock);
        }

        Clocks.ItemsSource = _clocks;
        _clocks.CollectionChanged += (_, _) => { SaveConfig(); Refresh(); };

        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        SizeChanged += (_, _) => ColumnCount = Math.Max(1, (int)(ActualWidth / 240));
        Refresh();
    }

    private void Refresh()
    {
        var now = DateTime.UtcNow;
        foreach (var clock in _clocks)
            clock.Update(now);
    }

    private static IEnumerable<ClockConfig> LoadConfig()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<ClockConfig[]>(File.ReadAllText(SettingsPath)) ?? Defaults;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return Defaults;
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var configs = _clocks.Select(c => new ClockConfig(c.City, c.TimeZoneId)).ToArray();
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void AddClock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddClockWindow { Owner = this };
        if (dialog.ShowDialog() == true && CityClock.TryCreate(dialog.Result!, out var clock))
            _clocks.Add(clock);
    }

    private static CityClock? ClockFrom(object sender) => (sender as FrameworkElement)?.DataContext as CityClock;

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
            if (element is Border { Name: "Card" }) return false;
        }
        return false;
    }

    private void TopmostToggle_Changed(object sender, RoutedEventArgs e) =>
        Topmost = TopmostToggle.IsChecked == true;
}

public sealed record ClockConfig(string City, string TimeZoneId);

public sealed class CityClock : INotifyPropertyChanged
{
    private readonly TimeZoneInfo _zone;
    private bool _isDragging;

    private CityClock(string city, TimeZoneInfo zone)
    {
        City = city;
        _zone = zone;
    }

    public static bool TryCreate(ClockConfig config, out CityClock clock)
    {
        clock = null!;
        try
        {
            clock = new CityClock(config.City, TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId));
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    public string City { get; }
    public string TimeZoneId => _zone.Id;
    public string Time { get; private set; } = "";
    public string Date { get; private set; } = "";
    public string Offset { get; private set; } = "";
    public string DayNight { get; private set; } = "";

    public bool IsDragging
    {
        get => _isDragging;
        set { _isDragging = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDragging))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(DateTime utcNow)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _zone);
        var offset = _zone.GetUtcOffset(utcNow);
        var diff = offset - TimeZoneInfo.Local.GetUtcOffset(utcNow);

        Time = local.ToString("h:mm tt");
        Date = local.ToString("dddd, d MMM");
        Offset = $"UTC{FormatSpan(offset)} · {DescribeDiff(diff)}";
        DayNight = local.Hour is >= 6 and < 18 ? "☀️" : "🌙";

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static string FormatSpan(TimeSpan t) =>
        (t < TimeSpan.Zero ? "-" : "+") + t.Duration().ToString(t.Minutes == 0 ? "%h" : @"h\:mm");

    private static string DescribeDiff(TimeSpan d) =>
        d == TimeSpan.Zero ? "same as you" : $"{FormatSpan(d)}h from you";
}
