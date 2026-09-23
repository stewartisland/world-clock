# Developer guide

## Building and running

```powershell
# Run from source
dotnet run --project WorldClock.csproj

# Debug build
dotnet build WorldClock.csproj

# Single-file exe in .\publish (needs the .NET 9 Desktop Runtime on the target machine)
dotnet publish WorldClock.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Always name `WorldClock.csproj` in these commands. The folder also contains `world-clock.sln`, and `dotnet` refuses to guess between the two.

To build an exe that runs on machines without .NET installed, use `--self-contained true`. The file is much larger, about 150 MB.

`bin/`, `obj/` and `publish/` are git-ignored.

The only NuGet dependencies are Microsoft's sign-in libraries (MSAL): `Microsoft.Identity.Client`, `.Broker` (the Windows account broker, which includes the native `msalruntime.dll`) and `.Extensions.Msal` (the encrypted token cache). They take the single-file exe from about 230 KB to about 5 MB. Everything else, including HTTP and JSON, comes from the framework.

### Tests

```powershell
dotnet test tests/WorldClock.Tests/WorldClock.Tests.csproj
```

xUnit tests cover `SettingsService` (defaults, migration, newer-version protection, round-trip) and `SyncService` (every first-sign-in and conflict path, against an in-memory OneDrive). Tests use temporary folders, never your real `%APPDATA%`. The root `WorldClock.csproj` excludes `tests/**` so the app build doesn't compile them.

## How it works

### Components

```
App.xaml ──opens──▶ MainWindow
                     ├── SettingsService ◀──load / save──▶ %APPDATA%\WorldClock\clocks.json (AppSettings)
                     ├── ObservableCollection<CityClock>  ──bound to──▶ ItemsControl (UniformGrid of cards)
                     ├── DispatcherTimer 250 ms   ──▶ CityClock.Update(utcNow, unit)
                     ├── DispatcherTimer 15 min   ──▶ IWeatherService.GetCurrentAsync ──▶ CityClock.SetWeather
                     ├── AddClockWindow  ──▶ IPlaceSearch.SearchAsync   (returns ClockConfig or PlaceResult)
                     ├── RenameWindow
                     ├── AboutWindow
                     ├── SyncService ──▶ MicrosoftAuth (MSAL/WAM) + OneDriveStore (Graph approot)
                     └── ThemeManager ──swaps──▶ Themes/Dark.xaml | Themes/Light.xaml
                                        OpenMeteoClient implements IWeatherService + IPlaceSearch
```

| Type | File | Responsibility |
| --- | --- | --- |
| `MainWindow` | `MainWindow.xaml.cs` | Owns the clock collection and timers. Handles the header and ⋯ menu, card commands, drag-and-drop, weather refresh and the one-off location lookup |
| `CityClock` | `CityClock.cs` | View model for one card. Holds a `TimeZoneInfo`, an optional location and the last weather. Exposes `Time`, `Date`, `Offset`, `Icon`, `Temperature`, `Place`, `HasLocation`, `IsWeatherStale`, `IsDragging` |
| `SettingsService` | `SettingsService.cs` | Reads and writes `clocks.json`, migrates the pre-v1 bare-array format, and holds `Defaults` |
| `OpenMeteoClient` | `OpenMeteo.cs` | Open-Meteo forecast and geocoding APIs. `OpenMeteoClient.Shared` is used everywhere |
| `AddClockWindow` | `AddClockWindow.xaml.cs` | Add mode: city search (or manual time zone list), returns `ResultClock`. Set-location mode (`new AddClockWindow(setLocationFor: label)`): returns `ResultPlace` |
| `RenameWindow` | `RenameWindow.xaml.cs` | Text prompt, returns `NewName` |
| `AboutWindow` | `AboutWindow.xaml.cs` | Version (from the assembly), links to the blog and GitHub, Open-Meteo credit, licence |
| `ClockConfig`, `AppSettings`, `PlaceResult`, `CurrentWeather` | `Models.cs` | Data shapes |
| `SyncService`, `MicrosoftAuth`, `OneDriveStore` | `Sync/` | Microsoft sign-in and OneDrive sync (see [Microsoft sign-in and sync](#microsoft-sign-in-and-sync)) |

### Time calculation

`CityClock.Update` takes one `DateTime.UtcNow` per tick, so all cards show the same instant. It then:

- converts it with `TimeZoneInfo.ConvertTimeFromUtc`, which applies daylight saving rules
- gets the UTC offset from `zone.GetUtcOffset(utcNow)`
- works out "from you" as that offset minus `TimeZoneInfo.Local.GetUtcOffset(utcNow)`

The timer runs every 250 ms rather than once a minute, so the display changes within a quarter-second of the minute rolling over, with no alignment logic needed. `Update` raises `PropertyChanged(null)` to refresh every binding on the card at once.

Time zones are stored as **Windows IDs** (such as `"Eastern Standard Time"`). City search returns IANA IDs (such as `"America/Chicago"`), and these are converted with `TimeZoneInfo.TryConvertIanaIdToWindowsId`. Results that can't be converted aren't offered when adding a clock.

### Weather

- **Service:** `https://api.open-meteo.com/v1/forecast` with `current=temperature_2m,weather_code,is_day`. Every clock that has a location goes into **one request**, using comma-separated latitudes and longitudes. With one location the API returns an object, and with several it returns an array. `GetCurrentAsync` handles both.
- **When:** on startup, every 15 minutes, after adding a clock, and after **Set location…**. A `_weatherBusy` flag stops requests overlapping.
- **Units:** always fetched in °C and converted in `CityClock.Update`, so switching °C/°F is instant and needs no request.
- **Ageing:** weather older than 20 minutes shows greyed out (`IsWeatherStale`). Older than 2 hours, it's dropped and the card shows "–".
- **Failures:** network, timeout and parse errors are caught and ignored. The card keeps its last value and ages it as above. There are never error dialogs.
- **Icons:** `CityClock.WeatherIcon` maps WMO weather codes plus `is_day` to emoji. Without weather, the icon falls back to ☀️/🌙 from local time (6am–6pm counts as day).
- **HTTP:** one shared `HttpClient` with a 10-second timeout and the User-Agent `WorldClock/<version> (+https://github.com/stewartisland/world-clock)`, using `AppInfo.Version`.

### Place search

- `https://geocoding-api.open-meteo.com/v1/search?name=…&count=10&language=en`
- The Add dialog waits 300 ms after the last keystroke and needs at least 2 characters. Each new search cancels the previous one (`CancellationTokenSource`).
- `PlaceResult.DisplayName` is "name, region, country" with duplicate parts removed. Coordinates are rounded to 4 decimal places.

### Persistence

- **Path:** `%APPDATA%\WorldClock\clocks.json`
- **Format (version 1):** an `AppSettings` object with `version`, `updatedAt`, `deviceName`, `temperatureUnit`, `locationLookupDone` and `clocks` (a list of `ClockConfig`). Null clock fields (`Lat`, `Lon`, `Place`) aren't written. The same file is what [sync](#microsoft-sign-in-and-sync) saves to OneDrive.
- **Migration:** a bare JSON array (the pre-v1 format) is read as clocks with `locationLookupDone = false` and a region-based unit. It's rewritten as v1 on the next save.
- **Newer files:** if `version` is higher than `AppSettings.CurrentVersion`, `SettingsService.CanSave` is false and the file is never overwritten.
- **Save:** runs on every add, remove or move, after a drag, rename, set location, unit, theme or Always on top change, and after the location lookup. Each local save also tells `SyncService`, which uploads 2 s later when signed in. Errors writing the file are ignored so a read-only profile can't crash the app.
- **Missing or unreadable file:** the app uses `SettingsService.Defaults`, which come with locations, and a region-based unit. Unknown time zone IDs are dropped by `CityClock.TryCreate`.

### One-off location lookup

`MainWindow.LookUpMissingLocationsAsync` runs on `Loaded` when `locationLookupDone` is false. For each clock without a location:

1. If its label and time zone match a default (for example "UK" + `GMT Standard Time`), it uses that default's location.
2. Otherwise it searches the label and keeps the top result **only if** that result's time zone matches the clock's. For example, "UK" on its own finds a village in Russia, which is rejected.

It sets `locationLookupDone = true` unless a network error stops it, in which case it retries on the next launch.

### Drag-and-drop reordering

Standard WPF `DragDrop`, with handlers on each card's `Border` (`x:Name="Card"`):

1. `PreviewMouseLeftButtonDown` records the start point, unless the click was on a button (`IsInsideButton` walks up the visual tree). This stops clicks on ✕ or **Set location…** from starting a drag.
2. `MouseMove` starts `DoDragDrop` with the `CityClock` as data once the pointer moves past the system drag threshold, and sets `IsDragging = true`. A `DataTrigger` then dims the card and outlines it in blue.
3. `DragOver` on any other card calls `_clocks.Move(from, to)`, so the grid reorders live. After the move, the card under the pointer is the dragged one, so the order doesn't flicker back and forth.
4. When the drag finishes, `IsDragging` is cleared and the settings are saved.

### Themes

- All colours are `SolidColorBrush` resources in `Themes/Dark.xaml` and `Themes/Light.xaml`, with the same keys in both (`WindowBackgroundBrush`, `CardBackgroundBrush`, `TextPrimaryBrush`, `TextMutedBrush`, `LinkBrush` and so on). XAML always uses `{DynamicResource …}`, and code uses `SetResourceReference`, never a literal colour, so a theme switch repaints everything.
- `ThemeManager.SetPreference(AppTheme?)` takes `Light`, `Dark`, or `null` for "Use Windows setting". It swaps `Application.Resources.MergedDictionaries` to the right dictionary, and sets each open window's title bar to dark or light with `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`. That works on Windows 10 20H1 and later, and does nothing on older versions.
- With `null`, the theme comes from `ThemeManager.SystemTheme()`, which reads `AppsUseLightTheme` from `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`. `SystemEvents.UserPreferenceChanged` re-applies it live when the Windows setting changes.
- Every window calls `ThemeManager.Attach(this)` in its constructor, so its title bar matches when it opens. **New windows must do the same.**
- Menus (`ContextMenu`, `MenuItem` and the menu separator) are restyled in `App.xaml` to use the theme colours. The template is flat: there are no submenus, checked items show a ✓, and disabled items are used as group labels (Temperature, Theme). Dialog buttons, scrollbars and the ListBox keep the standard Windows look.

### Header and ⋯ menu

The header has only **+ Add clock** and **⋯** (`MoreButton`). `More_Click` opens `MoreMenu` with a custom placement that right-aligns it under the button. `MoreMenu_Opened` sets the ✓ on the current unit, theme and Always on top state each time it opens, so there's no menu state to keep in sync. Unit, theme and Always on top are saved in `clocks.json` (`temperatureUnit`, `theme`, `alwaysOnTop`).

### Layout

`ColumnCount` is a dependency property on `MainWindow`. It's recalculated on `SizeChanged` as `max(1, ActualWidth / 250)`, and the `UniformGrid` columns are bound to it.

## Microsoft sign-in and sync

Design and decisions: [feature definition](features/cloud-sign-in.md). The code is in `Sync/` plus `MainWindow.Sync.cs`.

| Type | Responsibility |
| --- | --- |
| `MicrosoftAuth` (`IAccountService`) | MSAL public client, `consumers` authority (personal accounts only), Windows account broker (WAM). Scope: `Files.ReadWrite.AppFolder`. Token cache in `%LOCALAPPDATA%\WorldClock\msal.cache`, DPAPI-encrypted |
| `OneDriveStore` (`ISettingsStore`) | Graph `me/drive/special/approot:/clocks.json` (shows in OneDrive as `Apps\<app name>`). Download reads the metadata for the `eTag` and then the pre-authenticated download URL. Upload `PUT`s with `If-Match: <eTag>`, or with `conflictBehavior=fail` when creating the file, and `412`/`409` become `SyncConflictException` |
| `SyncService` | When to sync (startup, 2 s after the last local edit, on window focus at most once a minute, **Sync now**, a 60 s retry while offline) and what to do (see below). Keeps `%LOCALAPPDATA%\WorldClock\sync.json`: the last-seen eTag, whether there are local changes to upload, and the last sync time |
| `SyncChoiceWindow` | First sign-in when this PC's clocks differ from the saved ones |
| `MainWindow.Sync.cs` | The ⋯ menu's Sync section, the status text, and applying synced settings without treating them as a local change (`_applyingRemote`) |

**Reconcile rules** (`SyncService.ReconcileAsync`):

1. Nothing saved yet: upload this PC's settings (create-only).
2. Saved by a newer app version: do nothing, and show "Update World Clock to sync".
3. First sign-in: if the clocks match, or this PC still has the untouched defaults, use the saved copy. Otherwise ask. The copy that isn't kept goes to `clocks.backup.json`, and Cancel signs out with nothing changed.
4. Saved copy unchanged since the last sync (same eTag): upload if there are local changes.
5. Saved copy changed: the newer `updatedAt` wins. If this PC's copy loses while it had changes that weren't uploaded, it's backed up first.
6. An upload conflict (someone saved in between) re-runs the rules once.

Synced settings are applied with `SettingsService.Save(isLocalChange: false)`, so they keep the other device's `updatedAt`. Clocks, unit and theme sync. Always on top doesn't.

### Registering the app in Microsoft Entra

Sign-in needs an app registration. Its client ID is public (it's not a secret) and goes in `MicrosoftAuth.ClientId`. Forks should register their own app and either change that constant or set the `WORLDCLOCK_MS_CLIENT_ID` environment variable. With neither set, **Sign in with Microsoft…** is disabled.

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com) and go to **Identity → Applications → App registrations → New registration**.
2. **Name:** `World Clock`. This is shown on the consent screen, and it's the OneDrive folder name (`Apps\World Clock`).
3. **Supported account types:** *Personal Microsoft accounts only*.
4. Leave **Redirect URI** empty and select **Register**.
5. Copy the **Application (client) ID** from the Overview page.
6. **Authentication → Add a platform → Mobile and desktop applications.** Add both redirect URIs:
   - `ms-appx-web://microsoft.aad.brokerplugin/<client ID>` (the Windows account broker)
   - `http://localhost` (the fallback browser sign-in)
7. **API permissions → Add a permission → Microsoft Graph → Delegated permissions → `Files.ReadWrite.AppFolder`**. No admin consent is needed for personal accounts. You can remove the default `User.Read`, because the app doesn't use it.

## Versioning

The version is **Major.Minor** and lives in one place: `<Version>` in `WorldClock.csproj`. The app reads it from the built assembly (`AppInfo.Version`) for the ⋯ menu, the About window and the web requests' User-Agent, so nothing else needs editing.

| Bump | When | Example |
| --- | --- | --- |
| **minor** | New features, improvements and fixes | 1.2 → 1.3 |
| **major** | Big or breaking changes, such as a settings file that older versions can't read (`AppSettings.CurrentVersion` goes up) | 1.9 → 2.0 |

**Every change to the app that you ship to `main` gets a bump.** Several commits that go out together can share one version. Changes that only touch documentation don't need one. To release:

```powershell
# From a PowerShell prompt in the repo root
./scripts/bump-version.ps1 minor -Notes "Added rain radar", "Fixed Dallas weather" -Commit
git push --follow-tags

# Or from cmd / Git Bash (separate notes with ;)
powershell -File scripts/bump-version.ps1 minor -Notes "Added rain radar; Fixed Dallas weather" -Commit
```

The script:

1. raises `<Version>` in `WorldClock.csproj`,
2. adds a dated entry with your notes to the top of [CHANGELOG.md](../CHANGELOG.md),
3. with `-Commit`, commits both files as "Release X.Y" and creates an annotated tag `vX.Y`.

Without `-Commit`, it only edits the two files, so you can include them in your own commit. Then publish the exe as usual.

## Common changes

| Change | Where |
| --- | --- |
| Default clocks for new installs | `SettingsService.Defaults` |
| Time format (for example 24-hour: `"HH:mm"`) | `CityClock.Update`, the `Time = local.ToString(...)` line |
| Date format | `CityClock.Update`, the `Date = ...` line |
| Weather icons | `CityClock.WeatherIcon` |
| How long before weather greys out / is dropped | `CityClock.StaleAfter` / `DiscardAfter` |
| Weather refresh interval | `_weatherTimer` in `MainWindow` |
| Weather or place search provider | Implement `IWeatherService` / `IPlaceSearch` and change the `OpenMeteoClient.Shared` references in `MainWindow` and `AddClockWindow` |
| Card width / column breakpoint | the `250` in the `SizeChanged` handler in the `MainWindow` constructor |
| App version | Run `scripts/bump-version.ps1` (see [Versioning](#versioning)). Don't edit `<Version>` by hand |
| Colours | `Themes/Dark.xaml` and `Themes/Light.xaml`. Add any new key to both |
| Fonts and sizes | Inline in each window's XAML |

## Licensing of weather data

Open-Meteo's free API is **non-commercial only** and needs credit under CC BY 4.0. The credit is the "Weather: Open-Meteo.com" link at the bottom of the main window plus the Weather data section of the About window (`AboutWindow.xaml`). Keep both if you change the layout. See the [temperature feature definition](features/temperature.md#decisions) for why this provider was chosen and what switching would involve.

## Known limitations

- Emoji icons render in monochrome in WPF.
- Automated tests cover settings and sync logic only. The UI, weather and the real Microsoft sign-in are checked by hand.
