using System.Text.Json.Serialization;

namespace WorldClock;

/// <summary>One saved clock. Lat/Lon/Place are optional: clocks without them show "Set location…".</summary>
public sealed record ClockConfig(
    string City,
    string TimeZoneId,
    double? Lat = null,
    double? Lon = null,
    string? Place = null);

public enum TemperatureUnit { Celsius, Fahrenheit }

/// <summary>The contents of clocks.json (format version 1).</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")] public int Version { get; set; } = CurrentVersion;
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("deviceName")] public string? DeviceName { get; set; }
    [JsonPropertyName("temperatureUnit")] public TemperatureUnit TemperatureUnit { get; set; }

    /// <summary>Null until the user picks one; the Windows app theme is used until then.</summary>
    [JsonPropertyName("theme")] public AppTheme? Theme { get; set; }

    /// <summary>False until existing clocks have had their one-off location lookup by label.</summary>
    [JsonPropertyName("locationLookupDone")] public bool LocationLookupDone { get; set; }

    [JsonPropertyName("clocks")] public List<ClockConfig> Clocks { get; set; } = [];
}

/// <summary>A place returned by place search, with its time zone already converted to a Windows ID.</summary>
public sealed record PlaceResult(string Name, string DisplayName, double Lat, double Lon, string? TimeZoneId);

public sealed record CurrentWeather(double TemperatureC, int WeatherCode, bool IsDay);
