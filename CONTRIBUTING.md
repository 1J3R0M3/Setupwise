# Contributing to Setupwise

Thanks for helping! This guide covers the three most common contributions.

## 1. Add an app to the catalog

No C# needed – you only edit [`catalog/catalog.json`](catalog/catalog.json).

1. Find the winget id: `winget search <name>` and confirm with `winget show --id <Id> --exact`.
2. Add an entry to `apps`:
   ```jsonc
   { "id": "Vendor.App", "name": "App Name", "category": "utilities",
     "description": { "en": "What it does, in one short sentence.", "de": "Dasselbe auf Deutsch." } }
   ```
3. Optionally add the id to a profile under `profiles`.

**What belongs in the catalog?** Well-known apps that many people install on a new PC, from the official vendor. No trial-ware traps, no adware, no niche tools.

CI checks automatically that the JSON is valid, every category and profile reference exists, and the id exists in [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).

## 2. Translate Setupwise

UI texts live in [`src/Setupwise.App/Localization/Strings`](src/Setupwise.App/Localization/Strings):

1. Copy `en.json` to `<language code>.json` (for example `fr.json`) and translate the values. Keep placeholders like `{0}` exactly as they are.
2. Add the language to `Loc.Available` in [`Loc.cs`](src/Setupwise.App/Localization/Loc.cs).
3. Add translations to the texts in `catalog/catalog.json` (`"fr": "..."`).

Missing keys fall back to English; tests check that every key and placeholder matches.

## 3. Code

### Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The app runs on Windows, but everything **builds and tests on Linux and macOS** too:

```bash
dotnet build Setupwise.slnx
dotnet test --solution Setupwise.slnx
dotnet run --project src/Setupwise.App      # Windows only
```

Release files (installer needs [Inno Setup](https://jrsoftware.org/isinfo.php) on Windows):

```powershell
./scripts/Build-Release.ps1                 # artifacts/dist
./scripts/Build-Release.ps1 -SkipInstaller  # only the portable zip
```

### Project layout

| Path | Contents |
|---|---|
| `src/Setupwise.Core` | Platform-independent logic: catalog, winget integration, icon lookup, selection files, update check. Fully unit-tested. |
| `src/Setupwise.App` | WPF app (MVVM with CommunityToolkit.Mvvm, UI with [WPF-UI](https://github.com/lepoco/wpfui)). |
| `tests/Setupwise.Core.Tests` | xUnit tests, including checks of the catalog and all translations. |
| `catalog/catalog.json` | The curated app catalog and profiles. |
| `installer/Setupwise.iss` | Inno Setup script. |
| `legacy/` | The original PowerShell version (no longer developed). |

### Guidelines

- Warnings are errors; keep the build clean.
- Logic goes into `Setupwise.Core` with tests; the app stays thin.
- Code and comments in English; every UI text goes through `Loc` (no hard-coded strings in XAML).
- Keep pull requests focused and describe what you tested.

## Releases (maintainers)

1. Update `VersionPrefix` in `Directory.Build.props` and the `CHANGELOG.md`.
2. Commit, then `git tag v<version> && git push origin v<version>`.
3. The *Release* workflow builds, tests and publishes installer, portable zip and checksums.
4. With code signing set up, the workflow pauses twice for approval in SignPath (app files, then installer).

Code signing needs the repository variable `SIGNPATH_ORGANIZATION_ID` and the secret `SIGNPATH_API_TOKEN`
(optional variables: `SIGNPATH_PROJECT_SLUG`, default `Setupwise`; `SIGNPATH_SIGNING_POLICY_SLUG`, default `release-signing`).
The artifact configurations for SignPath are in [`.signpath/artifact-configurations`](.signpath/artifact-configurations).
