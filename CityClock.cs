using System.ComponentModel;

namespace WorldClock;

/// <summary>View model for one clock card.</summary>
public sealed class CityClock : INotifyPropertyChanged
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan DiscardAfter = TimeSpan.FromHours(2);

    private readonly TimeZoneInfo _zone;
    private bool _isDragging;
    private CurrentWeather? _weather;
    private DateTime _weatherAtUtc;

    private CityClock(ClockConfig config, TimeZoneInfo zone)
    {
        City = config.City;
        _zone = zone;
        if (config.Lat is { } lat && config.Lon is { } lon)
            Location = (lat, lon);
        Place = config.Place;
    }

    public static bool TryCreate(ClockConfig config, out CityClock clock)
    {
        clock = null!;
        try
        {
            clock = new CityClock(config, TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId));
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    public string City { get; private set; }
    public string TimeZoneId => _zone.Id;
    public (double Lat, double Lon)? Location { get; private set; }
    public string? Place { get; private set; }

    public string Time { get; private set; } = "";
    public string Date { get; private set; } = "";
    public string Offset { get; private set; } = "";
    public string Icon { get; private set; } = "";
    public string Temperature { get; private set; } = "";
    public bool IsWeatherStale { get; private set; }
    public bool HasLocation => Location is not null;

    public bool IsDragging
    {
        get => _isDragging;
        set { _isDragging = value; Notify(nameof(IsDragging)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ClockConfig ToConfig() => new(City, TimeZoneId, Location?.Lat, Location?.Lon, Place);

    public void Rename(string city)
    {
        City = city;
        Notify(null);
    }

    public void SetLocation(double lat, double lon, string place)
    {
        Location = (lat, lon);
        Place = place;
        _weather = null;
        Notify(null);
    }

    public void SetWeather(CurrentWeather weather, DateTime atUtc)
    {
        _weather = weather;
        _weatherAtUtc = atUtc;
    }

    public void Update(DateTime utcNow, TemperatureUnit unit)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _zone);
        var offset = _zone.GetUtcOffset(utcNow);
        var diff = offset - TimeZoneInfo.Local.GetUtcOffset(utcNow);

        Time = local.ToString("h:mm tt");
        Date = local.ToString("dddd, d MMM");
        Offset = $"UTC{FormatSpan(offset)} · {DescribeDiff(diff)}";

        var age = utcNow - _weatherAtUtc;
        if (_weather is { } w && age < DiscardAfter)
        {
            var value = unit == TemperatureUnit.Fahrenheit ? w.TemperatureC * 9 / 5 + 32 : w.TemperatureC;
            Temperature = $"{Math.Round(value):0}°";
            Icon = WeatherIcon(w.WeatherCode, w.IsDay);
            IsWeatherStale = age > StaleAfter;
        }
        else
        {
            Temperature = HasLocation ? "–" : "";
            Icon = local.Hour is >= 6 and < 18 ? "☀️" : "🌙";
            IsWeatherStale = false;
        }

        Notify(null);
    }

    // WMO weather codes, as returned by Open-Meteo.
    private static string WeatherIcon(int code, bool isDay) => code switch
    {
        0 => isDay ? "☀️" : "🌙",
        1 or 2 => isDay ? "⛅" : "☁️",
        3 => "☁️",
        45 or 48 => "🌫️",
        (>= 51 and <= 67) or (>= 80 and <= 82) => "🌧️",
        (>= 71 and <= 77) or 85 or 86 => "❄️",
        >= 95 and <= 99 => "⛈️",
        _ => isDay ? "☀️" : "🌙",
    };

    private void Notify(string? property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private static string FormatSpan(TimeSpan t) =>
        (t < TimeSpan.Zero ? "-" : "+") + t.Duration().ToString(t.Minutes == 0 ? "%h" : @"h\:mm");

    private static string DescribeDiff(TimeSpan d) =>
        d == TimeSpan.Zero ? "same as you" : $"{FormatSpan(d)}h from you";
}
