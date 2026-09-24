# iPhone app guide

The iPhone app lives in [`ios/`](../ios). It's a native SwiftUI app that does what the Windows app does, laid out for a phone. It shares no code with the Windows app. It follows the same behaviour, uses the same Open-Meteo services and matches the same colours.

## Using the app

It opens with the same six clocks as the Windows app. Each card shows the same things: label, weather icon, time, temperature, place, date, and offset from you.

| To… | Do this |
| --- | --- |
| Add a clock | Tap **+**, type a city and pick a result. The time zone and weather location are set for you. The label fills in with the city name, and you can change it. For places the search can't find, tap **Choose time zone manually** |
| Rename, set location, move or remove a clock | Touch and hold the card |
| Remove a clock | Swipe the card left |
| Reorder clocks | Tap **Edit** and drag the handles, or touch and hold a card and choose **Move up** / **Move down** |
| Refresh the weather now | Pull down on the list |
| Change °C / °F or the theme, or open About | Tap **⋯** |

Times follow the iPhone's 12-hour or 24-hour setting (Settings → General → Date & Time). Weather refreshes every 15 minutes while the app is open, whenever you come back to it, and when you pull to refresh. Stale weather greys out after 20 minutes and clears after 2 hours, as on Windows.

The temperature unit starts as °F if the iPhone's region uses US measurements, and °C otherwise. The theme starts as **Use iPhone setting**.

**Always on top** is Windows-only and isn't in the iPhone app.

**Sync with a Microsoft account** is Windows-only for now. The iPhone app keeps its clocks on the phone and doesn't sign in. See [Adding sync to the iPhone app](#adding-sync-to-the-iphone-app).

## Building and running

You need a Mac with Xcode 16 or later (the project uses folder-synced groups). The app targets iOS 17 and later, iPhone only.

```bash
open ios/WorldClock.xcodeproj           # then choose an iPhone simulator and press Run

# Or from the command line
xcodebuild test -project ios/WorldClock.xcodeproj -scheme WorldClock \
  -destination 'platform=iOS Simulator,name=iPhone 17'
```

If `xcodebuild` says it needs Xcode rather than the Command Line Tools, run `sudo xcode-select -s /Applications/Xcode.app`, or put `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` in front of the command.

To run the app on a real iPhone, open the project in Xcode, choose your team under **Signing & Capabilities**, and change the bundle ID (`com.brendonford.WorldClock`) if it's already taken.

The project uses folder-synced groups, so any file you add under `ios/WorldClock/` or `ios/WorldClockTests/` is included automatically. There are no package dependencies.

## How it's built

| File | Purpose | Windows equivalent |
| --- | --- | --- |
| `WorldClockApp.swift` | Entry point. Creates the `ClockStore` | `App.xaml` |
| `ContentView.swift` | Clock list, **+** and **⋯**, card menu, rename prompt, weather timer | `MainWindow` |
| `ClockCard.swift` | One card | Card template in `MainWindow.xaml` |
| `ClockStore.swift` | Owns settings, clocks and weather. Saves every change | Logic in `MainWindow.xaml.cs` |
| `ClockDisplay.swift` | Works out the card text (time, date, offset, temperature, icon, staleness) for one instant | `CityClock` |
| `AddClockView.swift` | Add clock (city search or manual time zone), and Set location | `AddClockWindow` |
| `AboutView.swift` | Version, links, Open-Meteo credit, licence | `AboutWindow` |
| `OpenMeteo.swift` | Weather and place search, behind `WeatherService` / `PlaceSearch` | `OpenMeteo.cs` |
| `SettingsService.swift` | Loads and saves `clocks.json`. Holds the default clocks | `SettingsService.cs` |
| `ThemeManager.swift` | Applies Light / Dark / Use iPhone setting to every window | `ThemeManager.cs` |
| `Models.swift` | `ClockConfig`, `AppSettings`, `PlaceResult`, `CurrentWeather` | `Models.cs` |
| `Assets.xcassets` | Colours (light and dark) and the app icon (light, dark and tinted) | `Themes/*.xaml` |

Differences from the Windows app:

- **Time zones are IANA IDs** (`Pacific/Auckland`), which is what iOS and Open-Meteo use natively. The Windows app stores Windows IDs (`New Zealand Standard Time`).
- **The clock redraws once a minute**, on the minute, using `TimelineView(.everyMinute)`, instead of polling every 250 ms.
- **Weather icons are SF Symbols** in the theme colour, instead of emoji.

### Settings file

Settings are saved to `Application Support/WorldClock/clocks.json` in the app's sandbox. The file uses the same format and key names as the Windows app (version 1), with IANA time zone IDs and without the Windows-only `alwaysOnTop` and `locationLookupDone`. As on Windows, a file written by a newer version is loaded but never overwritten.

### Adding sync to the iPhone app

The Windows app syncs through the user's OneDrive (see [Microsoft sign-in and sync](developer-guide.md#microsoft-sign-in-and-sync)). The iPhone app could join in by reading and writing the same file, but three things are needed:

1. **Sign-in:** Microsoft's iOS library (MSAL for iOS/macOS), plus an iOS redirect URI (`msauth.<bundle ID>://auth`) added to the same Entra app registration.
2. **The same sync rules:** If-Match on the eTag, newest `updatedAt` wins, a backup of the copy that's replaced, and the first-sign-in choice. The Windows rules and their tests are in `Sync/SyncService.cs` and `tests/WorldClock.Tests/SyncServiceTests.cs`.
3. **Time zone ID conversion:** the synced file would carry Windows IDs from the Windows app and IANA IDs from the iPhone app. One side has to convert. The simplest option is to store IANA IDs in the synced copy, because .NET converts both ways with `TimeZoneInfo.TryConvertIanaIdToWindowsId` / `TryConvertWindowsIdToIanaId`.

To reset the app, delete it from the iPhone or simulator.

### Colours

Colours come only from the colour sets in `Assets.xcassets`, which have the same light and dark values as `Themes/Light.xaml` and `Themes/Dark.xaml`. Use them through the generated symbols (`.textPrimary`, `.cardBackground`, `.linkText` and so on), never as literal colours. If you add a colour on one platform, add it to the other as well.

### App icon

The icon is a clock face over a globe, drawn by `scripts/make-app-icon.swift` in light, dark and tinted versions for the matching home screen styles. To change it, edit the colours or shapes in the script and run it again:

```bash
swift scripts/make-app-icon.swift ios/WorldClock/Assets.xcassets/AppIcon.appiconset
```

### Tests

`ios/WorldClockTests` uses Swift Testing. It covers offset and temperature text, weather icons, staleness, Open-Meteo parsing, settings load and save (including newer-version files), and the store's reorder, rename and weather matching. The tests don't use the network.

## Versioning

The iPhone app's version is `MARKETING_VERSION` in `ios/WorldClock.xcodeproj/project.pbxproj`, and it's always the same as the Windows app's version. `scripts/bump-version.ps1` updates both, so don't edit either one by hand. On a Mac, run the script with PowerShell 7 (`brew install powershell`):

```bash
pwsh -File scripts/bump-version.ps1 minor -Notes "What changed" -Commit
```

## Known limitations

- There's no home screen widget yet.
- No sign-in or sync yet (Windows only).
- Open-Meteo's free service is non-commercial only. Selling the app on the App Store counts as commercial use (see [Licensing of weather data](developer-guide.md#licensing-of-weather-data)).
