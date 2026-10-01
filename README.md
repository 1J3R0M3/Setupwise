<p align="center">
  <img src="assets/setupwise-256.png" width="96" alt="Setupwise logo">
</p>

<h1 align="center">Setupwise</h1>

<p align="center">
  <b>Set up a new Windows PC in minutes.</b><br>
  Pick a profile, check the apps you need, click <i>Install</i> – done.
</p>

<p align="center">
  <a href="https://github.com/1J3R0M3/Setupwise/actions/workflows/ci.yml"><img src="https://github.com/1J3R0M3/Setupwise/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/1J3R0M3/Setupwise/releases/latest"><img src="https://img.shields.io/github/v/release/1J3R0M3/Setupwise?include_prereleases" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue" alt="License: GPL v3"></a>
</p>

<p align="center"><a href="README.de.md">Deutsche Version</a></p>

---

Setupwise is a friendly Windows app for everyone who sets up PCs – your own, your family's or the ones at work. It installs and updates programs with **winget**, the package manager built into Windows, so every app comes straight from the official source.

## Features

- **Profiles** – *Essentials*, *Home office*, *Gaming PC*, *Creator*, *Developer*: one click selects everything that belongs together.
- **Curated catalog** – popular apps sorted into categories, with descriptions in English and German.
- **Search** – find any of the thousands of apps in the winget catalog.
- **Updates** – see which installed apps are outdated and update them in one go. Skip a version or exclude an app
  from updates completely (winget pin) with a right-click.
- **Installed apps** – everything winget knows as installed, in one list.
- **Right-click to install** – install or update a single app right away, also with one-off options
  (installer wizard, only for me, for all users, reinstall).
- **Console** – run your own winget commands; the output appears in the log.
- **Take your setup with you** – save your selection as a `.setupwise` file and open it on the next PC.
- **Windows 11 look** – Mica, light and dark mode, real app icons.
- **Privacy-friendly** – no telemetry, no account. Icons are loaded from each vendor's own website (can be turned off). See the [privacy policy](PRIVACY.md).

## Installation

1. Download **`Setupwise-x.y.z-Setup-x64.exe`** from the [latest release](https://github.com/1J3R0M3/Setupwise/releases/latest).
2. Run it and follow the wizard. No admin rights are needed.

Prefer no installer? Use the **portable zip** from the same page.

**Requirements:** Windows 10 (1809) or Windows 11, x64, and the *App Installer* from the Microsoft Store (preinstalled on current Windows versions; Setupwise tells you if it is missing).

> **Note:** Releases are not code-signed, so Windows SmartScreen may show a warning. Choose *More info → Run anyway*. You can verify the download with `SHA256SUMS.txt`.

## How it works

Setupwise is a graphical front end for [`winget`](https://learn.microsoft.com/windows/package-manager/winget/). When you click *Install*, it runs `winget install --id <app> --exact --silent` for each selected app, one after another, and shows the progress. Apps that need admin rights ask for permission through the normal Windows prompt – or start Setupwise as administrator (*Settings → Advanced*) to avoid repeated prompts.

## Contributing

Contributions are very welcome – especially **new apps for the catalog** and **translations**. Neither requires C# knowledge. See [CONTRIBUTING.md](CONTRIBUTING.md).

## Privacy

No telemetry, no account. Setupwise only contacts app vendors' websites (for icons) and GitHub (for the update check) – both can be turned off. See the [privacy policy](PRIVACY.md).

## License

Setupwise is free software under the [GNU General Public License v3.0](LICENSE) or later.
Setupwise is not affiliated with Microsoft. The apps it installs are provided by their respective vendors under their own licenses.
