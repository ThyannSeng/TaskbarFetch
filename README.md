<p align="center">
  <img src="assets/TaskbarFetch.png" width="128" alt="TaskbarFetch icon">
</p>

<h1 align="center">TaskbarFetch</h1>

<p align="center">
  Move an existing application window to the monitor whose taskbar button you clicked.
</p>

<p align="center">
  <strong>Created and maintained by Thyann Seng</strong>
</p>

> **Current version: v1.0.0-beta.1 (pre-release).** Please use the [multi-monitor test checklist](docs/TESTING.md) to validate your setup.

TaskbarFetch is a lightweight Windows 11 tray utility for multi-monitor setups. It makes the Windows taskbar behave in a way many multi-monitor users expect: if an application's window is on one monitor and you click that application's taskbar button on another monitor, TaskbarFetch moves the existing window to the monitor you clicked.

It is intentionally small and local-only. TaskbarFetch uses Windows Forms, Win32 hooks, and Microsoft Active Accessibility. It does not use Electron, run a background service, require a server, send telemetry, or require administrator privileges for normal desktop applications.

## What it does

Suppose Chrome is open on Monitor 1:

```text
Monitor 1                         Monitor 2
┌────────────────────────┐        ┌────────────────────────┐
│                        │        │                        │
│      Chrome            │        │                        │
│                        │        │                        │
├────────────────────────┤        ├────────────────────────┤
│ [Chrome] [Explorer]    │        │ [Chrome] [Explorer]    │
└────────────────────────┘        └────────────────────────┘
                                             ↑
                                      click Chrome here
```

TaskbarFetch moves the existing Chrome window to Monitor 2 and activates it.

## Features

- Move an existing application window to the monitor whose taskbar button was clicked.
- Support the primary and secondary standard Windows 11 taskbars.
- Preserve maximized windows when moving them.
- Restore and move minimized windows when appropriate.
- Preserve normal Windows minimize behavior when you click the application's taskbar button on the same monitor.
- Support grouped applications by keeping the target monitor pending while you choose a taskbar thumbnail.
- Map normal window position and size proportionally between monitors.
- Handle different monitor resolutions, work areas, and DPI scaling.
- Support monitor layouts that use negative desktop coordinates.
- Pause or resume from the system tray.
- Optional per-user startup with Windows.
- Local diagnostic logging.
- No telemetry and no network communication.

## Requirements

- Windows 11
- The standard Windows Explorer taskbar
- Two or more monitors for the primary use case
- .NET Framework 4.8 or later for source builds

For the intended workflow, enable:

**Settings > Personalization > Taskbar > Taskbar behaviors > Show my taskbar on all displays**

If Windows offers a setting for where taskbar app buttons appear, choose an option that displays the relevant application button on the monitor from which you want to move the window.

## Installation

### Option 1: GitHub release

Download the latest Windows release ZIP from the repository's **Releases** page, extract it, and run:

```text
Install.cmd
```

TaskbarFetch is installed for the current user at:

```text
%LOCALAPPDATA%\TaskbarFetch\TaskbarFetch.exe
```

The installer also enables **Start with Windows** for the current user and launches TaskbarFetch.

No administrator rights are required.

### Option 2: Build from source

Clone the repository and run:

```bat
Build.cmd
```

The executable will be created at:

```text
dist\TaskbarFetch.exe
```

Then run `Install.cmd`, or launch the executable directly.

The repository also contains `TaskbarFetch.csproj` for Visual Studio/MSBuild users.

## Portable use

From a source checkout, run:

```text
RunPortable.cmd
```

For a release package, you can run `TaskbarFetch.exe` directly without installing it.

## Tray menu

Right-click the TaskbarFetch icon in the notification area:

- **Pause / Resume** temporarily disables or enables window-moving behavior.
- **Start with Windows** toggles per-user startup.
- **Open log folder** opens TaskbarFetch's local diagnostic folder.
- **About** displays the application version and creator credit.
- **Exit** closes TaskbarFetch and removes its hooks.

