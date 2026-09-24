import Foundation
import Testing
@testable import WorldClock

struct ClockDisplayTests {
    // 15 Jan 2026 12:00 UTC: New Zealand is on daylight time (+13), New York on standard time (-5).
    let now = Date(timeIntervalSince1970: 1_768_478_400)
    let auckland = TimeZone(identifier: "Pacific/Auckland")!
    let newYork = TimeZone(identifier: "America/New_York")!
    let locale = Locale(identifier: "en_NZ")
    let located = ClockConfig(city: "New Zealand", timeZoneId: "Pacific/Auckland", lat: -36.8485, lon: 174.7633, place: "Auckland, New Zealand")

    func display(_ clock: ClockConfig? = nil, zone: TimeZone? = nil, reading: WeatherReading? = nil, unit: TemperatureUnit = .celsius) -> ClockDisplay {
        ClockDisplay(clock: clock ?? located, zone: zone ?? auckland, reading: reading, unit: unit, now: now, localZone: newYork, locale: locale)
    }

    @Test(arguments: [(0, "+0"), (43_200, "+12"), (-14_400, "-4"), (19_800, "+5:30"), (-12_600, "-3:30"), (20_700, "+5:45")])
    func formatSpan(seconds: Int, expected: String) {
        #expect(ClockDisplay.formatSpan(seconds) == expected)
    }

    @Test func offsetAndDifference() {
        #expect(display().offset == "UTC+13 · +18h from you")
        #expect(display(zone: newYork).offset == "UTC-5 · same as you")
    }

    @Test func timeAndDateAreInTheClocksZone() {
        let d = display()
        #expect(d.time.hasPrefix("1:00"))
        #expect(d.date.contains("Friday"))
        #expect(d.date.contains("16"))
    }

    @Test func temperatureConversionAndRounding() {
        #expect(ClockDisplay.temperature(20, unit: .celsius) == "20°")
        #expect(ClockDisplay.temperature(20, unit: .fahrenheit) == "68°")
        #expect(ClockDisplay.temperature(-0.3, unit: .celsius) == "0°")
        #expect(ClockDisplay.temperature(21.6, unit: .celsius) == "22°")
    }

    @Test(arguments: [
        (0, true, "sun.max.fill"), (0, false, "moon.stars.fill"), (2, true, "cloud.sun.fill"), (3, false, "cloud.fill"),
        (45, true, "cloud.fog.fill"), (61, true, "cloud.rain.fill"), (81, true, "cloud.rain.fill"),
        (73, true, "cloud.snow.fill"), (86, true, "cloud.snow.fill"), (95, true, "cloud.bolt.rain.fill"), (500, false, "moon.stars.fill"),
    ])
    func weatherSymbols(code: Int, isDay: Bool, expected: String) {
        #expect(ClockDisplay.weatherSymbol(code: code, isDay: isDay) == expected)
    }

    @Test func freshWeatherIsShown() {
        let reading = WeatherReading(weather: CurrentWeather(temperatureC: 18.4, weatherCode: 61, isDay: true), at: now.addingTimeInterval(-60))
        let d = display(reading: reading)
        #expect(d.temperature == "18°")
        #expect(d.symbol == "cloud.rain.fill")
        #expect(!d.isWeatherStale)
    }

    @Test func oldWeatherGreysOutThenClears() {
        let weather = CurrentWeather(temperatureC: 18, weatherCode: 3, isDay: true)
        #expect(display(reading: WeatherReading(weather: weather, at: now.addingTimeInterval(-30 * 60))).isWeatherStale)

        let cleared = display(reading: WeatherReading(weather: weather, at: now.addingTimeInterval(-3 * 60 * 60)))
        #expect(cleared.temperature == "–")
        #expect(cleared.symbol == "moon.stars.fill") // 1 am in Auckland
        #expect(!cleared.isWeatherStale)
    }

    @Test func clockWithoutLocationShowsNoTemperature() {
        let d = display(ClockConfig(city: "Somewhere", timeZoneId: "America/New_York"), zone: newYork)
        #expect(!d.hasLocation)
        #expect(d.temperature == "")
        #expect(d.symbol == "sun.max.fill") // 7 am in New York
    }
}
