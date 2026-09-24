import Foundation

/// Everything one clock card shows, worked out for a single instant.
struct ClockDisplay: Equatable {
    static let staleAfter: TimeInterval = 20 * 60
    static let discardAfter: TimeInterval = 2 * 60 * 60

    var time: String
    var date: String
    var offset: String
    /// SF Symbol name for the weather, or for day/night when there's no weather.
    var symbol: String
    var temperature: String
    var place: String?
    var hasLocation: Bool
    var isWeatherStale: Bool

    init(
        clock: ClockConfig,
        zone: TimeZone,
        reading: WeatherReading?,
        unit: TemperatureUnit,
        now: Date,
        localZone: TimeZone = .autoupdatingCurrent,
        locale: Locale = .autoupdatingCurrent
    ) {
        var timeStyle = Date.FormatStyle(date: .omitted, time: .shortened, locale: locale)
        timeStyle.timeZone = zone
        var dateStyle = Date.FormatStyle(locale: locale).weekday(.wide).day().month(.abbreviated)
        dateStyle.timeZone = zone

        let offset = zone.secondsFromGMT(for: now)
        let diff = offset - localZone.secondsFromGMT(for: now)

        time = now.formatted(timeStyle)
        date = now.formatted(dateStyle)
        self.offset = "UTC\(Self.formatSpan(offset)) · \(Self.describeDiff(diff))"
        place = clock.place
        hasLocation = clock.hasLocation

        if let reading, now.timeIntervalSince(reading.at) < Self.discardAfter {
            let age = now.timeIntervalSince(reading.at)
            temperature = Self.temperature(reading.weather.temperatureC, unit: unit)
            symbol = Self.weatherSymbol(code: reading.weather.weatherCode, isDay: reading.weather.isDay)
            isWeatherStale = age > Self.staleAfter
        } else {
            var calendar = Calendar(identifier: .gregorian)
            calendar.timeZone = zone
            let hour = calendar.component(.hour, from: now)
            temperature = hasLocation ? "–" : ""
            symbol = (6..<18).contains(hour) ? "sun.max.fill" : "moon.stars.fill"
            isWeatherStale = false
        }
    }

    static func temperature(_ celsius: Double, unit: TemperatureUnit) -> String {
        let value = unit == .fahrenheit ? celsius * 9 / 5 + 32 : celsius
        return "\(Int(value.rounded()))°"
    }

    // WMO weather codes, as returned by Open-Meteo.
    static func weatherSymbol(code: Int, isDay: Bool) -> String {
        switch code {
        case 0: isDay ? "sun.max.fill" : "moon.stars.fill"
        case 1, 2: isDay ? "cloud.sun.fill" : "cloud.moon.fill"
        case 3: "cloud.fill"
        case 45, 48: "cloud.fog.fill"
        case 51...67, 80...82: "cloud.rain.fill"
        case 71...77, 85, 86: "cloud.snow.fill"
        case 95...99: "cloud.bolt.rain.fill"
        default: isDay ? "sun.max.fill" : "moon.stars.fill"
        }
    }

    /// "+12", "-4", "+5:30"
    static func formatSpan(_ seconds: Int) -> String {
        let sign = seconds < 0 ? "-" : "+"
        let minutes = abs(seconds) / 60
        let (h, m) = (minutes / 60, minutes % 60)
        return m == 0 ? "\(sign)\(h)" : "\(sign)\(h):\(String(format: "%02d", m))"
    }

    private static func describeDiff(_ seconds: Int) -> String {
        seconds == 0 ? "same as you" : "\(formatSpan(seconds))h from you"
    }
}
