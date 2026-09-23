using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace WorldClock;

public interface IWeatherService
{
    /// <summary>Current weather for each location, in the same order. Throws on network or API failure.</summary>
    Task<IReadOnlyList<CurrentWeather>> GetCurrentAsync(IReadOnlyList<(double Lat, double Lon)> locations, CancellationToken ct = default);
}

public interface IPlaceSearch
{
    /// <summary>Places matching the name. Throws on network or API failure.</summary>
    Task<IReadOnlyList<PlaceResult>> SearchAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Weather and place search from open-meteo.com. Free, no API key, non-commercial use only, CC BY 4.0.
/// </summary>
public sealed class OpenMeteoClient : IWeatherService, IPlaceSearch
{
    public static OpenMeteoClient Shared { get; } = new();

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"WorldClock/{AppInfo.Version} (+https://github.com/stewartisland/world-clock)");
        return http;
    }

    public async Task<IReadOnlyList<CurrentWeather>> GetCurrentAsync(
        IReadOnlyList<(double Lat, double Lon)> locations, CancellationToken ct = default)
    {
        if (locations.Count == 0) return [];

        var lats = string.Join(',', locations.Select(l => Format(l.Lat)));
        var lons = string.Join(',', locations.Select(l => Format(l.Lon)));
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lats}&longitude={lons}" +
                  "&current=temperature_2m,weather_code,is_day&temperature_unit=celsius";

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));

        // One location returns an object; several return an array in request order.
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : [doc.RootElement];

        return items.Select(item =>
        {
            var current = item.GetProperty("current");
            return new CurrentWeather(
                current.GetProperty("temperature_2m").GetDouble(),
                current.GetProperty("weather_code").GetInt32(),
                current.GetProperty("is_day").GetInt32() == 1);
        }).ToList();
    }

    public async Task<IReadOnlyList<PlaceResult>> SearchAsync(string name, CancellationToken ct = default)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search?count=10&language=en&format=json&name=" +
                  Uri.EscapeDataString(name);

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return [];

        return results.EnumerateArray().Select(r =>
        {
            var placeName = GetString(r, "name") ?? "";
            var parts = new[] { placeName, GetString(r, "admin1"), GetString(r, "country") }
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct();
            var iana = GetString(r, "timezone");
            string? windowsId = iana is not null && TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out var id) ? id : null;

            return new PlaceResult(
                placeName,
                string.Join(", ", parts),
                Math.Round(r.GetProperty("latitude").GetDouble(), 4),
                Math.Round(r.GetProperty("longitude").GetDouble(), 4),
                windowsId);
        }).ToList();
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Format(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}