Double-clicking the tray icon also toggles Pause/Resume.

## Behavior details

### Clicking an app on another monitor

If an application is on Monitor 1 and you click its taskbar button on Monitor 2, the selected application window is moved to Monitor 2.

### Clicking an app on its current monitor

TaskbarFetch does not intentionally override ordinary Windows behavior. For example, clicking an already-active application's taskbar button on the same monitor can still minimize it normally.

### Grouped taskbar buttons

If an application has several top-level windows and Windows displays thumbnail previews, TaskbarFetch remembers the monitor whose taskbar button you clicked. After you choose the desired thumbnail, that selected window becomes the movement target.

### Maximized windows

A maximized window remains maximized after being moved.

### Normal windows

A normal window's restored rectangle is mapped proportionally from the source monitor's work area to the destination monitor's work area. The result is clamped so that the window remains reachable.

This is useful when your monitors have different resolutions or scaling values.

## Privacy

TaskbarFetch is designed to operate entirely on your PC.

It:

- does not hook the keyboard;
- observes left mouse clicks only for taskbar interaction detection;
- observes foreground-window changes;
- does not transmit data over the network;
- does not include analytics or telemetry;
- does not log window titles or taskbar button names.

Logs are stored locally under:

```text
%LOCALAPPDATA%\TaskbarFetch
```

## Elevated applications

Windows User Interface Privilege Isolation can prevent a normal unelevated process from repositioning an administrator-elevated window.

TaskbarFetch deliberately runs without elevation because ordinary applications should not require TaskbarFetch itself to have administrator privileges. If an elevated application cannot be moved, that can be a Windows security boundary rather than a TaskbarFetch monitor-detection failure.

## Third-party taskbars

TaskbarFetch targets the standard Windows Explorer taskbar classes:

```text
Shell_TrayWnd
Shell_SecondaryTrayWnd
```

Third-party taskbar replacements or heavily modified Explorer shells may expose different window classes or accessibility structures and are not guaranteed to work.

## How it works

At a high level, TaskbarFetch uses:

- `WH_MOUSE_LL` to observe taskbar mouse clicks;
- `MonitorFromPoint` to identify the clicked monitor;
- `AccessibleObjectFromPoint` as an advisory taskbar-control hit-test;
- `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` to observe the window selected by Windows;
- `SetWindowPos` and related window-state APIs to move the selected top-level window;
- Per-Monitor V2 DPI awareness;
- a dedicated STA worker for accessibility work so the low-level mouse hook remains fast.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for more detail.

## Building

### Simple Windows build

```bat
Build.cmd
```

`Build.cmd` uses the Windows .NET Framework C# compiler already present on a normal Windows installation and creates:

```text
dist\TaskbarFetch.exe
```

### Visual Studio / MSBuild

Open `TaskbarFetch.csproj` or build it with MSBuild on Windows.

## Testing

The most important behavior depends on real Explorer taskbars and monitor topology. See [docs/TESTING.md](docs/TESTING.md) for the manual regression matrix used for multi-monitor validation.

If you encounter an issue, please include your Windows version, monitor arrangement, resolutions, scaling percentages, and whether any taskbar customization software is installed.

## Uninstall

Run:

```text
Uninstall.cmd
```

This stops TaskbarFetch, removes its current-user startup entry, and deletes its installed files from `%LOCALAPPDATA%\TaskbarFetch`.

## Project ownership and icon provenance

TaskbarFetch is an independent implementation created by **Thyann Seng**.

The application icon is also an original TaskbarFetch asset. The deterministic source generator is included at:

```text
assets/generate_icon.py
```

This allows the PNG and Windows ICO assets to be regenerated directly from repository source. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for additional provenance information.

## Contributing

Contributions and reproducible bug reports are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## License

TaskbarFetch is released under the [MIT License](LICENSE).

Copyright © 2026 **Thyann Seng**.

TaskbarFetch is not affiliated with or endorsed by Microsoft. Windows and Microsoft are trademarks of Microsoft Corporation.
