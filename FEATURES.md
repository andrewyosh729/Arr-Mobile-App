# Arr Mobile App: Feature Plan

> **Living document.** This is the source of truth for what we're building and what's done.
> Update it in the same commit as the work it describes.

_Last updated: 2026-10-31 (first planning pass)_

---

## 1. Vision

A mobile-first app, built with **Avalonia (AXAML) + C#**, for managing self-hosted *Arr* servers. The first targets are **Sonarr** (TV) and **Radarr** (movies).

- Connect one or more Sonarr/Radarr servers with their URL and API key.
- Save connections ("accounts") locally in **SQLite** so they persist between sessions.
- Switch between Sonarr and Radarr in one tap. The app's context (library, discover, search wording) changes to match the active Arr.
- Share as much code and UI as possible across Arrs: monitoring, adding, interactive search, release lists and media cards are the same idea in each app.
- Look and feel like a real mobile app: touch-sized targets, bottom navigation, bottom sheets, pull-to-refresh, smooth image-heavy lists.

## 2. Working agreement

- **One commit per feature.** Each feature below has an ID (e.g. `F-104`). Implement it, tick its checkbox in this file, and commit both together.
- **Commit message format:** `F-104: Connection test against /system/status`. Non-feature work (typo fixes, doc edits) uses a plain descriptive message.
- **Done means:** builds on Desktop and Android, unit tests (where applicable) pass, and the checkbox is ticked.
- **Big features get split.** If a feature turns out larger than one reviewable commit, break it into sub-IDs (`F-502a`, `F-502b`) here first.
- **New ideas go in the backlog** (section 6), not straight into a phase.
- Claude is approved to make commits on the owner's behalf following these rules.

## 3. Architecture overview

### 3.1 Solution layout (target)

```
src/
  ArrApp.Core/        Domain models, IArrClient abstractions, Sonarr/Radarr clients (no UI refs)
  ArrApp.Data/        SQLite persistence: schema, migrations, repositories
  ArrApp.UI/          Shared Avalonia project: Views (AXAML), ViewModels, controls, styles
  ArrApp.Desktop/     Desktop head (Windows/macOS/Linux): dev loop and testing
  ArrApp.Android/     Android head
  ArrApp.iOS/         iOS head (building needs a Mac)
tests/
  ArrApp.Core.Tests/  Client tests against a mocked HttpMessageHandler + recorded JSON
  ArrApp.Data.Tests/  Repository tests against an in-memory / temp SQLite DB
```

### 3.2 Shared vs. Arr-specific

Sonarr and Radarr share the same API shape (`/api/v3`, `X-Api-Key` header, same concepts for quality profiles, root folders, releases, queue and commands). The plan:

| Concern | Shared | Arr-specific |
|---|---|---|
| HTTP, auth, errors | `ArrHttpClient` base | Base path only |
| Connection test | `GET /api/v3/system/status` | Validate `appName` matches the expected type |
| Library item | `MediaItem` (title, year, overview, images, monitored, status, size on disk, profile) | `Series` (seasons, episodes, network) / `Movie` (studio, runtime, digital/physical release) |
| Media card UI | `MediaCard` control bound to `MediaItem` | Small badge slot (e.g. "S3 · 24/30 eps" vs. "Downloaded") |
| Lookup / discover | Same search UI, same result card | `series/lookup` vs. `movie/lookup` |
| Add to library | Shared add sheet (profile, root folder, monitor, search on add) | Sonarr: monitor mode, series type, season folders. Radarr: minimum availability |
| Monitor toggle | `PUT` item with `monitored` flag | Sonarr also needs season/episode-level monitoring |
| Auto search | `POST /api/v3/command` | `SeriesSearch` / `SeasonSearch` / `EpisodeSearch` vs. `MoviesSearch` |
| Interactive search | `ReleaseList` UI + `Release` model, `POST /release` to grab | Query: `episodeId` / `seriesId`+`seasonNumber` vs. `movieId` |
| Queue / activity | `QueueItem` + queue UI | Episode vs. movie label on each row |

In code this is roughly an `IArrClient` (shared operations) plus `ISonarrClient` / `IRadarrClient` for app-specific ones. An **`ArrContext`** service holds the active server and tells the UI which Arr it is in. ViewModels depend on the shared interface wherever possible.

### 3.3 Key technology choices (proposed, see open questions)

