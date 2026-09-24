import Foundation
import Testing
@testable import WorldClock

struct OpenMeteoTests {
    @Test func parsesOneLocation() throws {
        let json = #"{"latitude":-36.85,"current":{"time":"2026-01-15T12:00","temperature_2m":18.4,"weather_code":2,"is_day":1}}"#
        let weather = try OpenMeteoClient.parseWeather(Data(json.utf8))
        #expect(weather == [CurrentWeather(temperatureC: 18.4, weatherCode: 2, isDay: true)])
    }

    @Test func parsesSeveralLocationsInOrder() throws {
        let json = #"""
        [{"current":{"temperature_2m":18.4,"weather_code":2,"is_day":1}},
         {"current":{"temperature_2m":-3.0,"weather_code":71,"is_day":0}}]
        """#
        let weather = try OpenMeteoClient.parseWeather(Data(json.utf8))
        #expect(weather.map(\.temperatureC) == [18.4, -3.0])
        #expect(weather[1] == CurrentWeather(temperatureC: -3, weatherCode: 71, isDay: false))
    }

    @Test func badWeatherResponseThrows() {
        #expect(throws: (any Error).self) { try OpenMeteoClient.parseWeather(Data(#"{"error":true}"#.utf8)) }
    }

    @Test func parsesPlaces() throws {
        let json = #"""
        {"results":[
          {"name":"Tokyo","latitude":35.6895123,"longitude":139.6917123,"country":"Japan","admin1":"Tokyo","timezone":"Asia/Tokyo"},
          {"name":"Nowhere","latitude":1,"longitude":2,"timezone":"Not/AZone"}
        ]}
        """#
        let places = try OpenMeteoClient.parsePlaces(Data(json.utf8))
        #expect(places.count == 2)
        #expect(places[0].displayName == "Tokyo, Japan")
        #expect(places[0].lat == 35.6895)
        #expect(places[0].lon == 139.6917)
        #expect(places[0].timeZoneId == "Asia/Tokyo")
        #expect(places[1].displayName == "Nowhere")
        #expect(places[1].timeZoneId == nil)
    }

    @Test func noResultsIsEmpty() throws {
        #expect(try OpenMeteoClient.parsePlaces(Data(#"{"generationtime_ms":0.5}"#.utf8)).isEmpty)
    }
}
