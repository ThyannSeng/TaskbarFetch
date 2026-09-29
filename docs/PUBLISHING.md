# Publishing TaskbarFetch on GitHub

The repository is prepared for a `main` branch and semantic-version tags such as `v1.0.0`.

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

## First push

Create an empty GitHub repository named `TaskbarFetch`, then from this folder run:

```bash
git init
git add .
git commit -m "Initial release of TaskbarFetch"
git branch -M main
git remote add origin https://github.com/YOUR-USERNAME/TaskbarFetch.git
git push -u origin main
```

Replace `YOUR-USERNAME` with your GitHub username.

## Create the first release

The included release workflow runs when a version tag is pushed:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will build the executable on Windows, create a release ZIP, generate SHA-256 checksums, and create the GitHub release automatically.

## Recommended repository settings

- Enable **Issues**.
- Enable **Discussions** if you want a place for usage questions.
- Enable **Private vulnerability reporting** under Security settings.
- Protect the `main` branch if other contributors begin submitting pull requests.
- Require the `Build` workflow to pass before merging pull requests.
