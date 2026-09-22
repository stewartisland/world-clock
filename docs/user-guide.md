# User guide

## Reading a clock card

Each card shows:

| Part | Example | Meaning |
| --- | --- | --- |
| Label | **New York** | The name you gave the clock |
| Icon | ☀️ / 🌙 | Day (6:00am–5:59pm local) or night |
| Time | 11:55 PM | Current local time there |
| Date | Monday, 21 Sep | Local day and date. Check this for places that are already in tomorrow or still in yesterday |
| Offset | UTC-4 · -16h from you | Its offset from UTC, and how far ahead (+) or behind (-) your computer's time zone it is |

Times update automatically, including when daylight saving starts or ends in either place.

## Adding a clock

1. Click **+ Add clock** in the top-right corner.
2. Type in the search box. You can search by:
   - city: `Tokyo`, `Sydney`
   - country or region: `India`, `Hawaii`
   - UTC offset: `+05:30`, `-10`
   - several words, all of which must match: `pacific us`
3. Choose a time zone with the mouse, or with the **↑ / ↓** keys while you're still in the search box.
4. The label fills in with the first city in the time zone's name. You can change it to anything, such as "Mum" or "Sydney office".
5. Click **Add** or press **Enter**. Double-clicking a time zone also adds it.

New clocks are added at the end of the list.

> You can only choose time zones that Windows knows about. If a city isn't listed, search for a nearby city in the same time zone and rename the label.

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

Cards arrange themselves into as many columns as fit, one column for roughly every 240 pixels of width. A narrow window gives a single column, and a scrollbar appears if the clocks don't all fit.

## Where your clocks are saved

Your clock list and order are saved automatically after every change to:

```
%APPDATA%\WorldClock\clocks.json
```

To open the folder, paste `%APPDATA%\WorldClock` into the File Explorer address bar.

The file is only created after your first change. Until then the app shows the six default clocks.

### Resetting to the defaults

Close the app, delete `clocks.json`, and reopen the app.

### Editing the file by hand

The file is plain JSON:

```json
[
  { "City": "New Zealand", "TimeZoneId": "New Zealand Standard Time" },
  { "City": "Croatia", "TimeZoneId": "Central European Standard Time" }
]
```

`TimeZoneId` must be a Windows time zone ID. To list them all, run `tzutil /l` in a terminal: the IDs are the lines that don't start with `(UTC`. Any entry with an ID the app doesn't recognise is skipped. If the file can't be read at all, the app falls back to the defaults.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| The app won't start and asks for .NET | Install the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) |
| A clock disappeared after editing `clocks.json` | Its `TimeZoneId` is misspelled or unknown. Check it against `tzutil /l` |
| The hours "from you" value looks wrong | It's calculated from your computer's time zone. Check it in Windows Settings → Time & language → Date & time |
