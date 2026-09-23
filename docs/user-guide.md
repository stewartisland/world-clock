# User guide

## Reading a clock card

Each card shows:

| Part | Example | Meaning |
| --- | --- | --- |
| Label | **New York** | The name you gave the clock |
| Weather icon | ☀️ ⛅ ☁️ 🌫️ 🌧️ ❄️ ⛈️ 🌙 | Current conditions there. A clear night shows 🌙 |
| Time | 11:55 PM | Current local time there |
| Temperature | 66° | Current temperature, in the unit chosen in the header |
| Place | New York, United States | The place the weather is for |
| Date | Monday, 21 Sep | Local day and date. Check this for places that are already in tomorrow or still in yesterday |
| Offset | UTC-4 · -16h from you | Its offset from UTC, and how far ahead (+) or behind (-) your computer's time zone it is |

Times update automatically, including when daylight saving starts or ends in either place. Weather updates every 15 minutes.

### When weather is missing or grey

| You see | Meaning |
| --- | --- |
| **Set location…** instead of a temperature | The clock doesn't know which place to show weather for. Click it and choose a city |
| A greyed-out temperature | The app couldn't reach the weather service for a while, usually because you're offline. It's showing the last known value |
| **–** | No recent weather (more than 2 hours old, or not loaded yet) |
| ☀️ / 🌙 with no temperature | The weather hasn't loaded, so the icon shows day or night from the local time |

The clocks keep working without an internet connection.

## °C or °F

Use the **°C | °F** switch in the header. It applies to every clock and is remembered. The first time you run the app, it picks the unit for your Windows region (Settings → Time & language → Language & region → Country or region).

## Adding a clock

1. Click **+ Add clock** in the top-right corner.
2. Type a city name, such as `Tokyo` or `Queenstown`. Results appear as you type, for example "Tokyo, Japan".
3. Choose a result with the mouse, or with the **↑ / ↓** keys while you're still in the search box. Picking a city sets its time zone and weather location for you.
4. The label fills in with the city name. You can change it to anything, such as "Mum" or "Sydney office".
5. Click **Add** or press **Enter**. Double-clicking a result also adds it.

New clocks are added at the end of the list.

### Choosing a time zone manually

If the city search can't find a place, or you're offline, click **Choose time zone manually**. You get the list of Windows time zones, which you can search by city, country or UTC offset (for example `+05:30`). A clock added this way has no weather until you use **Set location…**.

## Changing a clock

Right-click a card:

- **Rename…** changes the label only.
- **Set location…** changes which place the weather is for. It doesn't change the label or the time zone. Use it if the place shown under the time is wrong, for example to show Queenstown instead of Auckland.
- **Move earlier** / **Move later** reorders the clock.
- **Remove** deletes the clock.

## Reordering clocks

- **Drag:** press and hold on a card, then drag it. The other cards move out of the way as you go. Let go when it's where you want it.
- **Menu:** right-click a card and choose **Move earlier** or **Move later**.

Cards are ordered left to right, then top to bottom.

## Removing a clock

- Hover over a card and click the **✕** in its top-right corner, or
- right-click the card and choose **Remove**.

Nothing asks you to confirm. To get a clock back, add it again.

## Always on top

Tick **Always on top** to keep the window above other windows, which is handy as a small floating clock. This setting isn't saved and resets each time you open the app.

## Window size

Cards arrange themselves into as many columns as fit, one column for roughly every 250 pixels of width. A narrow window gives a single column, and a scrollbar appears if the clocks don't all fit.

## Where your clocks are saved

Your clocks, their order and the °C/°F choice are saved automatically after every change to:

```
%APPDATA%\WorldClock\clocks.json
```

To open the folder, paste `%APPDATA%\WorldClock` into the File Explorer address bar.

The file is only created after your first change. Until then the app shows the six default clocks.

### Upgrading from an earlier version

Earlier versions saved clocks without locations. The first time you open this version, it finds a location for each clock:

- clocks still named as they were by default (such as "UK") get the default city (London),
- other clocks are looked up by name, and a result is only used if it's in the clock's time zone.

Any clock it can't match shows **Set location…**. If you're offline at the time, it tries again next time you open the app.

### Resetting to the defaults

Close the app, delete `clocks.json`, and reopen the app.

### Editing the file by hand

The file is plain JSON:

```json
{
  "version": 1,
  "temperatureUnit": "Celsius",
  "locationLookupDone": true,
  "clocks": [
    { "City": "New Zealand", "TimeZoneId": "New Zealand Standard Time",
      "Lat": -36.8485, "Lon": 174.7633, "Place": "Auckland, New Zealand" },
    { "City": "Croatia", "TimeZoneId": "Central European Standard Time" }
  ]
}
```

- `TimeZoneId` must be a Windows time zone ID. To list them all, run `tzutil /l` in a terminal: the IDs are the lines that don't start with `(UTC`. Any clock with an ID the app doesn't recognise is skipped.
- `Lat`, `Lon` and `Place` are optional. Without them, the clock shows **Set location…**.
- `temperatureUnit` is `Celsius` or `Fahrenheit`.
- The app also writes `updatedAt` and `deviceName`. You don't need to edit them.

If the file can't be read at all, the app falls back to the defaults.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| The app won't start and asks for .NET | Install the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) |
| No temperatures, or they're all grey | Check your internet connection. Weather comes from open-meteo.com |
| The app started in °F (or °C) and you wanted the other | Click the other unit in the header. It's remembered |
| The weather is for the wrong place | Right-click the card → **Set location…** |
| City search says it can't reach place search | You're offline, or open-meteo.com is unavailable. Use **Choose time zone manually** |
| A clock disappeared after editing `clocks.json` | Its `TimeZoneId` is misspelled or unknown. Check it against `tzutil /l` |
| The hours "from you" value looks wrong | It's calculated from your computer's time zone. Check it in Windows Settings → Time & language → Date & time |

Weather and place data: [Open-Meteo.com](https://open-meteo.com/), CC BY 4.0.
