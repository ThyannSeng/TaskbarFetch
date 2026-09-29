# Contributing to TaskbarFetch

Thank you for considering a contribution.

## Before opening a pull request

1. Search existing issues to avoid duplicates.
2. Keep changes focused on one problem or feature.
3. Preserve the lightweight, local-only design. New network access, telemetry, or background services should not be introduced without a strong technical reason and explicit discussion.
4. Test on Windows 11 with the standard Explorer taskbar when the change affects taskbar detection or window movement.
5. Describe the monitor layout used for testing, including scaling values when relevant.

## Development build

From a Windows command prompt:

```bat
Build.cmd
```

The executable is written to:

```text
dist\TaskbarFetch.exe
```

A Visual Studio/MSBuild project is also provided as `TaskbarFetch.csproj`.

## Pull requests

Please include:

- the problem being solved;
- the behavior before and after the change;
- steps used to test it;
- screenshots or a short recording when the change is visual or monitor-layout specific;
- any known limitations.

By contributing code, documentation, or assets, you agree that your contribution may be distributed under the project's MIT License.
