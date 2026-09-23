# Changelog

World Clock uses **Major.Minor** version numbers:

- **Minor** (1.2 → 1.3): new features, improvements and fixes.
- **Major** (1.x → 2.0): big or breaking changes, such as a new settings file format that older versions can't read.

To release a new version, run `scripts/bump-version.ps1` (see the [developer guide](docs/developer-guide.md#versioning)).

## 1.2 (2026-09-23)

- Simpler header: **+ Add clock** and a **⋯** menu holding temperature unit, theme, Always on top and About
- Theme can now be Light, Dark or **Use Windows setting** (the default), which follows Windows live
- Always on top is remembered between launches
- Menus match the light and dark themes
- The version number is shown in the ⋯ menu, and releases are versioned with `scripts/bump-version.ps1`

## 1.1 (2026-09-23)

- Current temperature and weather icon on every clock, from Open-Meteo
- °C / °F switch
- Add a clock by searching for a city, which sets the time zone for you
- **Rename…** and **Set location…** on each clock
- About window with links to the blog, GitHub and Open-Meteo
- Light and dark themes
- Settings file upgraded to a versioned format, with existing clocks migrated automatically

## 1.0 (2026-09-22)

- Clocks for New Zealand, Croatia, UK, New York, Dallas and Seattle
- Add, remove and drag-to-reorder clocks, saved between launches
- Always on top
- MIT licence
