import Foundation
import Observation

/// Owns the clocks, settings and weather. Every change is saved straight away.
@MainActor @Observable
final class ClockStore {
    private(set) var settings: AppSettings
    private var weather: [UUID: WeatherReading] = [:]

    @ObservationIgnored private let settingsService: SettingsService
    @ObservationIgnored private let weatherService: WeatherService
    @ObservationIgnored private var weatherBusy = false
    @ObservationIgnored private var lastWeatherAttempt: Date?

    init(settingsService: SettingsService = SettingsService(), weatherService: WeatherService = OpenMeteoClient.shared) {
        self.settingsService = settingsService
        self.weatherService = weatherService
        var settings = settingsService.load()
        settings.clocks.removeAll { TimeZone(identifier: $0.timeZoneId) == nil }
        self.settings = settings
    }

    var clocks: [ClockConfig] { settings.clocks }

    func display(for clock: ClockConfig, at now: Date) -> ClockDisplay {
        ClockDisplay(
            clock: clock,
            zone: TimeZone(identifier: clock.timeZoneId) ?? .gmt,
            reading: weather[clock.id],
            unit: settings.temperatureUnit,
            now: now)
    }

    // Clocks

    func add(_ clock: ClockConfig) {
        guard TimeZone(identifier: clock.timeZoneId) != nil else { return }
        settings.clocks.append(clock)
        save()
        Task { await refreshWeather(force: true) }
    }

    func rename(_ id: UUID, to name: String) {
        let name = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty, let index = index(of: id) else { return }
        settings.clocks[index].city = name
        save()
    }

    func setLocation(_ id: UUID, to place: PlaceResult) {
        guard let index = index(of: id) else { return }
        settings.clocks[index].lat = place.lat
        settings.clocks[index].lon = place.lon
        settings.clocks[index].place = place.displayName
        weather[id] = nil
        save()
        Task { await refreshWeather(force: true) }
    }

    func remove(_ id: UUID) {
        settings.clocks.removeAll { $0.id == id }
        weather[id] = nil
        save()
    }

    func remove(atOffsets offsets: IndexSet) {
        for index in offsets { weather[settings.clocks[index].id] = nil }
        settings.clocks.remove(atOffsets: offsets)
        save()
    }

    func move(fromOffsets source: IndexSet, toOffset destination: Int) {
        settings.clocks.move(fromOffsets: source, toOffset: destination)
        save()
    }

    func moveBy(_ id: UUID, _ delta: Int) {
        guard let from = index(of: id) else { return }
        let to = from + delta
        guard settings.clocks.indices.contains(to) else { return }
        settings.clocks.swapAt(from, to)
        save()
    }

    func canMove(_ id: UUID, _ delta: Int) -> Bool {
        guard let from = index(of: id) else { return false }
        return settings.clocks.indices.contains(from + delta)
    }

    // Preferences

    func setUnit(_ unit: TemperatureUnit) {
        settings.temperatureUnit = unit
        save()
    }

    func setTheme(_ theme: AppTheme?) {
        settings.theme = theme
        ThemeManager.apply(theme)
        save()
    }

    // Weather

    /// Fetches weather for every clock with a location. Without `force`, does nothing if it was tried
    /// in the last minute, so coming back to the app repeatedly doesn't hammer the service.
    func refreshWeather(force: Bool = false) async {
        if weatherBusy { return }
        if !force, let last = lastWeatherAttempt, Date.now.timeIntervalSince(last) < 60 { return }

        let located = clocks.filter(\.hasLocation)
        if located.isEmpty { return }

        weatherBusy = true
        lastWeatherAttempt = .now
        defer { weatherBusy = false }
        do {
            let results = try await weatherService.current(for: located.map { ($0.lat!, $0.lon!) })
            let now = Date.now
            for (clock, result) in zip(located, results) {
                weather[clock.id] = WeatherReading(weather: result, at: now)
            }
        } catch {
            // Keep showing the last known weather; it greys out, then clears, as it ages.
        }
    }

    private func index(of id: UUID) -> Int? { settings.clocks.firstIndex { $0.id == id } }

    private func save() { settingsService.save(settings) }
}
