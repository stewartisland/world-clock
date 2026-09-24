import Foundation

protocol WeatherService: Sendable {
    /// Current weather for each location, in the same order. Throws on network or API failure.
    func current(for locations: [(lat: Double, lon: Double)]) async throws -> [CurrentWeather]
}

protocol PlaceSearch: Sendable {
    /// Places matching the name. Throws on network or API failure.
    func search(_ name: String) async throws -> [PlaceResult]
}

/// Weather and place search from open-meteo.com. Free, no API key, non-commercial use only, CC BY 4.0.
struct OpenMeteoClient: WeatherService, PlaceSearch {
    static let shared = OpenMeteoClient()

    private let session: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 10
        config.httpAdditionalHeaders = [
            "User-Agent": "WorldClock-iOS/\(AppInfo.version) (+https://github.com/stewartisland/world-clock)",
        ]
        return URLSession(configuration: config)
    }()

    func current(for locations: [(lat: Double, lon: Double)]) async throws -> [CurrentWeather] {
        if locations.isEmpty { return [] }

        let lats = locations.map { format($0.lat) }.joined(separator: ",")
        let lons = locations.map { format($0.lon) }.joined(separator: ",")
        let url = URL(string: "https://api.open-meteo.com/v1/forecast?latitude=\(lats)&longitude=\(lons)"
            + "&current=temperature_2m,weather_code,is_day&temperature_unit=celsius")!
        return try Self.parseWeather(try await get(url))
    }

    func search(_ name: String) async throws -> [PlaceResult] {
        var components = URLComponents(string: "https://geocoding-api.open-meteo.com/v1/search")!
        components.queryItems = [
            URLQueryItem(name: "count", value: "10"),
            URLQueryItem(name: "language", value: "en"),
            URLQueryItem(name: "format", value: "json"),
            URLQueryItem(name: "name", value: name),
        ]
        return try Self.parsePlaces(try await get(components.url!))
    }

    private func get(_ url: URL) async throws -> Data {
        let (data, response) = try await session.data(from: url)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw URLError(.badServerResponse) }
        return data
    }

    private func format(_ value: Double) -> String { String(format: "%.4f", value) }

    // Parsing, kept separate so it can be tested without the network.

    private struct Forecast: Decodable {
        struct Current: Decodable {
            let temperature_2m: Double
            let weather_code: Int
            let is_day: Int
        }
        let current: Current
    }

    static func parseWeather(_ data: Data) throws -> [CurrentWeather] {
        // One location returns an object; several return an array in request order.
        let decoder = JSONDecoder()
        let items = try (try? decoder.decode([Forecast].self, from: data)) ?? [decoder.decode(Forecast.self, from: data)]
        return items.map {
            CurrentWeather(temperatureC: $0.current.temperature_2m, weatherCode: $0.current.weather_code, isDay: $0.current.is_day == 1)
        }
    }

    private struct GeocodingResponse: Decodable {
        struct Place: Decodable {
            let name: String?
            let admin1: String?
            let country: String?
            let latitude: Double
            let longitude: Double
            let timezone: String?
        }
        let results: [Place]?
    }

    static func parsePlaces(_ data: Data) throws -> [PlaceResult] {
        let response = try JSONDecoder().decode(GeocodingResponse.self, from: data)
        return (response.results ?? []).map { place in
            let name = place.name ?? ""
            var parts: [String] = []
            for part in [name, place.admin1, place.country].compactMap({ $0 }) where !part.isEmpty && !parts.contains(part) {
                parts.append(part)
            }
            let zone = place.timezone.flatMap { TimeZone(identifier: $0) }?.identifier
            return PlaceResult(
                name: name,
                displayName: parts.joined(separator: ", "),
                lat: (place.latitude * 10_000).rounded() / 10_000,
                lon: (place.longitude * 10_000).rounded() / 10_000,
                timeZoneId: zone)
        }
    }
}
