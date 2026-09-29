# Third-Party Notices

TaskbarFetch does not incorporate copied third-party application source code or third-party icon artwork.

The application interacts with Microsoft Windows through operating-system APIs, including Win32, Microsoft Active Accessibility, Windows Forms, and the .NET Framework. Microsoft binaries are not redistributed by this repository.

The TaskbarFetch icon is an original project asset. Its generator is included in `assets/generate_icon.py` so the artwork can be reproduced from source.

The GitHub Actions workflow references official GitHub Actions such as `actions/checkout` and `actions/upload-artifact`. Those actions execute in GitHub's CI environment and are not bundled into TaskbarFetch.

The Windows setup executable is built with Inno Setup 6.7.3. Inno Setup is copyrighted by Jordan Russell, with portions copyrighted by Martijn Laan. Its compiler and generated installer are subject to the [Inno Setup license terms](https://jrsoftware.org/ishelp/topic_whatisinnosetup.htm). Non-commercial use is free; commercial users are asked to purchase a license.

"Windows" and "Microsoft" are trademarks of Microsoft Corporation. TaskbarFetch is an independent project and is not affiliated with or endorsed by Microsoft.
