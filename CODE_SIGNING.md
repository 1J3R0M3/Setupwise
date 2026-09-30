# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

> **Status:** the application with SignPath Foundation is in progress. Releases up to 0.1.0 are not signed yet.

## What is signed

Only files built from this repository's source code by the [release workflow](.github/workflows/release.yml) on GitHub Actions:

- `Setupwise.exe`, `Setupwise.dll`, `Setupwise.Core.dll`
- the installer `Setupwise-<version>-Setup-x64.exe`

Third-party files shipped with Setupwise (the .NET runtime and libraries) keep the signatures of their publishers.
Every release is approved manually by an approver before it is signed.

## Team roles

| Role | Members |
|---|---|
| Committers and reviewers | [Members with write access to this repository](https://github.com/1J3R0M3/Setupwise/graphs/contributors) |
| Approvers | [@1J3R0M3](https://github.com/1J3R0M3) |

All team members use multi-factor authentication for GitHub and SignPath.

## Privacy

Setupwise connects to the websites of app vendors (to show icons) and to GitHub (to check for new versions); both can be turned off in the settings. See the [privacy policy](PRIVACY.md), which is also shown during installation.

## System changes and uninstalling

Setupwise installs apps only when the user explicitly starts the installation, and shows every app beforehand.
Setupwise itself can be removed via *Windows Settings → Apps → Installed apps → Setupwise → Uninstall*.
