import SwiftUI

/// Version, links, Open-Meteo credit and licence.
struct AboutView: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("World Clock")
                            .font(.title2.weight(.semibold))
                            .foregroundStyle(.textPrimary)
                        Text("Version \(AppInfo.version)")
                            .foregroundStyle(.textMuted)
                        Text("The time and weather in the places you care about, at a glance.")
                            .foregroundStyle(.textSecondary)
                            .padding(.top, 4)
                    }
                    .padding(.vertical, 4)
                }

                Section("Links") {
                    Link("Blog: brendonford.com/world-clock", destination: URL(string: "https://www.brendonford.com/world-clock")!)
                    Link("Source code on GitHub", destination: URL(string: "https://github.com/stewartisland/world-clock")!)
                }
                .foregroundStyle(.linkText)

                Section("Weather data") {
                    Text("Weather and place search are provided by [Open-Meteo.com](https://open-meteo.com/), a free, open-source weather API. Thank you!")
                    Text("Data licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Open-Meteo's free service is for non-commercial use.")
                }
                .foregroundStyle(.textSecondary)
                .tint(.linkText)

                Section("License") {
                    Text("Released under the [MIT License](https://github.com/stewartisland/world-clock/blob/main/LICENSE).")
                }
                .foregroundStyle(.textSecondary)
                .tint(.linkText)
            }
            .navigationTitle("About")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }
}