- **MVVM:** CommunityToolkit.Mvvm (already referenced).
- **DI:** Microsoft.Extensions.DependencyInjection.
- **HTTP/JSON:** `HttpClient` + `System.Text.Json` **source generators** (trim/AOT-safe for iOS/Android).
- **SQLite:** `sqlite-net-pcl` (light and proven on mobile) or EF Core Sqlite. Decide in F-101.
- **Images:** async image loader with memory + disk cache (e.g. AsyncImageLoader.Avalonia or a small custom loader).
- **Theme:** Fluent base with our own design tokens (colors, spacing, corner radius, type scale). Dark-first, with an accent color per Arr (Sonarr blue/cyan, Radarr yellow/gold) so the active context is obvious.

### 3.4 Navigation model (mobile)

- **Top:** an Arr switcher (segmented control or pill dropdown) showing the active server, e.g. `Sonarr ▾`. Tapping it opens a sheet listing all saved servers.
- **Bottom tab bar** (contents depend on the active Arr): **Library · Discover · Activity · Settings** (Calendar later).
- **Detail pages** push onto a stack, with back gesture/button support (including Android back).
- **Actions** (add, interactive search, filters) open bottom sheets rather than new windows.

---

## 4. Features

Legend: `[ ]` not started · `[x]` done. IDs are stable and never reused.

### Phase 0: Foundation

- [x] **F-001a: Bring `LICENSE` and `README.md` onto `master`; confirm `bin/` and `obj/` stay ignored.** _(Done: copied from `origin/main`; ignore rules verified.)_
- [ ] **F-001b: Retire the old `main` branch and the old outer clone.** Needs the owner: `main` (unrelated history, now superseded) should be deleted on GitHub, and the old outer clone on disk removed so there's a single working copy. Left to the owner because both are destructive.
- [x] **F-002a: Shared UI project + Desktop head.** Split the desktop-only template into `src/ArrApp.UI` (shared App, `MainView` UserControl, ViewModels) and `src/ArrApp.Desktop`. Solution is `ArrApp.slnx`; builds on Desktop.
- [x] **F-002b: Android head.** `src/ArrApp.Android` builds (Avalonia 12 pattern: `AvaloniaAndroidApplication<App>` + non-generic `AvaloniaMainActivity`). Needs the `android` workload and an Android SDK/JDK; on this machine they live in `%LOCALAPPDATA%Android{Sdk,jdk}`, so set `ANDROID_HOME` / `JAVA_HOME` (or pass `-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`).
- [x] **F-002c: Android emulator launch.** Verified on a Pixel 7 / API 35 x86_64 AVD (`ArrPixel`). Android app projects need `<OutputType>Exe</OutputType>`. Run with: `dotnet build src/ArrApp.Android -t:Install -p:AdbTarget=-e -p:EmbedAssembliesIntoApk=true` (plus the SDK/JDK paths from F-002b).- [ ] **F-002d: iOS head.** `src/ArrApp.iOS` is scaffolded but unverified and not in the solution; needs a Mac (or a macOS CI runner, see F-008).
- [x] **F-003: Add Core / Data / test projects.** Create `ArrApp.Core`, `ArrApp.Data` and the test projects, set up references, and pick a test framework. _(Done: xUnit; solution is `ArrApp.slnx`.)_
- [x] **F-004: DI and app bootstrapping.** Service container, ViewModel resolution, and replace `ViewLocator` reflection with a trim-safe mapping.
- [x] **F-005: Navigation service.** Stack navigation (push/pop with params), tab host, bottom-sheet host, Android back-button handling. _(Done: `NavigationService` stack, `BottomSheetService`, `TabHostViewModel`, `ShellViewModel.HandleBack` wired to `TopLevel.BackRequested` (Esc on Desktop). Verified on the Android emulator. Placeholder tabs/pages until real screens land. Building the solution now needs `ANDROID_HOME` and `JAVA_HOME` set.)_
- [x] **F-006: Design system and mobile shell.** Color/spacing/typography tokens, light/dark, per-Arr accent color, safe-area insets, base control styles (buttons, inputs, list rows, chips), touch target ≥ 44px. _(Done: tokens in `Styles/Tokens.axaml`, control styles in `Styles/Controls.axaml`, per-Arr accent via `ThemeManager.ApplyAccent`, safe-area padding in `ShellView`. Dark-first; light variant defined but not yet eyeballed. Verified on the Android emulator. Caveat: on the dev machine, Windows blocked loading `ArrApp.Core.dll` in tests ("Application Control policy"), so the Core/Data tests and the new accent test could not be run after this change; the 4 navigation tests still pass.)_
- [x] **F-007: Error handling and logging baseline.** Global exception handler, user-facing error toast/snackbar, file/debug logging. _(Done: `GlobalExceptionHandler` (UI-thread, unobserved-task and last-chance handlers), `IErrorReporter` (log + toast), `ToastService` snackbar in the shell, daily-rolling `FileLoggerProvider` in `<LocalAppData>/ArrApp/logs` (7-day retention) plus Debug output. Verified on the emulator: a thrown command error shows a toast, is logged with its stack trace, and the app keeps running. The placeholder page has a temporary "Throw test error" button, to be removed with the placeholders.)_
- [x] **F-008: CI build.** GitHub Actions workflow that builds the solution and runs tests on push/PR. _(Optional, but cheap insurance.)_ _(Done in `.github/workflows/ci.yml`: Ubuntu job builds the whole solution (incl. Android) in Release and runs all tests; macOS job compile-checks the iOS head and is non-blocking until F-002d. The Release solution build passes locally, but the workflow itself has not run yet because nothing is pushed; confirm the first run on GitHub.)_

