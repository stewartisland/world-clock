using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WorldClock;

/// <summary>Reads and writes %APPDATA%\WorldClock\clocks.json, migrating the old bare-array format.</summary>
public sealed class SettingsService
{
    private static readonly string DefaultDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldClock");

    private readonly string _settingsPath;
    private readonly string _backupPath;

    /// <param name="directory">Where clocks.json is kept; defaults to %APPDATA%\WorldClock.</param>
    public SettingsService(string? directory = null)
    {
        _settingsPath = Path.Combine(directory ?? DefaultDirectory, "clocks.json");
        _backupPath = Path.Combine(directory ?? DefaultDirectory, "clocks.backup.json");
    }

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
            if (File.Exists(_settingsPath))
                return Parse(File.ReadAllText(_settingsPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
        }
        return CreateDefault();
    }

    /// <summary>
    /// Writes the settings file. A local change stamps <c>updatedAt</c> and <c>deviceName</c>; settings arriving
    /// from sync pass <paramref name="isLocalChange"/> false to keep the timestamp of the device that made them.
    /// </summary>
    public void Save(AppSettings settings, bool isLocalChange = true)
    {
        if (!CanSave) return;
        settings.Version = AppSettings.CurrentVersion;
        if (isLocalChange)
        {
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            settings.DeviceName = Environment.MachineName;
        }
        Write(_settingsPath, settings);
    }

    /// <summary>Saves a copy that's about to be replaced by sync, as clocks.backup.json next to clocks.json.</summary>
    public void SaveBackup(AppSettings settings) => Write(_backupPath, settings);

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, JsonOptions);

    /// <summary>Parses a version-1-or-later settings file, such as the synced copy.</summary>
    public static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? throw new JsonException("Empty settings file.");

    public static bool IsDefaultClocks(IEnumerable<ClockConfig> clocks) => clocks.SequenceEqual(Defaults);

    private static void Write(string path, AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Serialize(settings));
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
                UpdatedAt = File.GetLastWriteTimeUtc(_settingsPath),
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
