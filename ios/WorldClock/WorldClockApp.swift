import SwiftUI

@main
struct WorldClockApp: App {
    @State private var store = ClockStore()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
        }
    }
}
