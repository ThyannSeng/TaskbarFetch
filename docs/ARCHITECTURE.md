# Architecture

TaskbarFetch is a single-process Windows tray application designed to remain small and predictable.

## Event flow

1. A low-level mouse hook observes a left-click.
2. TaskbarFetch checks whether the click occurred inside a standard Windows Explorer taskbar (`Shell_TrayWnd` or `Shell_SecondaryTrayWnd`).
3. The monitor containing that taskbar click becomes the pending target monitor.
4. Microsoft Active Accessibility is used as an advisory hit-test to distinguish likely application buttons from Start, Search, notification-area, and other taskbar controls.
5. A foreground-window event hook observes the application window Windows activates after the taskbar interaction. When a grouped-app thumbnail picker is visible, the taskbar click stays pending until a window is selected or the short selection deadline expires.
6. If the target monitor differs from the window's current monitor, TaskbarFetch moves the selected top-level window. File Explorer windows are counted across separate Explorer processes when identifying a group. A current-user preference controls the special case where the clicked window remains active.
7. Normal windows are proportionally mapped between source and destination work areas. Maximized and minimized state is preserved.

## Main components

The runtime is split by responsibility under `src\`:

- `Program`: single-instance startup and application lifetime.
- `TrayApplicationContext`: tray icon and commands.
- `TaskbarFetchEngine`: mouse/foreground event coordination.
- `AccessibilityHitTesting`: taskbar accessibility classification.
- `WindowManagement`: state-preserving movement, window classification, and monitor data.
- `UserPreferences`: per-user startup registration and saved settings.
- `Logger`: local diagnostics.
- `NativeMethods`: Win32 interop definitions.

`Directory.Build.props` is the single version source for the project, command-line builds, Visual Studio builds, installer, and release workflow. `scripts\TaskbarFetch-Version.ps1` validates the version fields and derives the portable and setup filenames.

Per-click diagnostics record timing, hit-test classification, and window handles/counts only. They do not record window titles or taskbar button names.

## Threading

The low-level mouse hook is kept fast. Accessibility work is delegated to a dedicated STA worker thread instead of performing potentially slow COM/accessibility calls directly inside the hook callback.

## Security boundaries

Windows User Interface Privilege Isolation (UIPI) can prevent an unelevated TaskbarFetch process from manipulating a window belonging to an elevated application. TaskbarFetch does not request elevation because doing so would unnecessarily increase privilege for normal desktop use.
