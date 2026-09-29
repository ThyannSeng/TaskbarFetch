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
TaskbarFetch-Portable-Build.cmd
```

The build requires the .NET 10 SDK or newer. It builds the application project in Release configuration, verifies the generated version metadata, and writes the versioned portable executable to the project folder. Its name comes from `TaskbarFetchVersion` in `Directory.Build.props`:

```text
TaskbarFetch-Portable-v<version>.exe
```

The application is split by responsibility under `src\`. Keep Windows hooks and native API details isolated from tray UI, window management, preferences, and logging. Add automated coverage for platform-independent behavior where practical, and update the manual multi-monitor checklist when behavior changes.

Run the automated tests before submitting changes:

```powershell
dotnet restore .\tests\TaskbarFetch.GeometryTests\TaskbarFetch.GeometryTests.csproj --locked-mode
dotnet test .\tests\TaskbarFetch.GeometryTests\TaskbarFetch.GeometryTests.csproj --configuration Release --no-restore
```

Open `TaskbarFetch.sln` in Visual Studio to work with the app and automated tests. The standalone app project is `TaskbarFetch.csproj`.

## Pull requests

Please include:

- the problem being solved;
- the behavior before and after the change;
- steps used to test it;
- screenshots or a short recording when the change is visual or monitor-layout specific;
- any known limitations.

By contributing code, documentation, or assets, you agree that your contribution may be distributed under the project's MIT License.
