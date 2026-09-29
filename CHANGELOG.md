# Changelog

All notable changes to TaskbarFetch are documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0-beta.2] - 2026-09-30

### Added

- A saved tray-menu option, enabled by default, to move an already-active window to the monitor whose taskbar button was clicked. The choice persists across restarts.
- A per-user Windows setup wizard that installs TaskbarFetch under `%LOCALAPPDATA%\Programs\TaskbarFetch`, adds a Start menu shortcut, and registers the app in Windows' installed-app list.
- Setup choices to start TaskbarFetch with Windows and launch it after installation.
- A project-folder shortcut for source-checkout setup builds, pointing to the installed executable. Running a downloaded setup does not add shortcuts beside the downloaded installer.
- Privacy-conscious per-click diagnostics for taskbar hit-testing, grouped-window selection, retries, and timeouts. Window titles and taskbar button names are not recorded.
- Separate versioned portable and setup executables, plus a portable ZIP for release downloads.

### Fixed

- Prevent an older asynchronous click evaluation from replacing or clearing a newer pending taskbar click.
- Do not reject a taskbar click solely because Explorer reports an overly broad or overlapping notification-area child-window rectangle. Recognized shell controls continue to be ignored by accessible name.
- Re-evaluate a same-foreground click at the short settle interval, so an already-active window does not wait for the grouped-thumbnail timeout before it can move.
- Queue another evaluation when a foreground event arrives during an active evaluation, instead of losing that event until the pending-click timeout.
- Keep grouped-app clicks pending while Windows' thumbnail picker is open, then resolve the selected window after the user chooses a thumbnail.
- Find File Explorer windows across separate `explorer.exe` processes when resolving a grouped taskbar selection.
- Preserve normal Windows minimize behavior when the user clicks an application's taskbar button on the monitor where its window already resides.

### Changed

- Replace the script-based install and removal flow with a branded setup wizard and Windows uninstaller. Installation and removal require TaskbarFetch to be closed; setup does not force-close the app.
- Preserve the existing installation directory and startup preference during updates. The uninstaller removes the managed program files and shortcuts, and removes the startup entry only when it still points to the managed installation.
- Preserve user preferences and diagnostic logs after uninstalling.
- Give the portable build, portable launcher, and setup builder explicit `TaskbarFetch-...` command names.
- Keep the portable app and installer as distinct downloads, with versioned filenames generated from the central project version.

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
