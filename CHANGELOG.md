# Changelog

All notable changes to TaskbarFetch are documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- Avoid rejecting a taskbar click only because its coordinates fall inside a notification-area child-window rectangle. Explorer can report broad or overlapping child bounds; recognized shell controls are still ignored by their accessible names.
- Recheck the same-foreground move at its short settle point instead of leaving it pending until the grouped-thumbnail timeout.
- Queue another evaluation when a foreground event overlaps an active evaluation, so the event is not lost until the pending-click timeout.
- Keep a grouped-app click pending while Windows' thumbnail picker is visible, and count File Explorer windows across separate `explorer.exe` processes.

### Added

- A saved tray-menu option to move an already-active window to the monitor whose taskbar button was clicked. It is enabled by default.
- A per-user Start menu shortcut so installed users can find and open TaskbarFetch without browsing to its install folder.
- Privacy-safe per-click decision logs for hit-test results, grouped-window waits, selected-window resolution, and timeout cases; window titles and taskbar button names are not recorded.

### Changed

- Replace script-based setup and removal with a branded Windows setup wizard and native Windows uninstaller.
- Install per-user under `%LOCALAPPDATA%\Programs\TaskbarFetch`; add a Start menu shortcut and a standard Windows installed-app entry.
- Let users opt into startup during setup, while preserving existing TaskbarFetch startup state on updates and removing it only when it still points to the managed installation.
- Preserve saved preferences and diagnostic logs when uninstalling; refuse installation or removal while TaskbarFetch is running.
- Build a portable ZIP separately from the installable setup program.

## [1.0.0-beta.1] - 2026-09-29

### Added

- Initial pre-release candidate for testing.
- Move an existing application window to the monitor whose taskbar button was clicked.
- Support for primary and secondary Windows 11 taskbars.
- Preserve maximized, minimized, and normal window behavior when moving.
- Relative window-position mapping across monitors with different resolutions and DPI scaling.
- Delayed target handling for grouped taskbar buttons and thumbnail selection.
- System tray controls for Pause/Resume, startup, logs, About, and Exit.
- Per-user startup support without administrator privileges.
- Local diagnostic logging with no telemetry or network communication.
- Reproducible icon generator and original TaskbarFetch icon assets.
