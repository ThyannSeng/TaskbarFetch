# Architecture

TaskbarFetch is a single-process Windows tray application designed to remain small and predictable.

## Event flow

1. A low-level mouse hook observes a left-click.
2. TaskbarFetch checks whether the click occurred inside a standard Windows Explorer taskbar (`Shell_TrayWnd` or `Shell_SecondaryTrayWnd`).
3. The monitor containing that taskbar click becomes the pending target monitor.
4. Microsoft Active Accessibility is used as an advisory hit-test to distinguish likely application buttons from Start, Search, notification-area, and other taskbar controls.
5. A foreground-window event hook observes the application window Windows activates after the taskbar interaction.
6. If the target monitor differs from the window's current monitor, TaskbarFetch moves the selected top-level window.
7. Normal windows are proportionally mapped between source and destination work areas. Maximized and minimized state is preserved.

## Main components

The current implementation intentionally keeps the runtime in one source file to reduce moving parts in the initial release:

- `Program`: single-instance startup and application lifetime.
- `TrayApplicationContext`: tray icon and commands.
- `TaskbarFetchEngine`: mouse/foreground event coordination.
- `AccessibilityHitTester`: taskbar accessibility hit-testing.
- `WindowMover`: state-preserving monitor movement.
- `WindowUtilities`: window enumeration and classification helpers.
- `StartupManager`: per-user startup registration.
- `Logger`: local diagnostics.
- `NativeMethods`: Win32 interop definitions.

## Threading

The low-level mouse hook is kept fast. Accessibility work is delegated to a dedicated STA worker thread instead of performing potentially slow COM/accessibility calls directly inside the hook callback.

## Security boundaries

Windows User Interface Privilege Isolation (UIPI) can prevent an unelevated TaskbarFetch process from manipulating a window belonging to an elevated application. TaskbarFetch does not request elevation because doing so would unnecessarily increase privilege for normal desktop use.
