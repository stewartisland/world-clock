import Foundation
import Testing
@testable import WorldClock

@MainActor
struct ClockStoreTests {
    struct StubWeather: WeatherService {
        var result: Result<[CurrentWeather], Error>
        func current(for locations: [(lat: Double, lon: Double)]) async throws -> [CurrentWeather] {
            try result.get().prefix(locations.count).map { $0 }
        }
    }

    let url = FileManager.default.temporaryDirectory
        .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        .appending(path: "clocks.json")

    func makeStore(weather: Result<[CurrentWeather], Error> = .success([])) -> ClockStore {
        ClockStore(settingsService: SettingsService(url: url), weatherService: StubWeather(result: weather))
    }

    @Test func changesAreSaved() {
        let store = makeStore()
        let first = store.clocks[0].id
        store.rename(first, to: "  Auckland ")
        store.moveBy(first, 1)
        store.remove(store.clocks.last!.id)
        store.setUnit(.fahrenheit)

        let reloaded = makeStore()
        #expect(reloaded.clocks.map(\.city) == ["Croatia", "Auckland", "UK", "New York", "Dallas"])
        #expect(reloaded.settings.temperatureUnit == .fahrenheit)
    }

    @Test func emptyRenameIsIgnored() {
        let store = makeStore()
        store.rename(store.clocks[0].id, to: "   ")
        #expect(store.clocks[0].city == "New Zealand")
    }

    @Test func moveLimits() {
        let store = makeStore()
        #expect(!store.canMove(store.clocks[0].id, -1))
        #expect(store.canMove(store.clocks[0].id, 1))
        #expect(!store.canMove(store.clocks.last!.id, 1))
    }

    @Test func clockWithUnknownTimeZoneIsNotAdded() {
        let store = makeStore()
        store.add(ClockConfig(city: "Nowhere", timeZoneId: "Not/AZone"))
        #expect(store.clocks.count == 6)
    }

    @Test func weatherIsMatchedToClocksInOrder() async {
        let results = (0..<6).map { CurrentWeather(temperatureC: Double($0 * 10), weatherCode: 0, isDay: true) }
        let store = makeStore(weather: .success(results))
        store.setUnit(.celsius)
        await store.refreshWeather(force: true)
        #expect(store.display(for: store.clocks[2], at: .now).temperature == "20°")
    }

    @Test func failedRefreshKeepsNoWeather() async {
        let store = makeStore(weather: .failure(URLError(.notConnectedToInternet)))
        await store.refreshWeather(force: true)
        #expect(store.display(for: store.clocks[0], at: .now).temperature == "–")
    }
}
