# World Clock

WPF desktop app on .NET 9. See [README.md](README.md) and [docs/developer-guide.md](docs/developer-guide.md).

## Build

Always name the project, because the folder also has a `.sln`:

```powershell
dotnet build WorldClock.csproj
dotnet publish WorldClock.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Close any running `WorldClock.exe` before publishing, because it locks the file.

The iPhone app is in `ios/` (SwiftUI, see [docs/ios-guide.md](docs/ios-guide.md)). It needs Xcode, which isn't the active developer directory on this Mac, so prefix commands with `DEVELOPER_DIR`:

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild test -project ios/WorldClock.xcodeproj -scheme WorldClock -destination 'platform=iOS Simulator,name=iPhone 17'
```

## Releasing: bump the version every time

Every change shipped to `main` gets a version bump, using Major.Minor:

- **minor** for features, improvements and fixes
- **major** for big or breaking changes (for example, raising `AppSettings.CurrentVersion`)

Use the script, which also updates the iPhone app's `MARKETING_VERSION`. Never edit either by hand:

```powershell
powershell -File scripts/bump-version.ps1 minor -Notes "What changed; Another change" -Commit
git push --follow-tags
```

Write notes in plain language for users, like the existing entries in `CHANGELOG.md`. Several commits released together share one bump.

## Conventions

- Colours come only from `Themes/Dark.xaml` and `Themes/Light.xaml` via `{DynamicResource …}`. No literal colours in XAML or code.
- On iPhone, colours come only from the colour sets in `ios/WorldClock/Assets.xcassets` (same values as the XAML themes). Add a new colour on both platforms.
- Behaviour changes should be made in both apps, or the difference noted in `docs/ios-guide.md`.
- Every new window calls `ThemeManager.Attach(this)` in its constructor.
- Update `README.md` and the relevant guide in `docs/` whenever you change behaviour.
