# Feature: Sign in to save and sync settings

| | |
| --- | --- |
| **Status** | Phases 1–2 (Microsoft sign-in and OneDrive sync) built 23 Sep 2026, on branch `feature/microsoft-sign-in`, waiting for the Entra app registration's client ID before end-to-end testing. Phase 3 (Google) not started. See [Implementation notes](#implementation-notes) |
| **Author** | Brendon |
| **Date** | 23 Sep 2026 |
| **Affects** | `MainWindow` (persistence and header UI), new auth and sync services, `AddClockWindow` (unchanged) |

## Summary

Let people sign in with a **Microsoft account** or a **Google account** so their clocks (the list, order and labels) are saved to the cloud. The same clocks then appear on any Windows PC where they sign in.

**Approach (decided by Brendon, 23 Sep 2026):** there's no World Clock server. Settings are saved as a small JSON file in the signed-in user's **own** cloud storage:

- **Microsoft:** the app's private folder in OneDrive (`Files.ReadWrite.AppFolder`)
- **Google:** the app's hidden data folder in Google Drive (`drive.appdata`)

The app can only see its own folder, never the user's other files. The developer never holds anyone's data, and there's nothing to host or pay for. This suits a free, open-source hobby app far better than running a backend. See [Decision 1](#decisions).

