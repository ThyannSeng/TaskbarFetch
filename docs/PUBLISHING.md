# Publishing TaskbarFetch on GitHub

The repository is published at [github.com/ThyannSeng/TaskbarFetch](https://github.com/ThyannSeng/TaskbarFetch). The initial pre-release candidate is `v1.0.0-beta.1`.

## Suggested repository description

> A lightweight Windows 11 utility that moves an existing app window to the monitor whose taskbar button you clicked.

## Suggested GitHub topics

```text
windows
windows-11
multi-monitor
taskbar
win32
csharp
winforms
desktop-utility
productivity
open-source
```

## Create a release

Before publishing a new version, update `CITATION.cff`, `CHANGELOG.md`, the `AssemblyInformationalVersion` attribute in `src/TaskbarFetch.cs`, and the release status in `README.md`. Then create and push a unique semantic-version tag:

```bash
git tag v1.0.0-beta.2
git push origin v1.0.0-beta.2
```

The release workflow builds the executable on Windows, creates a ZIP containing the installer and linked documentation, calculates SHA-256 checksums, and publishes a GitHub release. A semantic-version tag with a pre-release suffix, such as `-beta.2` or `-rc.1`, is marked as a pre-release. A stable tag such as `v1.0.0` is published as a normal release.

## Recommended repository settings

- Enable **Issues**.
- Enable **Discussions** if you want a place for usage questions.
- Enable **Private vulnerability reporting** under Security settings.
- Protect the `main` branch if other contributors begin submitting pull requests.
- Require the `Build` workflow to pass before merging pull requests.
