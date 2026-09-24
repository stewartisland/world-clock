import Foundation

/// The app version (Major.Minor), from MARKETING_VERSION in the Xcode project.
enum AppInfo {
    static let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.0"
}

/// One saved clock. Lat/Lon/Place are optional: clocks without them show "Set location…".
/// Keys match the desktop app's clocks.json, but `timeZoneId` is an IANA ID ("Pacific/Auckland")
/// where the desktop stores a Windows ID ("New Zealand Standard Time").
struct ClockConfig: Codable, Equatable, Identifiable {
    /// Identifies the clock while the app runs; not saved.
    var id = UUID()
    var city: String
    var timeZoneId: String
    var lat: Double?
    var lon: Double?
    var place: String?

    enum CodingKeys: String, CodingKey {
        case city = "City", timeZoneId = "TimeZoneId", lat = "Lat", lon = "Lon", place = "Place"
    }

    var hasLocation: Bool { lat != nil && lon != nil }
}

enum TemperatureUnit: String, Codable {
    case celsius = "Celsius"
    case fahrenheit = "Fahrenheit"
}

enum AppTheme: String, Codable {
    case light = "Light"
    case dark = "Dark"
}

/// The contents of clocks.json (format version 1).
struct AppSettings: Codable {
    static let currentVersion = 1

    var version = currentVersion
    var updatedAt: Date?
    var deviceName: String?
    var temperatureUnit: TemperatureUnit

    /// Light or Dark; nil (the default) follows the iPhone's appearance.
    var theme: AppTheme?

    var clocks: [ClockConfig]
}

/// A place returned by place search. `timeZoneId` is nil when the IANA ID isn't known to iOS.
struct PlaceResult: Identifiable, Hashable {
    var name: String
    var displayName: String
    var lat: Double
    var lon: Double
    var timeZoneId: String?

    var id: String { "\(lat),\(lon),\(displayName)" }
}

struct CurrentWeather: Equatable {
    var temperatureC: Double
    var weatherCode: Int
    var isDay: Bool
}

/// Weather for one clock and when it was fetched.
struct WeatherReading: Equatable {
    var weather: CurrentWeather
    var at: Date
}