### Phase 1: Server connections (first pass, MVP)

- [x] **F-101: SQLite setup.** DB file in the app data directory, schema versioning/migrations, connection lifetime. _(Done: `sqlite-net-pcl` (Q4 decided); `AppDatabase` owns one lazily-opened connection, DB at `<LocalAppData>/ArrApp/arr.db`, versioned with `PRAGMA user_version` and forward-only raw-SQL `Migrations`; each migration runs in a transaction; a DB newer than the app is refused.)_
- [x] **F-102: `ServerConnection` model and repository.** Fields: `Id`, `Name`, `ArrType` (Sonarr/Radarr), `BaseUrl`, `ApiKey` (see F-109), `IsDefault`, `SortOrder`, `CreatedAt`, `LastConnectedAt`, `LastKnownVersion`. Full CRUD. _(Done: model + `IServerConnectionRepository` in Core, SQLite implementation in Data. Keys go through `IApiKeyProtector` before storage; the real per-platform protectors are F-109, which must land before any screen saves a key. First server added becomes the default; deleting the default promotes the next.)_
- [x] **F-103: Shared Arr HTTP client.** Base URL normalization (scheme, trailing slash, URL-base subpaths like `/sonarr`), `X-Api-Key` header, timeouts, cancellation, mapping of 401 / unreachable / TLS / non-JSON errors to friendly messages. _(Done: `ArrUrl.TryNormalize` (default `http://`, strips pasted `/api/v3`, keeps URL base), `ArrHttpClient` with per-request timeout, cancellation and `ArrApiException` kinds (unauthorized, not found, unreachable, timeout, TLS, server error, non-JSON). JSON via source generation (`ArrJsonContext`). Self-signed certificate support is not included yet; see Q5.)_
- [x] **F-104: Connection test.** Call `GET /api/v3/system/status`, show app name and version, and reject a mismatch (e.g. a Radarr URL saved as Sonarr). _(Done: `ArrConnectionTester` returns a `ConnectionTestResult` (never throws for expected failures; cancellation still propagates) and rejects a mismatched `appName` such as a Radarr URL saved as Sonarr. Tested against a fake handler only; not yet against a real server.)_
- [x] **F-105: Add/edit server screen.** Type picker, name, URL, API key (with paste and show/hide), "Test connection" button with inline status, save only after a successful test (with an override). _(Done: `ServerEditViewModel` + `ServerEditView`; saves only after a successful test of the values on screen (any edit invalidates it), with "Save anyway" for failures except a malformed address or wrong-app mismatch. Verified on the emulator against a local fake Sonarr: test, save, and the on-device DB holds only the Keystore-encrypted key. Edit mode exists (`Load`) but nothing opens it yet (F-106). Reached for now via a temporary "Add server (dev)" button on the placeholder page. Not yet tried against a real Sonarr/Radarr.)_
- [x] **F-106: Manage servers screen.** List saved servers with type icon and status, edit, delete (with confirm), set default, reorder. _(Done: Settings tab → Servers. List with Sonarr/Radarr accent badge, default pill and live status per server (online + version, or a short reason); add, edit (on a copy), make default, reorder with up/down buttons (drag-and-drop deliberately skipped on touch), delete behind a confirmation bottom sheet; empty state. Verified on the emulator against a local fake Sonarr/Radarr. Also: shell scrim now covers the whole screen and the sheet reaches the screen edge; removed my own safe-area handling because Avalonia already pads the main view; disabled chips fade instead of filling.)_
- [x] **F-107: First-run onboarding.** With no servers saved: welcome screen, then add your first server. Afterwards, land on the default server. _(Done: `ShellViewModel.InitializeAsync` shows `WelcomeViewModel` when no server is saved, otherwise the main tabs on the default server; saving the first server swaps Welcome out so there is no way back into it. New `ArrContext` (Core) holds the active server (default for now; F-108 adds switching and remembering the last one) and drives the accent colour. Verified on the emulator: clean install → Welcome → add Radarr → gold-accent tabs, and a restart lands straight on the tabs. A storage failure at startup is reported and falls back to Welcome. Placeholder pages show the active server label.)_
- [x] **F-108: Arr switcher.** Top-bar switcher that changes the active server and the context (tabs, accent, labels). Remembers the last active server across launches. _(Done: "● Name ▾" pill in the main top bar opens a bottom sheet listing all servers (active one ticked, plus a "Manage servers" link); picking one switches `ArrContext`, so the accent, labels and wording (Series vs Movies) follow. The last active server is remembered in a new `Setting` table (schema migration 2, `ISettingsStore`, reusable for F-404/F-605) and restored on launch, falling back to the default when it no longer exists or settings can't be read. Verified on the emulator, including the 1→2 schema upgrade on an existing database, and a restart landing on the remembered server rather than the default. The tab set is still the same for both Arrs; real Sonarr/Radarr-specific screens come in later phases.)_
- [x] **F-109: API key protection.** Don't store keys as plain text: platform secure storage (Android Keystore / iOS Keychain / DPAPI on Windows), or encrypt in SQLite with a key held there. _(See open question Q3.)_ _(Done, Q3 decided: platform secure storage. Windows = DPAPI (current user), Android = AES-GCM key inside the Android Keystore (verified on the emulator; `allowBackup` is off so a restored DB can never be left without its key), iOS = AES-GCM with a Keychain-held key (compile-checked in CI only, not run; see F-002d), Linux/macOS desktop = key file with owner-only permissions, which is weaker and meant for dev only. Heads register their protector via `PlatformServices.ApiKeyProtector` at startup. A key that can no longer be decrypted loads as empty so the user can re-enter it instead of the server list failing. The placeholder page has temporary "Test key vault" and "Throw test error" buttons.)_

### Phase 2: Shared media foundation

- [ ] **F-201: Shared domain models.** `MediaItem`, `MediaImage`, `QualityProfile`, `RootFolder`, `Tag`, `Release`, `QueueItem`, plus Sonarr `Series`/`Season`/`Episode` and Radarr `Movie`. JSON source-gen contexts.
- [ ] **F-202: Client abstraction.** `IArrClient` + `SonarrClient` / `RadarrClient`: library list, single item, quality profiles, root folders, tags. Tests use recorded JSON fixtures.
- [ ] **F-203: Image loading and caching.** Posters/fanart via the server's `MediaCover` endpoint (with API key), with remote URL fallback. Memory + disk cache, placeholder and fade-in.
- [ ] **F-204: `MediaCard` control.** Poster, title, year, monitored indicator, status/progress badge slot, and grid and list variants. Used for both shows and movies.
- [ ] **F-205: Media collection view.** Virtualized grid/list toggle, pull-to-refresh, local search, sort (title, date added, year, size), filter (monitored, missing, downloaded).
- [ ] **F-206: State components.** Shared loading skeleton, empty state, error-with-retry, offline banner.
- [ ] **F-207: Local caching of library data.** Cache the last fetched library per server so the app opens instantly, then refresh in the background.

### Phase 3: Library and details

- [ ] **F-301: Sonarr library tab.** Series in the collection view, with episode-count progress badge.
- [ ] **F-302: Radarr library tab.** Movies in the collection view, with downloaded/missing badge.
- [ ] **F-303: Series detail.** Hero/fanart header, overview, metadata, seasons list, episodes per season with file status, size and quality.
- [ ] **F-304: Movie detail.** Hero header, overview, metadata, file info (quality, size, path), release dates.
- [ ] **F-305: Monitor toggles (shared).** Series/movie-level monitor toggle, plus Sonarr season- and episode-level toggles. Optimistic update with rollback on failure.
- [ ] **F-306: Edit item.** Change quality profile, root folder/path, tags, and (Radarr) minimum availability.
- [ ] **F-307: Delete from library.** Confirm sheet with "delete files" and "add import exclusion" options.

### Phase 4: Discover and add

- [ ] **F-401: Lookup/search.** Shared search screen (debounced input) calling `series/lookup` or `movie/lookup`. Results use `MediaCard` and mark items already in the library.
- [ ] **F-402: Discover preview.** Tapping a result opens a read-only detail preview (reusing the detail layouts from F-303/F-304).
- [ ] **F-403: Add-to-library sheet (shared).** Quality profile, root folder, monitor, tags, "start search on add". Arr-specific sections plug in: Sonarr monitor mode/series type/season folders, Radarr minimum availability.
- [ ] **F-404: Remember add defaults.** Last-used profile/root folder per server, saved in SQLite.

### Phase 5: Searching and downloads

- [ ] **F-501: Automatic search.** Trigger search commands: series/season/episode (Sonarr) or movie (Radarr), with command status feedback.
- [ ] **F-502: Interactive search, shared `ReleaseList`.** Fetch releases and show title, quality, size, age, indexer, protocol, seeders/leechers, language, custom-format score, approved/rejected with reasons.
- [ ] **F-503: Release sort and filter.** Sort by size/age/seeders/score/quality, filter by protocol/quality/approved-only.
- [ ] **F-504: Grab release.** `POST /release`, with confirmation (and a warning when a rejected release is forced) and a success/failure toast.
- [ ] **F-505: Activity/queue tab.** Queue with progress, size remaining, ETA, status/warnings; pull-to-refresh plus periodic polling while visible.
- [ ] **F-506: Queue actions.** Remove from queue, with optional blocklist and remove-from-client.

### Phase 6: Polish and secondary features

- [ ] **F-601: Calendar/upcoming.** Combined or per-Arr upcoming episodes/releases.
- [ ] **F-602: Wanted/missing.** Missing (and cutoff unmet) lists with search actions.
- [ ] **F-603: History.** Per-item and global history (grabbed, imported, failed).
- [ ] **F-604: System health.** Server health warnings, disk space, version, shown in Settings and as a badge.
- [ ] **F-605: Settings screen.** Theme (system/light/dark), default server, image cache size + clear, about/version.
- [ ] **F-606: Accessibility pass.** Screen-reader labels, contrast check, dynamic text sizing.
- [ ] **F-607: App icon and splash.** Branding assets for all platforms.

### Phase 7: Release

- [ ] **F-701: Android packaging.** Signing, versioning, release build with trimming verified.
- [ ] **F-702: iOS packaging.** Provisioning, release build (requires a Mac).
- [ ] **F-703: Desktop packaging.** _(Optional.)_

---

## 5. Open questions

Answer these during refinement and record the decision here.

- **Q1: Platforms for v1.** Android only, or Android + iOS? (iOS builds need a Mac and an Apple developer account.)
- **Q2: Multiple servers per Arr type.** Allow e.g. "Sonarr" and "Sonarr 4K" from day one? (The data model above supports it; this only affects UI scope.)
- **Q3: API key storage.** Platform secure storage (recommended) vs. plain SQLite for simplicity in the MVP.
- **Q4: SQLite library.** `sqlite-net-pcl` (simple, mobile-proven) vs. EF Core (migrations, LINQ; heavier, needs more care with trimming/AOT).
- **Q5: Remote access.** Will users usually reach servers over LAN, reverse proxy, or VPN/Tailscale? This affects HTTP (cleartext) allowances on Android/iOS and support for self-signed certificates. _Partly decided: cleartext `http://` is allowed on Android (`usesCleartextTraffic`), since LAN servers are usually plain http. Self-signed certificate support is still open._
- **Q6: Future Arrs.** Which next? (e.g. Lidarr, Prowlarr.) This decides how generic `IArrClient` needs to be now.
- **Q7: Notifications.** Any interest in local notifications (download finished, etc.)? True push would need a server-side relay.

## 6. Backlog / ideas

- Combined "All" view across Sonarr + Radarr (unified activity/calendar)
- Home-screen widgets (upcoming, queue)
- Download client integration (qBittorrent/SABnzbd) for direct control
- Trakt / TMDB lists for richer Discover
- Biometric lock on app open
- Import/export server configs (QR code)

## 7. Changelog

- **2026-10-31:** Moved plan to `master`; updated F-001 for the new branch setup.
- **2026-10-31:** First planning pass: vision, architecture, phases 0–7, open questions.
