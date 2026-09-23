using System.IO;

namespace WorldClock.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("worldclock-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingFile_GivesDefaultsWithLocations()
    {
        var settings = new SettingsService(_dir).Load();

        Assert.True(SettingsService.IsDefaultClocks(settings.Clocks));
        Assert.All(settings.Clocks, c => Assert.NotNull(c.Lat));
        Assert.True(settings.LocationLookupDone);
    }

    [Fact]
    public void Pre1_0BareArray_IsMigratedAndFlaggedForLocationLookup()
    {
        File.WriteAllText(Path.Combine(_dir, "clocks.json"),
            """[{ "City": "Seattle", "TimeZoneId": "Pacific Standard Time" }]""");

        var settings = new SettingsService(_dir).Load();

        var clock = Assert.Single(settings.Clocks);
        Assert.Equal("Seattle", clock.City);
        Assert.Null(clock.Lat);
        Assert.False(settings.LocationLookupDone);
    }

    [Fact]
    public void FileFromNewerVersion_IsNeverOverwritten()
    {
        var path = Path.Combine(_dir, "clocks.json");
        var original = """{ "version": 99, "clocks": [] }""";
        File.WriteAllText(path, original);

        var service = new SettingsService(_dir);
        service.Save(service.Load());

        Assert.False(service.CanSave);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Save_LocalChangeStampsTime_SyncedCopyKeepsItsTime()
    {
        var service = new SettingsService(_dir);
        var settings = new AppSettings { UpdatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) };

        service.Save(settings, isLocalChange: false);
        Assert.Equal(2020, service.Load().UpdatedAt.Year);

        service.Save(settings);
        Assert.True(service.Load().UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void RoundTrip_KeepsThemeUnitAndAlwaysOnTop()
    {
        var service = new SettingsService(_dir);
        service.Save(new AppSettings
        {
            Theme = AppTheme.Light,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            AlwaysOnTop = true,
            Clocks = [new ClockConfig("Lima", "SA Pacific Standard Time", -12.0432, -77.0282, "Lima, Peru")],
        });

        var loaded = service.Load();

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.Equal(TemperatureUnit.Fahrenheit, loaded.TemperatureUnit);
        Assert.True(loaded.AlwaysOnTop);
        Assert.Equal(-12.0432, Assert.Single(loaded.Clocks).Lat);
    }
}
