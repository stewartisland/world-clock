# About World Clock

World Clock is a small Windows desktop app that shows the current time and weather for a list of places you choose, one card per place. It's written in C# with WPF on .NET 9, has no backend, and its only dependencies are Microsoft's sign-in libraries. It's MIT-licensed.

This page explains why the project exists, how it was built, and the reasoning behind its main design decisions. For how to use the app, see the [user guide](user-guide.md). For how the code works, see the [developer guide](developer-guide.md).

## Why it exists

The need was simple: see at a glance what time it is for people spread across New Zealand, Croatia, the UK, New York, Dallas and Seattle, before messaging or calling them.

The existing options didn't quite fit:

- **Windows' built-in extra clocks** (Settings → Time & language → Date & time → Additional clocks) allow only two extra time zones, and they're shown only in the taskbar flyout.
- **Websites and phone apps** work, but they aren't something you can leave open on a second monitor or keep above other windows.
- Many time zone tools **don't show the date alongside the time**, which is what trips you up when one place is already into tomorrow.

So the goal was a lightweight native window that shows every place at once, with the day, the offset from you, and later the weather, and that you can reorder and keep on top.

## How it was built

The app was built over two days (22–23 September 2026) by Brendon Ford, pair-programming with [Claude Code](https://claude.com/claude-code), Anthropic's AI coding agent. Commits carry a `Co-Authored-By: Claude` trailer to reflect that.

The workflow was the same each time: describe what's wanted in plain language, have Claude Code write and build the change, check it running, and adjust. Larger features got a written feature definition first, with the open questions put to Brendon as explicit decisions and recorded in the spec before any code was written:

- [Temperature feature definition](features/temperature.md) (built)
- [Sign-in and sync feature definition](features/cloud-sign-in.md) (Microsoft built, Google not started)

Changes were checked by building with no warnings, launching the app against real settings, and driving the UI with Windows UI Automation to capture screenshots and click through menus. The sync logic, where a mistake could lose data, also has xUnit tests. They run every first-sign-in and conflict path against an in-memory OneDrive.

## Timeline

| Version | Date | What changed |
| --- | --- | --- |
| 1.0 | 22 Sep 2026 | Six clocks. Add, remove and drag-to-reorder. Always on top. Settings saved locally. MIT licence |
| 1.1 | 23 Sep 2026 | Temperature and weather on each card (Open-Meteo). Add a clock by searching for a city. Rename and set location. About window. Light and dark themes |
| 1.2 | 23 Sep 2026 | Header reduced to **+ Add clock** and a **⋯** menu. Theme can follow Windows. Always on top remembered. Major.Minor versioning with a bump script |

The full list is in the [changelog](../CHANGELOG.md).

## Design decisions

Each decision below records what was chosen, why, and what it costs. They're the choices most likely to be questioned or revisited.

### Native WPF, not Electron or a web app

**Chosen:** WPF on .NET 9, published as a single framework-dependent `WorldClock.exe` of about 5 MB, most of which is the Windows sign-in component.

**Why:** the app is Windows-only by nature: a small always-on window next to your other work. WPF gives native windowing (always on top, dark title bars through DWM), low memory use and a small download.

**Cost:** Windows only. The target machine needs the .NET 9 Desktop Runtime, unless you publish self-contained (about 150 MB). WPF also can't draw colour emoji, so the weather icons are monochrome.

### Windows time zone IDs, and daylight saving left to Windows

**Chosen:** clocks store Windows time zone IDs (`"New Zealand Standard Time"`). All conversion goes through `TimeZoneInfo`, from one `DateTime.UtcNow` per tick, so every card shows the same instant.

**Why:** Windows keeps its time zone rules up to date through Windows Update, so daylight saving changes need no app release. City search returns IANA IDs (`"America/Chicago"`), and those are converted to Windows IDs with `TimeZoneInfo.TryConvertIanaIdToWindowsId`.

**Cost:** the settings file isn't portable to non-Windows platforms without converting the IDs. That's acceptable for a Windows-only app.

### Refresh every 250 ms rather than on the minute

**Chosen:** a 250 ms `DispatcherTimer` that re-formats every card.

**Why:** there's no minute-alignment logic to get wrong, and the display changes within a quarter-second of the minute rolling over. The work per tick is trivial.

**Cost:** the app wakes more often than strictly necessary. That's unnoticeable on a desktop PC.

### Open-Meteo for weather and place search

**Chosen:** [Open-Meteo](https://open-meteo.com/) for both the current weather and city search (geocoding).

**Why:** it's free and **needs no API key**. That matters for a public repo: there's no secret to leak, hide, or make every fork register for. One request fetches every clock's weather using comma-separated coordinates, so six clocks refreshed every 15 minutes come to under 100 calls a day against a 10,000-call free limit. MET Norway (free, including commercial use) and key-based services like OpenWeatherMap were considered and rejected: MET Norway has stricter rules on caching and identifying your app, and offers no place search, while key-based services put a secret in a public repo.

**Cost:** Open-Meteo's free tier is **non-commercial only**, and its data is CC BY 4.0, so the app credits it in the footer and the About window. Commercial use needs Open-Meteo's paid plan or a different provider. Weather and search sit behind `IWeatherService` and `IPlaceSearch`, so swapping providers is contained.

Temperatures are always fetched in °C and converted locally, so switching between °C and °F is instant and needs no network call.

### Wrong locations must be visible, never silent

This principle shaped the most code.

A time zone isn't a location: "Central Standard Time" covers Dallas to Winnipeg. To show weather, each clock needs coordinates. Three ways of getting them were considered:

| Option | How you'd find out it was wrong |
| --- | --- |
| Guess a city from the time zone | **Silently.** US zones name no city, so Dallas might quietly get Chicago's weather |
| Look up the clock's label | **Silently.** "UK" resolves to a village in Russia, and "New Zealand" to the middle of the country |
| **Search for a city when adding a clock** (chosen) | **Visibly.** The card shows the matched place, e.g. "Dallas, Texas, United States" |

Existing clocks from 1.0 needed a location too, so the upgrade runs a one-off lookup. Clocks still named as they were by default take the default city. Others are searched by label, **and a result is kept only if its time zone matches the clock's**. "UK" returns Uk, Irkutsk Oblast (`Asia/Irkutsk`), which is rejected, and that card shows **Set location…** instead of the wrong weather.

The same thinking applies to failures. When the weather service can't be reached, a temperature greys out after 20 minutes and disappears after 2 hours. It never keeps showing an old value as if it were current, and it never shows an error dialog.

### No backend: sync through your own OneDrive

**Chosen:** settings live in `%APPDATA%\WorldClock\clocks.json`. The file is versioned (`"version": 1`), and older files (a bare JSON array) are migrated on load. A file written by a newer version is never overwritten. To sync between PCs, you sign in with a personal Microsoft account, and the same file is saved to an app-private folder in **your own** OneDrive (Microsoft Graph `approot`, scope `Files.ReadWrite.AppFolder`). Google Drive (`drive.appdata`) is designed but not built ([spec](features/cloud-sign-in.md)).

**Why:** a hobby app shouldn't need a server. The developer never holds user data, and there's nothing to host or pay for. The narrowest scope Microsoft offers means the app can't see any of your other files.

**How conflicts are handled:** every upload uses `If-Match` on the eTag the app last saw, so another PC's change is never overwritten blindly. When both sides changed, the newer `updatedAt` wins, and if this PC's copy loses while it still had changes that weren't uploaded, it's written to `clocks.backup.json` first. On first sign-in with different clocks on each side, you choose, and the set you don't keep is backed up. This is the one area with automated tests, because it's the one place a bug could quietly lose someone's data.

**Cost:** no sharing between users and no web version. Those were ruled out deliberately in the spec. Sign-in also needs an Entra app registration, and forks need their own.

### Themes as swappable resource dictionaries

**Chosen:** every colour is a `SolidColorBrush` in `Themes/Dark.xaml` or `Themes/Light.xaml`, referenced with `{DynamicResource}`. `ThemeManager` swaps the dictionary at runtime and sets each window's title bar with `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`. With **Use Windows setting**, the app reads `AppsUseLightTheme` and follows changes live through `SystemEvents.UserPreferenceChanged`.

**Why:** an instant switch with no restart, and title bars that match the theme rather than staying white in dark mode.

**Cost:** there must be no literal colours anywhere, which is a convention to maintain. Menus had to be restyled by hand, because WPF's default menu template doesn't follow app colours. Dialog buttons and scrollbars still use the standard Windows look.

### A header with one main action

In 1.1 the header had five controls of equal weight, in four different styles: a two-part °C/°F switch, a checkbox, a text button and two icon buttons. A UX review sorted them by how often they're used. Only **Add clock** is used regularly. Units, theme and Always on top are set once, and About is rarely opened.

**Chosen (1.2):** **+ Add clock** plus a **⋯** menu, the standard Windows 11 pattern. The theme toggle became a three-way choice (Light / Dark / Use Windows setting), partly because an icon toggle is ambiguous: does ☀ mean "it's light now" or "switch to light"?

**Cost:** changing units takes one more click.

### Major.Minor versioning from one source

**Chosen:** the version exists only in `<Version>` in `WorldClock.csproj`, and the app reads it from the assembly. `scripts/bump-version.ps1` raises the major or minor number, adds a changelog entry, and can commit and tag `vX.Y`.

**Why:** one source of truth can't drift, and a script makes "bump on every release" cheap enough to actually happen.

## What's next

- **Sign-in and sync** ([spec](features/cloud-sign-in.md)): Microsoft accounts are built. Google is next.
- **Smaller improvements** listed in the specs: more weather detail on hover, a card ⋯ menu with undo for removals, colour weather icons.
- **More automated tests**, beyond settings and sync.

## Links

- Source: [github.com/stewartisland/world-clock](https://github.com/stewartisland/world-clock)
- Blog: [brendonford.com/world-clock](https://www.brendonford.com/world-clock)
- Weather data: [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0, non-commercial free tier)
- Licence: [MIT](../LICENSE)
