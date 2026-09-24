import SwiftUI

/// One clock: label, weather, time, temperature, place, date and offset.
struct ClockCard: View {
    let city: String
    let display: ClockDisplay
    var onSetLocation: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack {
                Text(city)
                    .font(.headline)
                    .foregroundStyle(.textSecondary)
                Spacer()
                Image(systemName: display.symbol)
                    .symbolRenderingMode(.hierarchical)
                    .foregroundStyle(.textSecondary)
                    .font(.title3)
                    .accessibilityHidden(true)
            }

            HStack(alignment: .firstTextBaseline) {
                Text(display.time)
                    .font(.system(size: 44, weight: .light))
                    .monospacedDigit()
                    .foregroundStyle(.textPrimary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.6)
                Spacer()
                if display.hasLocation {
                    Text(display.temperature)
                        .font(.system(size: 28, weight: .light))
                        .foregroundStyle(.textPrimary)
                        .opacity(display.isWeatherStale ? 0.45 : 1)
                        .accessibilityLabel(display.isWeatherStale
                            ? "\(display.temperature), couldn't update the weather recently"
                            : display.temperature)
                } else {
                    Button("Set location…", action: onSetLocation)
                        .font(.footnote)
                        .foregroundStyle(.linkText)
                        .buttonStyle(.borderless)
                }
            }

            if display.hasLocation, let place = display.place {
                Text(place)
                    .font(.caption)
                    .foregroundStyle(.textMuted)
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .padding(.bottom, 2)
            }
            Text(display.date)
                .font(.subheadline)
                .foregroundStyle(.textMuted)
            Text(display.offset)
                .font(.subheadline)
                .foregroundStyle(.textMuted)
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.cardBackground, in: RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).strokeBorder(.cardBorder))
        .accessibilityElement(children: .combine)
    }
}
