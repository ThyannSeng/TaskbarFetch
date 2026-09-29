# Testing Guide

TaskbarFetch depends on real Windows Explorer taskbar behavior, so the most important tests are performed on a physical or virtual Windows 11 desktop with multiple displays.

## Core manual test matrix

Test each scenario with TaskbarFetch active:

1. Normal window on Monitor 1, click its taskbar button on Monitor 2.
2. Maximized window on Monitor 1, click its taskbar button on Monitor 2.
3. Minimized window, activate it from another monitor's taskbar.
4. Already-active window on Monitor 1, click the same app button on Monitor 2.
5. Click the same app button on the window's current monitor and verify normal Windows behavior is preserved.
6. Open several windows from one application, click a grouped taskbar button on another monitor, then choose a thumbnail.
7. Test two monitors with different resolutions.
8. Test mixed scaling, for example 100% and 150%.
9. Test a layout with a monitor positioned left of the primary display, producing negative desktop coordinates.
10. Pause TaskbarFetch and confirm it stops moving windows; resume and confirm operation returns.
11. Toggle Start with Windows and confirm the per-user Run entry changes.
12. Exit TaskbarFetch and confirm hooks are removed and normal Windows behavior returns.
13. Toggle **Move already-active window to clicked monitor**, verify an already-active single-window app moves promptly from another monitor when enabled, and verify it stays in place when disabled. Confirm the selected state persists after restarting TaskbarFetch.
14. Open several File Explorer windows, click their grouped taskbar button on another monitor, choose different thumbnails, and verify only the selected Explorer window moves each time.
15. Repeat the grouped File Explorer selection with the thumbnail popup open for a few seconds before choosing. Confirm the chosen window moves promptly after selection, and same-monitor clicks keep normal minimize behavior.
16. Run `TaskbarFetch-Setup-v<version>.exe` and verify the setup wizard identifies TaskbarFetch and Thyann Seng, installs under `%LOCALAPPDATA%\Programs\TaskbarFetch`, creates a Start menu shortcut, and registers TaskbarFetch under Windows installed apps without requesting administrator access. Test both settings for the optional startup task, and confirm the final-page launch checkbox starts the app only when selected.
17. Install a source-checkout build and confirm the project-folder `TaskbarFetch.lnk` points to the installed executable. Install a release setup from a different directory and confirm no shortcut is added beside the downloaded setup file.
18. While TaskbarFetch is running, start setup and the uninstaller separately. Confirm both require the app to be exited and never force-close it. After exiting the app, uninstall from Windows Settings; confirm the installed executable and managed shortcuts are removed, the startup entry is removed only when it still points to the installed executable, and diagnostic logs and saved preferences remain.
19. Repeat setup over an existing installation with startup enabled and disabled. Confirm updates preserve the current startup preference and keep the existing install directory. Verify the portable executable continues to work after uninstalling the installed copy.

## Regression checks

- Start, Search, Task View, Widgets, and notification-area clicks should not move unrelated app windows.
- TaskbarFetch should remain single-instance.
- The application should not require administrator rights for normal applications.
- No network connection should be required.
- Window titles and taskbar button text should not be written to logs.

## Known environment dependency

Third-party taskbar replacements or deep Explorer/taskbar customization may expose different window classes or accessibility trees. Those environments require separate compatibility testing.
