# Publishing TaskbarFetch on GitHub

The repository is published at [github.com/ThyannSeng/TaskbarFetch](https://github.com/ThyannSeng/TaskbarFetch). The latest published pre-release is `v1.0.0-beta.1`; `v1.0.0-beta.2` is the next local test candidate.

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

Build and manually test the candidate using the scenarios in [the multi-monitor test checklist](TESTING.md). Do not publish or tag it until the taskbar-button behavior passes on the target Windows setup.

Before publishing a validated version, move the appropriate entries from `Unreleased` into a dated section in `CHANGELOG.md`, update `CITATION.cff`, update `TaskbarFetchVersion` (and assembly/file versions when appropriate) in `Directory.Build.props`, and update the release status in `README.md`. Then create and push a unique semantic-version tag:

```bash
git tag v1.0.0-beta.2
git push origin v1.0.0-beta.2
```

The release workflow builds the executable and branded setup program on Windows, creates a separate portable ZIP, calculates SHA-256 checksums for each download, and publishes the matching changelog section with a direct bug-report link on the GitHub release. The release tag must match `TaskbarFetchVersion` in `Directory.Build.props`. A semantic-version tag with a pre-release suffix, such as `-beta.2` or `-rc.1`, is marked as a pre-release. A stable tag such as `v1.0.0` is published as a normal release.

The `TaskbarFetch Build` workflow builds the portable executable through the .NET SDK project, checks its embedded version, runs the locked geometry tests, builds the installer, and verifies install/update/uninstall behavior in a clean runner profile. The release workflow repeats those checks before creating a release. These checks do not replace the manual multi-monitor tests above.

The setup program is compiled with Inno Setup 6.7.3 from its official signed release. The CI helper verifies the publisher signature before installing the compiler on the temporary runner.

## Recommended repository settings

- Enable **Issues**.
- Enable **Discussions** if you want a place for usage questions.
- Enable **Private vulnerability reporting** under Security settings.
- Protect the `main` branch if other contributors begin submitting pull requests.
- Require the `TaskbarFetch Build` workflow to pass before merging pull requests.
