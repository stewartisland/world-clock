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

## How it works

### Components

```
App.xaml ──opens──▶ MainWindow
                     ├── ObservableCollection<CityClock>  ──bound to──▶ ItemsControl (UniformGrid of cards)
                     ├── DispatcherTimer (250 ms)          ──calls──▶  CityClock.Update(utcNow)
                     ├── clocks.json  ◀──load / save──▶   ClockConfig[]
                     └── AddClockWindow ──returns──▶ ClockConfig
```

| Type | File | Responsibility |
| --- | --- | --- |
| `MainWindow` | `MainWindow.xaml.cs` | Owns the clock collection, timer, persistence, drag-and-drop and menu commands |
| `CityClock` | `MainWindow.xaml.cs` | View model for one card. Holds a `TimeZoneInfo` and exposes formatted `Time`, `Date`, `Offset`, `DayNight` and `IsDragging` |
| `ClockConfig` | `MainWindow.xaml.cs` | `record(City, TimeZoneId)`, the saved shape of one clock |
| `AddClockWindow` | `AddClockWindow.xaml.cs` | Modal dialog. Filters `TimeZoneInfo.GetSystemTimeZones()` and returns a `ClockConfig` in `Result` |

### Time calculation

`CityClock.Update` takes one `DateTime.UtcNow` per tick, so all cards show the same instant. It then:

- converts it with `TimeZoneInfo.ConvertTimeFromUtc`, which applies daylight saving rules
- gets the UTC offset from `zone.GetUtcOffset(utcNow)`
- works out "from you" as that offset minus `TimeZoneInfo.Local.GetUtcOffset(utcNow)`

The timer runs every 250 ms rather than once a minute, so the display changes within a quarter-second of the minute rolling over, with no alignment logic needed. `Update` raises `PropertyChanged(null)` to refresh every binding on the card at once.

Time zones use **Windows IDs** (such as `"Eastern Standard Time"`) rather than IANA IDs (such as `"America/New_York"`), because the dialog lists `GetSystemTimeZones()` and those return Windows IDs.

### Persistence

- Path: `%APPDATA%\WorldClock\clocks.json` (`MainWindow.SettingsPath`)
- **Load** (constructor): deserialise `ClockConfig[]`. If the file is missing or unreadable, use `MainWindow.Defaults`. Each entry goes through `CityClock.TryCreate`, which silently drops unknown time zone IDs.
- **Save**: runs on every `CollectionChanged` (add, remove, move) and again when a drag finishes. Errors writing the file are ignored so a read-only profile can't crash the app.

### Drag-and-drop reordering

Standard WPF `DragDrop`, with handlers on each card's `Border` (`x:Name="Card"`):

1. `PreviewMouseLeftButtonDown` records the start point, unless the click was on a button (`IsInsideButton` walks up the visual tree). This stops a click on ✕ from starting a drag.
2. `MouseMove` starts `DoDragDrop` with the `CityClock` as data once the pointer moves past the system drag threshold, and sets `IsDragging = true`. A `DataTrigger` then dims the card and outlines it in blue.
3. `DragOver` on any other card calls `_clocks.Move(from, to)`, so the grid reorders live. After the move, the card under the pointer is the dragged one, so the order doesn't flicker back and forth.
4. When the drag finishes, `IsDragging` is cleared and the config is saved.

### Layout

`ColumnCount` is a dependency property on `MainWindow`. It's recalculated on `SizeChanged` as `max(1, ActualWidth / 240)`, and the `UniformGrid` columns are bound to it.

## Common changes

| Change | Where |
| --- | --- |
| Default clocks for new installs | `MainWindow.Defaults` |
| Time format (for example 24-hour: `"HH:mm"`) | `CityClock.Update`, the `Time = local.ToString(...)` line |
| Date format | `CityClock.Update`, the `Date = ...` line |
| Day/night hours | `CityClock.Update`, the `DayNight = ...` line |
| Card width / column breakpoint | the `240` in the `SizeChanged` handler in the `MainWindow` constructor |
| Colours and fonts | `MainWindow.xaml` and `AddClockWindow.xaml` (hex values inline; the main palette is `#15171C` background, `#1F232B` cards, `#2C313B` borders, `#8A909C` muted text) |
| Label suggested in the Add dialog | `AddClockWindow.SuggestLabel` |

## Known limitations

- **Always on top** isn't saved.
- There's no rename. To change a label, remove the clock and add it again, or edit `clocks.json`.
- Emoji icons render in monochrome in WPF.
- No automated tests.