Microsoft sign-in ships first, with Google in a later release ([Decision 2](#decisions)).

## Problem statement

Clocks are saved only in `%APPDATA%\WorldClock\clocks.json` on one PC. Anyone using World Clock on more than one computer (for example a work laptop and a home desktop) has to recreate the list by hand on each one and keep them in step. Reinstalling Windows or moving to a new PC loses the setup completely. The pain grows with the number of clocks: people who track many cities, or give clocks custom labels like "Mum" or "Dallas office", are the ones who care most about keeping them.

## Goals

1. **Set up once, use anywhere.** After signing in on a second PC, the user sees their clocks there within 10 seconds, without re-entering anything.
2. **Changes follow you.** A change made on one PC (add, remove, reorder or rename) shows up on another signed-in PC the next time it's opened or brought to the front.
3. **No data loss.** Signing in, signing out, going offline or editing on two PCs never silently throws away a user's clocks.
4. **Sign-in stays optional.** People who never sign in get exactly today's behaviour, with no nagging.
5. **Private by design.** Settings live only on the user's PC and in their own cloud storage. The app requests the narrowest permission each provider offers.

## Non-goals

| Out of scope | Why |
| --- | --- |
| A World Clock backend or database | Costs money, needs securing and makes the developer responsible for user data, all to store a few hundred bytes the user's own cloud can hold for free |
| Email/password accounts or other providers (Apple, GitHub) | Microsoft and Google cover almost every Windows user. Running our own passwords is a security liability |
| Real-time sync (changes appearing within seconds on an open window) | Needs push infrastructure. Checking on startup and when the window is focused is enough for how rarely clocks change |
| Sharing clocks with other people, or team lists | A different feature with different permission needs |
| Mac, mobile or web versions | The app is WPF and Windows-only |
| Merging a Microsoft-saved copy with a Google-saved copy | Only one account is signed in at a time. Switching accounts is covered, merging across them isn't |
| Usage analytics or telemetry | The app has none today, and adding tracking to justify metrics would work against goal 5 |

## User stories

**Person with more than one PC** (main persona)

- As someone who uses two PCs, I want to sign in with the Microsoft account I already use for Windows, so I don't have to create a new account.
- As someone who uses two PCs, I want my clocks to appear on my second PC once I sign in, so I don't have to set them up twice.
- As someone who uses two PCs, I want a change I make on one PC to appear on the other, so they never drift apart.
- As a Google user, I want to sign in with my Google account instead, so I can use the account I actually rely on.

**Person moving to a new PC**

- As someone who's just reinstalled Windows, I want to sign in and get my clocks back, so a rebuild doesn't cost me my setup.

**Privacy-conscious person**

- As someone careful about permissions, I want the app to ask only for access to its own folder, so I can trust it isn't reading my files.
- As a user, I want to sign out and remove my saved settings from the cloud, so I can leave cleanly.

**Edge cases**

- As someone who has clocks on this PC and also saved in the cloud, when I first sign in I want to choose which set to keep (or combine them), so neither is overwritten without my say.
- As a laptop user who's offline, I want to keep editing clocks, and have those changes sync when I'm back online.
- As a user whose sign-in has expired, I want a clear prompt to sign in again rather than sync silently stopping.
- As someone who never signs in, I want the app to work exactly as it does today.

## Requirements

### P0: must have

**R1. Sign in with Microsoft**
- Uses MSAL.NET (`Microsoft.Identity.Client`) with the Windows account broker (WAM), so people already signed in to Windows can pick their account in one click.
- Accepts personal Microsoft accounts only (Outlook.com, Hotmail, Live, Xbox), using the `consumers` authority. Work and school accounts can't sign in ([Decision 3](#decisions)).
- Requests only the `Files.ReadWrite.AppFolder` and `offline_access` scopes.

Acceptance criteria:
- [ ] Given I'm not signed in, when I click **Sign in → Microsoft** and complete the Windows prompt, then the header shows my name or email and the sync status.
- [ ] The consent screen lists access to the app's own folder only, not all my files.
- [ ] Given I close the consent prompt, then I stay signed out, nothing changes, and no error dialog appears.

**R2. Sign in with Google**
- Uses Google's OAuth 2.0 flow for desktop apps: the system browser, a loopback redirect to `127.0.0.1` and PKCE (for example via `Google.Apis.Auth`).
- Requests only the `drive.appdata` scope plus the basic profile/email scope needed to show who's signed in.

Acceptance criteria:
- [ ] Given I'm not signed in, when I click **Sign in → Google**, then my default browser opens Google's sign-in page, and after I approve, the app shows me as signed in.
- [ ] Given I close the browser tab without approving, then the app gives up after a timeout (for example 2 minutes) and stays signed out.

**R3. Save settings to the user's cloud**
- Microsoft: `me/drive/special/approot:/clocks.json` via Microsoft Graph.
- Google: a file named `clocks.json` in `appDataFolder` via the Drive v3 API.
- The file uses the versioned format in [Data format](#data-format).
- Uploads are debounced (for example 2 seconds after the last change), so dragging a card doesn't cause dozens of uploads.

Acceptance criteria:
- [ ] Given I'm signed in, when I add, remove or reorder a clock, then within 5 seconds (while online) the cloud copy matches.
- [ ] Given I drag a card across six positions, then there is at most one upload once I let go.

**R4. Load settings from the cloud**
- Pull the cloud copy on startup, and when the window is focused if more than 60 seconds have passed since the last check.
- If the cloud copy is newer than the local one, replace the local clocks with it.

Acceptance criteria:
- [ ] Given PC A and PC B are signed in to the same account, when I add "Tokyo" on A and then focus B's window after at least 60 seconds, then B shows Tokyo.

**R5. First sign-in when both local and cloud clocks exist**

Acceptance criteria:
- [ ] Given this PC has clocks that differ from the cloud copy, when I sign in, then I'm asked to choose **Use saved clocks** or **Keep this PC's clocks**. The copy that isn't kept is saved as `clocks.backup.json`. (Combining both lists is P1-6, per [Decision 6](#decisions).)
- [ ] Given the cloud has no saved file yet, then the local clocks are uploaded without asking.
- [ ] Given the local clocks are still the untouched defaults, then the cloud copy is used without asking.

**R6. Work offline and keep a local copy**
- `%APPDATA%\WorldClock\clocks.json` stays the working copy. The app never needs the network to start or to edit.
- Changes made while offline are uploaded when the connection returns.

Acceptance criteria:
- [ ] Given I'm signed in and offline, when I open the app, then my last-synced clocks appear immediately and the status says "Offline, changes will sync".
- [ ] Given I made changes offline, when I go back online, then they're uploaded within 60 seconds or on the next window focus.

**R7. Two PCs editing the same settings**
- Last write wins, using `updatedAt`. Before uploading, the app checks that the cloud file hasn't changed since it was last read (using the provider's ETag). If it has, the app pulls the newer copy first.

Acceptance criteria:
- [ ] Given PC A and PC B both change clocks offline, when both reconnect, then both end up with the same list, namely the most recently changed one, and neither app crashes or loops.
- [ ] Before overwriting, the local copy being replaced is saved as `clocks.backup.json`, so nothing is lost for good.

**R8. Stay signed in, securely**
- Tokens are saved so people don't have to sign in on every launch. They're encrypted with Windows DPAPI for the current user: the MSAL cache via `Microsoft.Identity.Client.Extensions.Msal`, and the Google token store wrapped the same way. Tokens are never written as plain text.
- An expired or revoked token doesn't crash the app. It shows "Sign in again to keep syncing".

Acceptance criteria:
- [ ] Given I signed in yesterday, when I launch the app today, then I'm still signed in and no browser or prompt appears.
- [ ] The token files in `%LOCALAPPDATA%\WorldClock\` can't be read as plain text.

**R9. Sign out**

Acceptance criteria:
- [ ] Given I'm signed in, when I choose **Sign out**, then saved tokens are deleted and my clocks stay on this PC as they are.
- [ ] After signing out, changes are no longer uploaded.

**R10. Header UI and sync status**
- Signed out: a **Sign in** button next to **+ Add clock**, opening a menu with **Microsoft** and **Google**.
- Signed in: the user's initials or picture, opening a menu with the account email, **Sync now** and **Sign out**.
- A status line under the title shows one of: "Synced just now" / "Synced 5 min ago" / "Syncing…" / "Offline, changes will sync" / "Sign in again to keep syncing".

### P1: nice to have

- **P1-1. Delete my cloud data.** A menu option that deletes the cloud `clocks.json` and signs out, after asking for confirmation.
- **P1-2. Sync more settings.** Include **Always on top** and window size/position in the synced file. This also fixes the known limitation that **Always on top** isn't remembered.
- **P1-3. Rename a clock.** Not strictly about sign-in, but label edits are exactly what people want synced. Today they have to remove the clock and add it again.
- **P1-4. Restore the previous version.** A menu option to restore `clocks.backup.json`.
- **P1-5. Switching accounts.** Signing in with a different account signs out the current one first and runs the R5 choice again.
- **P1-6. Combine both on first sign-in.** Add a third R5 choice: the saved list, then any of this PC's clocks not already in it, de-duplicated by label and time zone.
- **P1-7. Work and school accounts.** Switch the Microsoft authority from `consumers` to `common`, and handle "admin approval required" errors clearly.

### P2: future considerations (design for, don't build)

- **Multiple profiles** (for example "Work" and "Personal" clock sets). The data format should allow a list of named sets later without breaking v1 readers.
- **Other providers.** Hide the storage behind an `ISettingsStore` interface (`LoadAsync`, `SaveAsync`, returning content plus ETag), so Dropbox or a different backend can be added without touching the UI.
- **Near-real-time sync** using OneDrive and Drive change notifications, if polling on focus turns out not to be enough.

## Data format

The current file is a bare JSON array. v1 wraps it in a versioned object:

```json
{
  "version": 1,
  "updatedAt": "2026-09-23T04:12:09Z",
  "deviceName": "PRINTERPC",
  "clocks": [
    { "City": "New Zealand", "TimeZoneId": "New Zealand Standard Time" },
    { "City": "Croatia", "TimeZoneId": "Central European Standard Time" }
  ]
}
```

- **Migration:** on first launch of the new version, a bare array is read as `version: 1` with `updatedAt` set to the file's last-modified time, then rewritten in the new format.
- **Forward compatibility:** readers ignore unknown fields. If `version` is higher than the app understands, the app doesn't overwrite the cloud copy and shows "Update World Clock to sync".
- Time zone IDs stay as Windows IDs. Every PC running the app is on Windows, so they always resolve.

## Architecture sketch

```
MainWindow ──▶ SettingsService ──▶ LocalStore            (%APPDATA%\WorldClock\clocks.json)
                    │
                    └──▶ ISettingsStore ──┬──▶ OneDriveStore   (MSAL + Graph, approot)
                                          └──▶ GoogleDriveStore (Google OAuth + Drive appDataFolder)
               AuthService: sign in / out, token cache (DPAPI), current account
```

- `MainWindow` stops reading and writing JSON itself and goes through `SettingsService`.
- `SettingsService` owns debouncing, ETag checks, conflict handling and the status text.
- New packages: `Microsoft.Identity.Client`, `Microsoft.Identity.Client.Broker`, `Microsoft.Identity.Client.Extensions.Msal`, `Google.Apis.Drive.v3`. Graph calls are two plain REST endpoints, so the Graph SDK is optional.

## Security and privacy notes

- **Public repo:** both apps are registered as *public clients*. The Microsoft client ID is safe to publish. Google's "client secret" for desktop apps isn't treated as confidential by Google, but keep it out of source anyway (for example inject it at build time) so forks register their own. Document the steps for forks in the developer guide.
- **Least privilege:** the app-folder scopes can't read any of the user's other files. This is the main reason for choosing them.
- **Data held by the developer:** none. No server logs, no user list.
- A short privacy statement is needed, both for the Google consent screen and to reassure users. It will be hosted on GitHub Pages (see [Resolved without a decision](#resolved-without-a-decision)).

## Success metrics

The app has no telemetry, and adding it is a non-goal, so these are checked through manual test runs on two PCs and through GitHub issues.

| Type | Metric | Target |
| --- | --- | --- |
| Leading | Sign-in completes first try (both providers) in test runs | 10 of 10 |
| Leading | Time from signing in on a second PC to clocks appearing | ≤ 10 s |
| Leading | Change on PC A shows on PC B after focusing it | ≤ 60 s + one focus |
| Leading | Offline edit → reconnect → synced, with no data lost | 100% of test runs |
| Lagging | GitHub issues reporting lost or overwritten clocks, 3 months after release | 0 |
| Lagging | GitHub issues or requests about sync not working | Fewer than 3 in the first 3 months |

## Decisions

| Decided | Still open |
| --- | --- |
| Storage in the user's own cloud app folder, no server (D1) | Nothing blocking. The feature is decided but **not yet scheduled**: nobody is assigned and no start date is set |
| Microsoft first, Google in a later release (D2) | |
| Personal Microsoft accounts only (D3) | |
| First sign-in offers "keep one" only, combine later (D6) | |

**D1. Where synced settings are stored.** *Decided by Brendon, 23 Sep 2026:* in the signed-in user's own OneDrive app folder and Google Drive app data folder. There's no World Clock server. Why: it's free, no user data is held by the developer, and failures show up to the user ("Sign in again") instead of going unnoticed on a server. Rejected: our own server (ongoing cost, security upkeep and responsibility for user data), and OneDrive only (leaves out Google users). Rules out, by choice: sharing clocks between users and a web version, since each user's data is only reachable through their own account.

**D2. Launch order.** *Decided by Brendon, 23 Sep 2026:* Microsoft sign-in ships first, and Google follows in a later release. Why: Microsoft needs only a free app registration with no review, and every user is on Windows. Rejected: launching both together, which would hold Microsoft users back until Google's setup is ready.

**D3. Which Microsoft accounts are allowed.** *Decided by Brendon, 23 Sep 2026:* personal accounts only (Outlook, Hotmail, Live, Xbox), using MSAL's `consumers` authority. Why: that's the "Live ID" in the request, and it avoids "admin approval required" errors from organisations that block unapproved apps. Rejected, for now: work and school accounts, kept as P1-7 (a one-line authority change plus error handling).

**D6. First sign-in when this PC's clocks differ from the saved ones.** *Decided by Brendon, 23 Sep 2026:* offer "Use saved clocks" or "Keep this PC's clocks" only, and back up the copy that isn't kept. Why: nothing is lost, and combining two lists raises ordering questions that are easy to get subtly wrong. Rejected, for now: "Combine both" in v1, kept as P1-6.

### Resolved without a decision

These had a standard answer or could be looked up, so no decision was needed. Any of them can be overruled.

- **Q4, privacy statement and homepage:** a GitHub Pages site at `stewartisland.github.io/world-clock`. It's free, and Google can verify you own it for the OAuth consent screen. Needed before the Google release only.
- **Q5, Google verification:** not blocking. Google's Drive API scope guide (developers.google.com/workspace/drive/api/guides/api-specific-auth, read 23 Sep 2026) lists `drive.appdata` as a **non-sensitive, recommended** scope that needs only basic OAuth app verification. The 100-user limit for unverified apps covers sensitive and restricted scopes, so it doesn't apply.
- **Q7, Google client secret:** keep it in a file git ignores and add it into release builds. Forks register their own Google client. Document this in the developer guide when the Google work starts.

## Implementation notes

Built for Microsoft (phases 1–2). Where the build differs from the requirements above:

- **R10, header UI:** the header had already been reduced to **+ Add clock** and **⋯** (release 1.2), so sign-in lives in a **Sync** section at the top of the ⋯ menu: **Sign in with Microsoft…**, or, when signed in, the account email, **Sync now** and **Sign out**. The status line is at the bottom left of the window instead of under the title.
- **What syncs:** clocks, as specified, plus the temperature unit and theme (part of P1-2). **Always on top** stays per-PC.
- **Conflict handling (R7):** the rule is as specified (newest `updatedAt` wins, with If-Match on the eTag). The replaced copy is written to `clocks.backup.json` whenever this PC had changes that hadn't been uploaded yet.
- **Tests:** `tests/WorldClock.Tests` covers every R5/R7 path against an in-memory store, including a conflict partway through an upload and a queued change surviving a restart.
- **Still to verify with a real account:** the WAM sign-in prompt, Graph `approot` read/write, and the token cache. These need the client ID.

## Timeline and phasing

There are no external deadlines. Suggested phases, each shippable on its own:

1. **Groundwork:** versioned data format and migration, `SettingsService`, `ISettingsStore`, header sign-in UI and status line. No behaviour changes for people who don't sign in.
2. **Microsoft sign-in and OneDrive sync:** R1, R3–R10 for Microsoft. Needs an app registration in the Entra admin centre (free, about 10 minutes).
3. **Google sign-in and Drive sync (a later release, per D2):** R2, plus the Google Cloud project, consent screen, GitHub Pages privacy statement and basic app verification.
4. **Fast follow:** P1 items, starting with syncing **Always on top** and adding rename.

**Dependencies:** a Microsoft Entra app registration, a Google Cloud project and OAuth client, and a public privacy statement URL.
