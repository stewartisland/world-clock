using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WorldClock;

/// <summary>Reads and writes %APPDATA%\WorldClock\clocks.json, migrating the old bare-array format.</summary>
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldClock", "clocks.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly ClockConfig[] Defaults =
    [
        new("New Zealand", "New Zealand Standard Time", -36.8485, 174.7633, "Auckland, New Zealand"),
        new("Croatia", "Central European Standard Time", 45.815, 15.9819, "Zagreb, Croatia"),
        new("UK", "GMT Standard Time", 51.5085, -0.1257, "London, England, United Kingdom"),
        new("New York", "Eastern Standard Time", 40.7143, -74.006, "New York, United States"),
        new("Dallas", "Central Standard Time", 32.7831, -96.8067, "Dallas, Texas, United States"),
        new("Seattle", "Pacific Standard Time", 47.6062, -122.3321, "Seattle, Washington, United States"),
    ];

    /// <summary>False when the file was written by a newer version of the app; it is then left untouched.</summary>
    public bool CanSave { get; private set; } = true;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return Parse(File.ReadAllText(SettingsPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
        }
        return CreateDefault();
    }

    public void Save(AppSettings settings)
    {
        if (!CanSave) return;
        try
        {
            settings.Version = AppSettings.CurrentVersion;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            settings.DeviceName = Environment.MachineName;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private AppSettings Parse(string json)
    {
        var root = JsonNode.Parse(json);

        // Pre-version-1 files are a bare array of clocks.
        if (root is JsonArray)
        {
            return new AppSettings
            {
                UpdatedAt = File.GetLastWriteTimeUtc(SettingsPath),
                TemperatureUnit = DefaultUnit(),
                LocationLookupDone = false,
                Clocks = root.Deserialize<List<ClockConfig>>(JsonOptions) ?? [],
            };
        }

        var settings = root.Deserialize<AppSettings>(JsonOptions) ?? CreateDefault();
        if (settings.Version > AppSettings.CurrentVersion)
            CanSave = false;
        return settings;
    }

    private static AppSettings CreateDefault() => new()
    {
        TemperatureUnit = DefaultUnit(),
        LocationLookupDone = true,
        Clocks = [.. Defaults],
    };

    private static TemperatureUnit DefaultUnit() =>
        RegionInfo.CurrentRegion.IsMetric ? TemperatureUnit.Celsius : TemperatureUnit.Fahrenheit;
}
