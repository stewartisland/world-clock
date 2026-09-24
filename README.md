# World Clock

A small Windows desktop app that shows the current time and weather in several cities at once. It's built with WPF on .NET 9.

There's also a native **iPhone app** in [`ios/`](ios), built with SwiftUI, which does the same things laid out for a phone. See the [iPhone app guide](docs/ios-guide.md).

It opens with six clocks: New Zealand, Croatia, UK, New York, Dallas and Seattle. You can add, remove, rename and reorder clocks, and your layout is kept between launches.

## Features

- A card for each clock showing the time, current temperature, a weather icon, the place the weather is for, the day and date, the UTC offset, and how many hours it is from your own time
- A tidy header with **+ Add clock** and a **⋯** menu for everything else: °C / °F, Light / Dark / Use Windows setting, Always on top, and About
- **+ Add clock**: type a city name and the time zone is chosen for you. There's also a manual time zone list for places the search can't find
- Right-click a card to **Rename…**, **Set location…** (which place the weather is for), move it or remove it
- Drag cards to reorder them
- Daylight saving is handled automatically using Windows' own time zone data
- Cards rearrange into columns as you resize the window
- Units start from your Windows region and the theme follows Windows (including live changes) until you pick your own. All choices are remembered
- **About** shows the version and links to the blog, the source code and Open-Meteo

## Requirements

- Windows 10 or 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) to run the app
- .NET 9 SDK to build it
- An internet connection for weather and city search. The clocks work without one

## Quick start

```powershell
dotnet run --project WorldClock.csproj
```

To build a single `.exe` you can copy anywhere or pin to the taskbar:

```powershell
dotnet publish WorldClock.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The app is written to `publish\WorldClock.exe`. Close any running copy before you publish, because a running copy locks the file and the publish will fail.

## Documentation

- [User guide](docs/user-guide.md): how to use the app, where settings are stored and how to reset them
- [Developer guide](docs/developer-guide.md): project layout, how the app works and how to change it
- [iPhone app guide](docs/ios-guide.md): using, building and changing the iPhone app
- [Changelog](CHANGELOG.md): what changed in each version
- Feature definitions: [temperature](docs/features/temperature.md) (built), [sign in and sync](docs/features/cloud-sign-in.md) (planned)

## Project layout

| File | Purpose |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Application entry point, which opens the main window |
| `MainWindow.xaml` / `.cs` | Clock grid, header, drag-and-drop, card menu, weather refresh |
| `CityClock.cs` | View model for one card: time, temperature, weather icon |
| `AddClockWindow.xaml` / `.cs` | The "Add clock" dialog (city search or manual time zone). Also used for "Set location…" |
| `RenameWindow.xaml` / `.cs` | The "Rename clock" dialog |
| `AboutWindow.xaml` / `.cs` | The About window: version, links, Open-Meteo credit, licence |
| `ThemeManager.cs`, `Themes/Dark.xaml`, `Themes/Light.xaml` | Light/dark themes: colour palettes, swapping them at runtime, and matching the title bar |
| `SettingsService.cs` | Loads and saves `clocks.json`, including migrating older files. Holds the default clocks |
| `OpenMeteo.cs` | Weather and place search from Open-Meteo, behind `IWeatherService` / `IPlaceSearch` |
| `Models.cs` | `ClockConfig`, `AppSettings`, `PlaceResult`, `CurrentWeather` |
| `WorldClock.csproj` | Project file (targets `net9.0-windows` with WPF). Holds the app version |
| `scripts/bump-version.ps1` | Raises the version (major or minor) of both apps and updates `CHANGELOG.md` |
| `ios/` | The iPhone app (SwiftUI, Xcode project `ios/WorldClock.xcodeproj`). See the [iPhone app guide](docs/ios-guide.md) |

## License

The code is released under the [MIT License](LICENSE). You're free to use, copy, modify and distribute it, including commercially, as long as you keep the copyright notice and licence text.

Weather and place data come from [Open-Meteo.com](https://open-meteo.com/) under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Open-Meteo's free service is for **non-commercial use only**. If you use this app or a fork commercially, you need an [Open-Meteo API subscription](https://open-meteo.com/en/pricing), or you need to replace `OpenMeteoClient` with a provider whose terms allow commercial use.
