import UIKit

/// Applies Light, Dark or "Use iPhone setting" (nil) to every window, including sheets and alerts.
/// Setting the window's style is more reliable than SwiftUI's preferredColorScheme, which doesn't
/// always switch back when it's set to nil.
enum ThemeManager {
    @MainActor
    static func apply(_ theme: AppTheme?) {
        let style: UIUserInterfaceStyle = switch theme {
        case .light: .light
        case .dark: .dark
        case nil: .unspecified
        }
        for case let scene as UIWindowScene in UIApplication.shared.connectedScenes {
            for window in scene.windows {
                window.overrideUserInterfaceStyle = style
            }
        }
    }
}
