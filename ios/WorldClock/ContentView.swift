import SwiftUI

/// The clock list, with + (add a clock) and ⋯ (everything else) in the navigation bar.
struct ContentView: View {
    @Environment(ClockStore.self) private var store
    @Environment(\.scenePhase) private var scenePhase

    @State private var sheet: Sheet?
    @State private var renaming: ClockConfig?
    @State private var newName = ""

    private enum Sheet: Identifiable {
        case add
        case setLocation(ClockConfig)
        case about

        var id: String {
            switch self {
            case .add: "add"
            case .setLocation(let clock): "location-\(clock.id)"
            case .about: "about"
            }
        }
    }

    var body: some View {
        NavigationStack {
            TimelineView(.everyMinute) { context in
                List {
                    ForEach(store.clocks) { clock in
                        ClockCard(
                            city: clock.city,
                            display: store.display(for: clock, at: context.date),
                            onSetLocation: { sheet = .setLocation(clock) })
                            .contextMenu { cardMenu(for: clock) }
                            .listRowSeparator(.hidden)
                            .listRowBackground(Color.clear)
                            .listRowInsets(EdgeInsets(top: 6, leading: 16, bottom: 6, trailing: 16))
                    }
                    .onDelete { store.remove(atOffsets: $0) }
                    .onMove { store.move(fromOffsets: $0, toOffset: $1) }

                    if !store.clocks.isEmpty {
                        Link("Weather: Open-Meteo.com", destination: URL(string: "https://open-meteo.com/")!)
                            .font(.caption)
                            .foregroundStyle(.linkText)
                            .frame(maxWidth: .infinity)
                            .listRowSeparator(.hidden)
                            .listRowBackground(Color.clear)
                    }
                }
                .listStyle(.plain)
                .scrollContentBackground(.hidden)
                .background(.appBackground)
                .refreshable { await store.refreshWeather(force: true) }
                .overlay {
                    if store.clocks.isEmpty {
                        ContentUnavailableView {
                            Label("No clocks", systemImage: "clock")
                        } description: {
                            Text("Tap + to add a clock for a city.")
                        }
                    }
                }
            }
            .navigationTitle("World Clock")
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    if !store.clocks.isEmpty { EditButton() }
                }
                ToolbarItemGroup(placement: .topBarTrailing) {
                    Button { sheet = .add } label: { Label("Add clock", systemImage: "plus") }
                    moreMenu
                }
            }
        }
        .sheet(item: $sheet) { sheet in
            switch sheet {
            case .add:
                AddClockView(mode: .add) { result in
                    if let clock = result.clock { store.add(clock) }
                }
            case .setLocation(let clock):
                AddClockView(mode: .setLocation(city: clock.city)) { result in
                    if let place = result.place { store.setLocation(clock.id, to: place) }
                }
            case .about:
                AboutView()
            }
        }
        .alert("Rename clock", isPresented: Binding(get: { renaming != nil }, set: { if !$0 { renaming = nil } })) {
            TextField("Name", text: $newName)
            Button("Cancel", role: .cancel) {}
            Button("Rename") {
                if let clock = renaming { store.rename(clock.id, to: newName) }
            }
            .disabled(newName.trimmingCharacters(in: .whitespaces).isEmpty)
        }
        .onAppear { ThemeManager.apply(store.settings.theme) }
        .task {
            // Weather refreshes every 15 minutes while the app is open, and when it comes back to the front.
            while !Task.isCancelled {
                await store.refreshWeather()
                try? await Task.sleep(for: .seconds(15 * 60))
            }
        }
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { Task { await store.refreshWeather() } }
        }
    }

    @ViewBuilder
    private func cardMenu(for clock: ClockConfig) -> some View {
        Button { newName = clock.city; renaming = clock } label: { Label("Rename…", systemImage: "pencil") }
        Button { sheet = .setLocation(clock) } label: { Label("Set location…", systemImage: "location") }
        Divider()
        Button { store.moveBy(clock.id, -1) } label: { Label("Move up", systemImage: "arrow.up") }
            .disabled(!store.canMove(clock.id, -1))
        Button { store.moveBy(clock.id, 1) } label: { Label("Move down", systemImage: "arrow.down") }
            .disabled(!store.canMove(clock.id, 1))
        Divider()
        Button(role: .destructive) { store.remove(clock.id) } label: { Label("Remove", systemImage: "trash") }
    }

    private var moreMenu: some View {
        Menu {
            Picker("Temperature", selection: Binding(get: { store.settings.temperatureUnit }, set: { store.setUnit($0) })) {
                Text("Celsius (°C)").tag(TemperatureUnit.celsius)
                Text("Fahrenheit (°F)").tag(TemperatureUnit.fahrenheit)
            }
            .pickerStyle(.inline)

            Picker("Theme", selection: Binding(get: { ThemeChoice(store.settings.theme) }, set: { store.setTheme($0.theme) })) {
                ForEach(ThemeChoice.allCases) { Text($0.title).tag($0) }
            }
            .pickerStyle(.inline)

            Section {
                Text("Version \(AppInfo.version)")
                Button { sheet = .about } label: { Label("About World Clock", systemImage: "info.circle") }
            }
        } label: {
            Label("More", systemImage: "ellipsis.circle")
        }
    }
}

/// The theme choices as the menu shows them. `system` is stored as a nil theme.
private enum ThemeChoice: CaseIterable, Identifiable {
    case light, dark, system

    init(_ theme: AppTheme?) {
        self = switch theme {
        case .light: .light
        case .dark: .dark
        case nil: .system
        }
    }

    var id: Self { self }

    var theme: AppTheme? {
        switch self {
        case .light: .light
        case .dark: .dark
        case .system: nil
        }
    }

    var title: String {
        switch self {
        case .light: "Light"
        case .dark: "Dark"
        case .system: "Use iPhone setting"
        }
    }
}
