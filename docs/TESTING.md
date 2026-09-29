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

## Regression checks

- Start, Search, Task View, Widgets, and notification-area clicks should not move unrelated app windows.
- TaskbarFetch should remain single-instance.
- The application should not require administrator rights for normal applications.
- No network connection should be required.
- Window titles and taskbar button text should not be written to logs.

## Known environment dependency

Third-party taskbar replacements or deep Explorer/taskbar customization may expose different window classes or accessibility trees. Those environments require separate compatibility testing.
