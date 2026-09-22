# World Clock

A small Windows desktop app that shows the current time in several cities at once. It's built with WPF on .NET 9.

It opens with six clocks: New Zealand, Croatia, UK, New York, Dallas and Seattle. You can add, remove and reorder clocks, and your layout is kept between launches.

## Features

- A card for each clock showing the time, day and date, UTC offset, how many hours it is from your own time, and a day/night icon (☀️ 6am–6pm, 🌙 otherwise)
- Daylight saving is handled automatically using Windows' own time zone data
- Drag cards to reorder them, or right-click and choose **Move earlier** / **Move later**
- **+ Add clock** lets you search every Windows time zone by city, country or UTC offset, and give the clock your own label
- Remove a clock with the **✕** that appears when you hover over its card, or from the right-click menu
- Cards rearrange into columns as you resize the window
- **Always on top** keeps the window above other windows

## Requirements

- Windows 10 or 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) to run the app
- .NET 9 SDK to build it

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

## Project layout

| File | Purpose |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Application entry point, which opens the main window |
| `MainWindow.xaml` / `.cs` | Clock grid, drag-and-drop, add and remove, saving settings, and the `CityClock` model |
| `AddClockWindow.xaml` / `.cs` | The "Add clock" dialog with time zone search |
| `WorldClock.csproj` | Project file (targets `net9.0-windows` with WPF) |
