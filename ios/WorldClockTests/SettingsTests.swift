import Foundation
import Testing
@testable import WorldClock

struct SettingsTests {
    let url = FileManager.default.temporaryDirectory
        .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        .appending(path: "clocks.json")

    @Test func missingFileGivesDefaults() {
        let settings = SettingsService(url: url, locale: Locale(identifier: "en_NZ")).load()
        #expect(settings.clocks.map(\.city) == ["New Zealand", "Croatia", "UK", "New York", "Dallas", "Seattle"])
        #expect(settings.temperatureUnit == .celsius)
        #expect(settings.theme == nil)
    }

    @Test func unitDefaultsToFahrenheitInTheUS() {
        #expect(SettingsService(url: url, locale: Locale(identifier: "en_US")).load().temperatureUnit == .fahrenheit)
    }

    @Test func defaultTimeZonesAreKnown() {
        for clock in SettingsService.defaults {
            #expect(TimeZone(identifier: clock.timeZoneId) != nil, "\(clock.timeZoneId)")
        }
    }

    @Test func savesAndLoads() throws {
        let service = SettingsService(url: url)
        var settings = service.load()
        settings.temperatureUnit = .fahrenheit
        settings.theme = .dark
        settings.clocks = [ClockConfig(city: "Tokyo", timeZoneId: "Asia/Tokyo", lat: 35.6895, lon: 139.6917, place: "Tokyo, Japan"),
                           ClockConfig(city: "UTC", timeZoneId: "GMT")]
        service.save(settings)

        let loaded = SettingsService(url: url).load()
        #expect(loaded.temperatureUnit == .fahrenheit)
        #expect(loaded.theme == .dark)
        #expect(loaded.clocks.map(\.city) == ["Tokyo", "UTC"])
        #expect(loaded.clocks[0].place == "Tokyo, Japan")
        #expect(loaded.clocks[1].hasLocation == false)
        #expect(loaded.updatedAt != nil)

        // Same key names as the desktop app's clocks.json.
        let json = try String(contentsOf: url, encoding: .utf8)
        for key in [#""version" : 1"#, #""temperatureUnit" : "Fahrenheit""#, #""theme" : "Dark""#, #""TimeZoneId" : "Asia\/Tokyo""#] {
            #expect(json.contains(key), "\(key)")
        }
    }

    @Test func fileFromNewerVersionIsLeftAlone() throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        let original = #"{"version":2,"temperatureUnit":"Celsius","clocks":[{"City":"Tokyo","TimeZoneId":"Asia/Tokyo"}]}"#
        try Data(original.utf8).write(to: url)

        let service = SettingsService(url: url)
        let settings = service.load()
        #expect(settings.clocks.map(\.city) == ["Tokyo"])
        #expect(!service.canSave)

        service.save(settings)
        #expect(try String(contentsOf: url, encoding: .utf8) == original)
    }

    @Test func unreadableFileGivesDefaults() throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("not json".utf8).write(to: url)
        #expect(SettingsService(url: url).load().clocks.count == 6)
    }
}
