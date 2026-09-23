# Feature: Temperature and conditions on each clock

| | |
| --- | --- |
| **Status** | Built 23 Sep 2026: all P0 requirements (W1–W6), plus renaming a clock. P1 items not started. Decisions: see [Decisions](#decisions) |
| **Author** | Brendon |
| **Date** | 23 Sep 2026 |
| **Affects** | Clock card (`MainWindow.xaml`), `ClockConfig`, `AddClockWindow` (becomes a city search), new weather service |
| **Related** | [Sign in to save and sync settings](cloud-sign-in.md). Locations are stored in `clocks.json`, so they sync once that feature ships |

## Summary

Each clock card shows the current temperature and a weather icon for that place, in °C or °F. Data comes from **[Open-Meteo](https://open-meteo.com)**, which is free and needs no API key. To make the weather accurate, each clock gets a real location: adding a clock becomes a search for a city, which sets both the location and the time zone.

## Problem statement

The app answers "what time is it there?" but not "what's it like there?". When you're calling or messaging someone in Dallas or Zagreb, the weather is useful context, and today you have to look it up separately. The app also can't show weather now, because a clock only knows its time zone, and a time zone covers a huge area. "Central Standard Time" spans Dallas to Winnipeg.

## Goals

1. See the current temperature and conditions for every clock at a glance, without opening anything else.
2. Weather is for the right place. A clock whose location is wrong or unknown makes that obvious on the card, and it's never quietly wrong.
3. Temperature in the unit you prefer, switchable in one click.
4. No keys, accounts or cost. Weather never stops the clocks working.

## Non-goals

| Out of scope | Why |
| --- | --- |
| Forecasts, hourly charts, rain radar | This is a clock with context, not a weather app |
| Weather alerts or notifications | Needs background work and region-specific alert sources |
| Commercial use of the weather data | Open-Meteo's free tier is non-commercial only ([D-T1](#decisions)). A commercial build needs their paid plan or a switch to MET Norway |
| Weather for your own location | You can add a clock for your own city |
| Per-clock units | One switch for all clocks ([D-T3](#decisions)) |

## User stories

- As someone keeping in touch across countries, I want to see the temperature and conditions next to each clock, so I know what the day is like there.
- As someone in New Zealand, I want temperatures in °C by default, and a US colleague wants °F, with a switch to change it.
- As someone adding a clock, I want to type a city name and have its time zone chosen for me, so I don't have to know which Windows time zone Dallas is in.
- As someone with existing clocks, I want them to pick up a location automatically, and to be able to fix any that got it wrong.
- As someone whose clock is labelled "New Zealand", I want the card to tell me which place the weather is for, so I can change it to Queenstown if that's who I'm thinking of.
- As a laptop user who's offline, I want the clocks to keep working and the weather to show as unavailable rather than as an error.

## Requirements

### P0: must have

**W1. Weather service**
- Current conditions from Open-Meteo's forecast API, with all clocks fetched in **one request** (it accepts comma-separated lists of latitudes and longitudes):
  `GET https://api.open-meteo.com/v1/forecast?latitude=-36.85,45.81&longitude=174.76,15.98&current=temperature_2m,weather_code,is_day&temperature_unit=celsius`
- Refreshes every **15 minutes**, when a clock is added or its location changes, and when the unit is switched. With 6 clocks that's under 100 calls a day, against a limit of 10,000.
- Hidden behind an `IWeatherService` interface, so MET Norway or a paid provider can be swapped in later without touching the UI.

Acceptance criteria:
- [ ] Given 6 clocks with locations, when the app refreshes, then exactly one request is made to Open-Meteo.
- [ ] Given the network is down, then the clocks keep working, and the weather shows "–" (or the last value, greyed out, if it's less than 2 hours old).
- [ ] No error dialog ever appears because of weather.

**W2. Clock card display** ([D-T4](#decisions))
- The temperature appears on the card next to the time (e.g. "18°"). The unit symbol appears once, in the header toggle.
- A weather icon replaces the current ☀️/🌙 day/night icon. It's chosen from Open-Meteo's `weather_code` plus `is_day`:

| Weather code | Day | Night |
| --- | --- | --- |
| 0 clear | ☀️ | 🌙 |
| 1–2 mainly clear / partly cloudy | ⛅ | ☁️ |
| 3 overcast, 45–48 fog | ☁️ / 🌫️ | ☁️ / 🌫️ |
| 51–67, 80–82 drizzle, rain, showers | 🌧️ | 🌧️ |
| 71–77, 85–86 snow | ❄️ | ❄️ |
| 95–99 thunderstorm | ⛈️ | ⛈️ |

- If weather isn't available yet, the ☀️/🌙 day/night icon worked out from local time is shown, as it is today.
- A muted line on the card shows the matched place, e.g. "Dallas, Texas, US".

Acceptance criteria:
- [ ] Given it's night and clear in Zagreb, then the Croatia card shows 🌙 and the temperature.
- [ ] Given a clock with no location, then the card shows **Set location…** where the temperature would be, and clicking it opens the location search.

**W3. °C / °F switch** ([D-T3](#decisions))
- A **°C | °F** toggle in the header, next to **Always on top**.
- On first run the default comes from the Windows region (`RegionInfo.CurrentRegion.IsMetric`), so °C in New Zealand and °F in the US.
- The choice is saved locally, and it syncs once the sign-in feature's P1-2 (sync more settings) ships.

Acceptance criteria:
- [ ] Given I switch to °F, then every card updates within 2 seconds, and the choice is kept after restarting the app.

**W4. Add a clock by searching for a city** ([D-T2](#decisions))
- The Add dialog's search box searches places using Open-Meteo's geocoding API (`https://geocoding-api.open-meteo.com/v1/search?name=Dallas&count=10`). Results are shown as "Dallas, Texas, United States".
- Picking a result sets the label (editable), the latitude and longitude, and the time zone. The result's IANA time zone (e.g. `America/Chicago`) is converted to a Windows ID with `TimeZoneInfo.TryConvertIanaIdToWindowsId`.
- A **Choose time zone manually** link opens today's time zone list, for places a search can't find. A clock added that way has no location until one is set.
- Searches wait 300 ms after the last keystroke before running, and need at least 2 characters.

Acceptance criteria:
- [ ] Given I type "Dallas" and pick "Dallas, Texas, United States", then a clock is added labelled "Dallas" with time zone `Central Standard Time` and weather for Dallas.
- [ ] Given place search is unavailable (offline), then the dialog says so and offers **Choose time zone manually**.

**W5. Existing clocks: a one-off lookup and a fix**
- On first launch after the upgrade, each clock without a location is looked up once by its label. The top result is kept only if its time zone matches the clock's time zone. Otherwise the clock is left without a location and shows **Set location…**.
- Right-clicking a card offers **Set location…**, which opens the place search. Choosing a place updates the location and matched place, but **not** the label or time zone.

Acceptance criteria:
- [ ] Given a clock labelled "Seattle" (Pacific Standard Time), then after the upgrade it shows Seattle weather and "Seattle, Washington, United States".
- [ ] Given a clock labelled "UK" whose top search result is in a different time zone, then it shows **Set location…** and isn't given the wrong place.

**W6. Credit** (required by CC BY 4.0)
- Small muted text at the bottom of the main window: "Weather: Open-Meteo.com" (linked).
- The README's License section notes that weather data is from Open-Meteo under CC BY 4.0, for **non-commercial use** only.

### P1: nice to have

- **P1-1.** Hover a card for more detail: feels-like temperature, wind, and today's high and low.
- **P1-2.** The time the weather was last updated, shown in the card's tooltip.
- **P1-3.** Colour emoji icons (WPF shows emoji in monochrome), using a small bundled icon set instead.

### P2: future (design for, don't build)

- A commercial-safe provider (MET Norway for weather, plus a commercial-licensed place search) via `IWeatherService` and `IPlaceSearch`.
- A short forecast for the next few hours.

## Data format

`ClockConfig` gains optional fields. This is compatible with the versioned format in the [sign-in spec](cloud-sign-in.md#data-format):

```json
{ "City": "Dallas", "TimeZoneId": "Central Standard Time",
  "Lat": 32.7767, "Lon": -96.797, "Place": "Dallas, Texas, United States" }
```

Clocks without `Lat`/`Lon` are valid and show **Set location…**. Coordinates are stored with at most 4 decimal places, which keeps the file small and matches MET Norway's limit if the provider is ever switched.

## Decisions

| Decided | Still open |
| --- | --- |
| D-T1: Open-Meteo for weather and place search | Nothing blocking. All four decisions are built (23 Sep 2026) |
| D-T2: Pick a place when adding. Existing clocks get a one-off lookup plus **Set location…** | |
| D-T3: One °C/°F switch, default from the Windows region | |
| D-T4: Temperature plus a weather icon that replaces ☀️/🌙 | |

**D-T1. Weather service.** *Decided by Brendon, 23 Sep 2026:* Open-Meteo for both weather and place search, behind an interface so it can be swapped. Why: free, no API key to keep out of a public repo, and one service to integrate. The app is free with no ads, which is the private non-profit use Open-Meteo's terms allow ([open-meteo.com/en/terms](https://open-meteo.com/en/terms), read 23 Sep 2026). Rejected: MET Norway plus Open-Meteo search (more work, and search is still non-commercial), and services that need a key. Rules out: **commercial use**. A SuiteFiles or paid version would need Open-Meteo's paid plan or a switch to MET Norway ([api.met.no terms](https://api.met.no/doc/TermsOfService), read 23 Sep 2026: free for commercial use, CC BY 4.0, strict identification and caching rules).

**D-T2. How clocks get a location.** *Decided by Brendon, 23 Sep 2026:* adding a clock becomes a city search that sets location and time zone, with a manual time zone fallback. Existing clocks get a one-off lookup by label (kept only if the time zone matches), and **Set location…** fixes any mistakes. Why: it's the only option where a wrong location is visible on the card. Rejected: guessing from the time zone, and looking up the label only. Both can quietly show the wrong place's weather.

**D-T3. Units.** *Decided by Brendon, 23 Sep 2026:* one °C/°F switch for all clocks, with the first-run default from the Windows region. Why: matches "F or C" in the request, and keeps cards comparable at a glance. Rejected: each place's own unit, and both units on every card.

**D-T4. Card display.** *Decided by Brendon, 23 Sep 2026:* temperature plus a weather icon, with night versions, replacing the ☀️/🌙 icon. Why: shows conditions without making cards taller, and avoids two icons in one corner. Accepted trade-off: a cloudy night shows ☁️, not 🌙. Rejected: temperature only, and temperature with high/low (high/low is kept as P1-1).

### Resolved without a decision (can be overruled)

- **Refresh interval:** 15 minutes, all clocks in one request.
- **Failure display:** "–", or the last value greyed out if it's under 2 hours old. Never an error dialog.
- **Credit:** "Weather: Open-Meteo.com" in the window's footer, plus a note in the README.
- **Storage:** location fields on each clock in `clocks.json`, so they sync with the sign-in feature.
- **Manual fallback:** a **Choose time zone manually** link stays in the Add dialog.

## Timeline and phasing

No external deadlines. This is independent of the sign-in feature and can ship before it.

1. **Locations:** new `ClockConfig` fields, city search in the Add dialog, one-off lookup for existing clocks, **Set location…**
2. **Weather:** `IWeatherService` (Open-Meteo), the card temperature and icon, the °C/°F switch, and the credit line.
3. **Fast follow:** P1 items.

Phase 1 is useful on its own, because adding a clock by city is easier than picking from Windows' time zone list.
