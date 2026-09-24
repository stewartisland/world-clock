import Foundation

/// Reads and writes Application Support/WorldClock/clocks.json.
final class SettingsService {
    static let defaults: [ClockConfig] = [
        ClockConfig(city: "New Zealand", timeZoneId: "Pacific/Auckland", lat: -36.8485, lon: 174.7633, place: "Auckland, New Zealand"),
        ClockConfig(city: "Croatia", timeZoneId: "Europe/Zagreb", lat: 45.815, lon: 15.9819, place: "Zagreb, Croatia"),
        ClockConfig(city: "UK", timeZoneId: "Europe/London", lat: 51.5085, lon: -0.1257, place: "London, England, United Kingdom"),
        ClockConfig(city: "New York", timeZoneId: "America/New_York", lat: 40.7143, lon: -74.006, place: "New York, United States"),
        ClockConfig(city: "Dallas", timeZoneId: "America/Chicago", lat: 32.7831, lon: -96.8067, place: "Dallas, Texas, United States"),
        ClockConfig(city: "Seattle", timeZoneId: "America/Los_Angeles", lat: 47.6062, lon: -122.3321, place: "Seattle, Washington, United States"),
    ]

    static let defaultURL = URL.applicationSupportDirectory
        .appending(path: "WorldClock", directoryHint: .isDirectory)
        .appending(path: "clocks.json")

    private let url: URL
    private let locale: Locale

    /// False when the file was written by a newer version of the app; it is then left untouched.
    private(set) var canSave = true

    init(url: URL = SettingsService.defaultURL, locale: Locale = .current) {
        self.url = url
        self.locale = locale
    }

    func load() -> AppSettings {
        guard let data = try? Data(contentsOf: url),
              let settings = try? Self.decoder.decode(AppSettings.self, from: data)
        else { return makeDefault() }

        if settings.version > AppSettings.currentVersion {
            canSave = false
        }
        return settings
    }

    func save(_ settings: AppSettings) {
        guard canSave else { return }
        var settings = settings
        settings.version = AppSettings.currentVersion
        settings.updatedAt = .now
        do {
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Self.encoder.encode(settings).write(to: url, options: .atomic)
        } catch {
            // Keep running with the in-memory settings; the next change tries again.
        }
    }

    func makeDefault() -> AppSettings {
        AppSettings(
            temperatureUnit: locale.measurementSystem == .us ? .fahrenheit : .celsius,
            clocks: Self.defaults)
    }

    private static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }()

    private static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()
}
