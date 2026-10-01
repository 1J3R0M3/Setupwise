# Changelog

All notable changes to Setupwise are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Page "Installed" with every app winget knows as installed.
- Right-click an app: *Install now* / *Update now*, and *Install with* one-off options (installer wizard,
  only for me, for all users, reinstall).
- Exclude an app from updates (blocking winget pin) or allow updates again, from the right-click menu.
- Skip one version of an update; newer versions are shown again. Skipped updates can be shown again in the settings.
- Console: run own winget commands from the bottom bar; the output appears in the log.
- Beta versions: the version shows its pre-release suffix, and the update check can offer betas (off by default).
- Four new categories (Graphics & photos, Security, Cloud storage, Runtimes) and 40 more apps.
- Clear status when an app could not be updated because it is still running.
- `docs/requirements.md`: requirements and roadmap.

### Changed
- App names in the catalog can be translated (e.g. "Mozilla Firefox (Deutsch)").
- Polished German texts.

### Fixed
- After creating a category via "New category", the dialog opened a second time.

## [0.2.0] - 2026-09-30

### Added
- Own categories: create, rename and delete them; add apps via right-click on any app card;
  save the current selection as a category.
- Installation settings for winget: installation mode (silent, with progress, interactive),
  scope (user/machine), architecture, installer language, force, skip dependencies,
  show updates for unknown versions.
- Security checks (with warning): ignore checksum mismatches and skip the malware scan of
  archives, plus a button that allows these options in winget (admin).
- Clear status for "checksum mismatch" and "download failed", with a hint how to proceed.
- Privacy policy, shown during installation in English and German.

### Fixed
- The settings page was empty.
- Correct singular texts for one selected app or one update.
- Readable status labels in dark mode.

## [0.1.0] - 2026-09-30

First version of Setupwise as a Windows application (rewrite of the former PowerShell "Winget Installer").

### Added
- Windows 11 style app (WPF, Mica, light/dark mode) in English and German.
- Curated catalog with 35 apps in 7 categories and 5 profiles.
- Search in the full winget catalog.
- Updates page for installed apps with newer versions.
- Install queue with progress, cancel and log.
- Save and load selections as `.setupwise` files; double-click opens them in Setupwise.
- App icons from the vendor's website, cached locally (can be turned off).
- Check for new Setupwise versions on GitHub (can be turned off).
- Installer (per-user or all users) and portable zip.
