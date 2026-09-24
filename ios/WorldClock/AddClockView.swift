import SwiftUI

/// Adds a clock by searching for a city (which sets both location and time zone), or by picking a
/// time zone manually. In set-location mode it only picks a place for an existing clock.
struct AddClockView: View {
    enum Mode {
        case add
        case setLocation(city: String)
    }

    struct Result {
        /// The clock to add (add mode).
        var clock: ClockConfig?
        /// The chosen place (set-location mode).
        var place: PlaceResult?
    }

    let mode: Mode
    var placeSearch: PlaceSearch = OpenMeteoClient.shared
    var onDone: (Result) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var query = ""
    @State private var places: [PlaceResult] = []
    @State private var status = ""
    @State private var manualZones = false
    @State private var selection: Selection?
    @State private var label = ""
    // Whether the label was typed by the user, so selecting a result only fills it in when it hasn't been.
    @State private var labelEdited = false

    private enum Selection: Hashable {
        case place(PlaceResult)
        case zone(String)
    }

    init(mode: Mode, placeSearch: PlaceSearch = OpenMeteoClient.shared, onDone: @escaping (Result) -> Void) {
        self.mode = mode
        self.placeSearch = placeSearch
        self.onDone = onDone
        if case .setLocation(let city) = mode {
            _query = State(initialValue: city)
        }
    }

    private var setLocationOnly: Bool {
        if case .setLocation = mode { true } else { false }
    }

    var body: some View {
        NavigationStack {
            Form {
                if !setLocationOnly {
                    Section("Label") {
                        TextField("Label", text: Binding(get: { label }, set: {
                            label = $0
                            labelEdited = !$0.isEmpty
                        }))
                    }
                }

                Section {
                    TextField(manualZones ? "City, country or UTC offset" : "City", text: $query)
                        .autocorrectionDisabled()
                        .textInputAutocapitalization(.words)
                        .submitLabel(.search)
                } header: {
                    Text(manualZones ? "Search time zones" : "Search for a city")
                } footer: {
                    if !setLocationOnly {
                        Button(manualZones ? "Search for a city instead" : "Choose time zone manually") {
                            manualZones.toggle()
                            selection = nil
                            status = ""
                        }
                        .font(.footnote)
                        .foregroundStyle(.linkText)
                    }
                }

                Section {
                    if manualZones {
                        ForEach(TimeZoneOption.matching(query)) { zone in
                            row(title: zone.city, subtitle: "\(zone.offset) · \(zone.id)", selection: .zone(zone.id), suggestedLabel: zone.city)
                        }
                    } else {
                        ForEach(places) { place in
                            row(title: place.displayName, subtitle: nil, selection: .place(place), suggestedLabel: place.name)
                        }
                    }
                } footer: {
                    if !status.isEmpty { Text(status) }
                }
            }
            .navigationTitle(setLocationOnly ? "Set location" : "Add clock")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(setLocationOnly ? "Set" : "Add", action: confirm)
                        .disabled(!canConfirm)
                }
            }
            .task(id: "\(manualZones) \(query)") {
                guard !manualZones else { return }
                try? await Task.sleep(for: .milliseconds(300))
                guard !Task.isCancelled else { return }
                await runPlaceSearch()
            }
        }
    }

    private func row(title: String, subtitle: String?, selection value: Selection, suggestedLabel: String) -> some View {
        Button {
            selection = value
            if !labelEdited { label = suggestedLabel }
        } label: {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).foregroundStyle(.textPrimary)
                    if let subtitle {
                        Text(subtitle).font(.caption).foregroundStyle(.textMuted)
                    }
                }
                Spacer()
                if selection == value {
                    Image(systemName: "checkmark").foregroundStyle(.tint)
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private func runPlaceSearch() async {
        let text = query.trimmingCharacters(in: .whitespaces)
        guard text.count >= 2 else {
            places = []
            status = ""
            return
        }

        status = "Searching…"
        do {
            let found = try await placeSearch.search(text)
            guard !Task.isCancelled else { return }
            // A new clock needs a time zone we can use; setting a location doesn't.
            places = found.filter { setLocationOnly || $0.timeZoneId != nil }
            status = places.isEmpty
                ? (setLocationOnly ? "No places found. Try another spelling." : "No places found. Try another spelling, or choose a time zone manually.")
                : ""
            if let first = places.first {
                selection = .place(first)
                if !labelEdited { label = first.name }
            }
        } catch {
            guard !Task.isCancelled else { return }
            places = []
            status = setLocationOnly
                ? "Can't reach place search. Check your connection and try again."
                : "Can't reach place search. Check your connection, or choose a time zone manually."
        }
    }

    private var canConfirm: Bool {
        selection != nil && (setLocationOnly || !label.trimmingCharacters(in: .whitespaces).isEmpty)
    }

    private func confirm() {
        let name = label.trimmingCharacters(in: .whitespaces)
        switch selection {
        case .place(let place) where setLocationOnly:
            onDone(Result(place: place))
        case .place(let place):
            guard let zone = place.timeZoneId else { return }
            onDone(Result(clock: ClockConfig(city: name, timeZoneId: zone, lat: place.lat, lon: place.lon, place: place.displayName)))
        case .zone(let zone):
            onDone(Result(clock: ClockConfig(city: name, timeZoneId: zone)))
        case nil:
            return
        }
        dismiss()
    }
}

/// One entry in the manual time zone list.
struct TimeZoneOption: Identifiable {
    let id: String
    /// "Pacific/Port_Moresby" -> "Port Moresby"
    let city: String
    let offset: String

    private static let all: [TimeZoneOption] = TimeZone.knownTimeZoneIdentifiers.compactMap { id in
        guard let zone = TimeZone(identifier: id) else { return nil }
        let city = (id.split(separator: "/").last.map(String.init) ?? id).replacingOccurrences(of: "_", with: " ")
        return TimeZoneOption(id: id, city: city, offset: "UTC\(ClockDisplay.formatSpan(zone.secondsFromGMT()))")
    }

    static func matching(_ query: String) -> [TimeZoneOption] {
        let terms = query.split(separator: " ").map(String.init)
        return all.filter { option in
            terms.allSatisfy { term in
                option.id.localizedCaseInsensitiveContains(term)
                    || option.city.localizedCaseInsensitiveContains(term)
                    || option.offset.localizedCaseInsensitiveContains(term)
            }
        }
    }
}
