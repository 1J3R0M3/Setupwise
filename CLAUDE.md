# CLAUDE.md

Guidance for Claude Code (and other AI assistants) working in this repository.

## Project

**Setupwise** – a Windows desktop app that sets up a new PC: users pick apps from a curated
catalog or profiles, and Setupwise installs/updates them via `winget`. Target audience is
normal users, not power users (that niche is covered by UniGetUI).

- C# / .NET 10, WPF + [WPF-UI](https://github.com/lepoco/wpfui) 4.x (Fluent / Windows 11 look), MVVM with CommunityToolkit.Mvvm
- License GPL-3.0-or-later, UI in English and German
- Releases are **not code-signed** (SignPath was rejected by the maintainer – do not propose it again)
- `legacy/` holds the original PowerShell version; it is no longer developed

## Commands

```bash
dotnet build Setupwise.slnx                          # warnings are errors
dotnet test --project tests/Setupwise.Core.Tests     # runs on Linux, macOS and Windows
dotnet test --project tests/Setupwise.App.UiTests    # Windows only: renders every page
dotnet run --project src/Setupwise.App               # Windows only
pwsh ./scripts/Build-Release.ps1 [-SkipInstaller]    # artifacts/dist (installer needs Inno Setup)
pwsh ./scripts/Test-CatalogIds.ps1                   # checks catalog ids against microsoft/winget-pkgs (set GITHUB_TOKEN)
```

The WPF app **builds** on Linux thanks to `EnableWindowsTargeting`, but cannot run there.
If no .NET SDK is installed, install one into a temporary folder with
`dotnet-install.sh --channel 10.0 --install-dir <dir>` (do not install system-wide).
`global.json` opts into Microsoft.Testing.Platform, so use `dotnet test --project …` / `--solution …`.

## Layout

| Path | What |
|---|---|
| `src/Setupwise.Core` | Platform-independent logic: catalog, winget CLI (`WingetCli`, `WingetArguments`, `WingetTableParser`, `WingetExitCodes`), icons, `.setupwise` selection files, own categories, update check. **All logic goes here, with tests.** |
| `src/Setupwise.App` | WPF app. `ViewModels/`, `Views/`, `Services/`, `Localization/Strings/{en,de}.json`, `Styles/Controls.xaml` |
| `tests/Setupwise.Core.Tests` | xUnit v3. Also checks the real catalog, all translations and XAML conventions |
| `tests/Setupwise.App.UiTests` | Opens the real main window with `FakePackageManager`, saves screenshots of every page (de/light, en/dark) to `artifacts/screenshots`, fails on exceptions **and on any binding error** |
| `catalog/catalog.json` | Curated apps, categories, profiles (embedded into the app) |
| `docs/requirements.md` | Requirements and roadmap (requirements, not issues); keep the status column up to date |
| `installer/Setupwise.iss` | Inno Setup script (UTF-8 **with BOM**, CRLF; shows `privacy.*.txt` before installing) |
| `.github/workflows` | `ci.yml` (build/test on Linux, catalog id check, UI screenshots + installer on Windows), `release.yml` (tag `v*.*.*`) |

## Conventions

- Code, comments, commit messages and docs in **English**. Every UI text goes through `Loc`:
  `{loc:Tr Key}` in XAML, `Loc.T("Key")` / `Loc.F("Key", args)` in C#. Add new keys to **both** `en.json` and `de.json`
  with identical `{0}` placeholders – tests enforce this.
- Every XAML file with `x:Class` needs a `.xaml.cs` that calls `InitializeComponent()` (a missing one compiles but
  leaves the page empty – this happened once; a test now checks it).
- Pack URIs must name the assembly: `pack://application:,,,/Setupwise;component/...` (plain `/Assets/...` breaks in tests).
- Use WPF-UI theme brushes via `DynamicResource` (e.g. `TextFillColorPrimaryBrush`, `CardBackgroundFillColorDefaultBrush`);
  icons are `SymbolRegular` names (e.g. `Folder24`). XAML resource keys and icon names are only checked at runtime –
  verify new ones exist (reflection on `Wpf.Ui.dll`) and look at the CI screenshots.
- Page-specific toolbar buttons live in per-view-model `DataTemplate`s in `PackageListView.xaml`; do not bind commands
  that only exist on some pages (that produces binding errors and fails the UI test).
- **winget facts must be verified, not recalled**: option names against the official docs
  (`MicrosoftDocs/windows-dev-docs` → `hub/package-manager/winget/*.md`), exit codes against
  `microsoft/winget-cli` → `doc/windows/package-manager/winget/returnCodes.md`. (A wrong exit code from memory slipped in once.)
- Tests and the UI test must not touch the real user profile: `SETUPWISE_DATA_DIR` overrides all data folders.
- The UI test must not create the real `App` – WPF would run `App.OnStartup` (real settings, real winget).

## Catalog

Add apps to `catalog/catalog.json` (`id` = exact winget id, `category`, `description` with `en` + `de`).
Check the id exists (`Test-CatalogIds.ps1`); CI checks it too. Only well-known apps from the official vendor.

## Data on the user's PC

`%APPDATA%\Setupwise\settings.json` (incl. winget `InstallOptions`), `%APPDATA%\Setupwise\categories.json`
(own categories), `%LOCALAPPDATA%\Setupwise\{Logs,IconCache}`. Network access only for winget, vendor homepages
(icons, can be disabled) and the GitHub releases API (update check, can be disabled) – keep `PRIVACY.md` in sync.

## Git and releases

- Commit and push **directly to `main`**; no feature branches or PRs unless the maintainer asks for one.
- Commit identity in this repo: `1J3R0M3 <180740383+1J3R0M3@users.noreply.github.com>` (already set in the local
  git config). Never commit with a real name or private e-mail address. Do not merge PRs via the GitHub UI/API
  (that may use the account e-mail); apply Dependabot changes locally instead.
- Release: bump `VersionPrefix` in `Directory.Build.props`, move `[Unreleased]` in `CHANGELOG.md` to the new
  version, commit, `git tag -a vX.Y.Z -m "Setupwise X.Y.Z"`, push the tag. `release.yml` checks that tag and version
  match, runs all tests, builds installer + portable zip + `SHA256SUMS.txt` and creates the GitHub release.
- After pushing UI changes, download the `screenshots-*` artifact of the CI run and look at the pages.

## Working with the maintainer

- The maintainer writes in German; answer in German (code and docs stay English).
- The maintainer tests on Windows; Claude usually works on Linux. Say clearly what could only be checked
  via CI/screenshots and what still needs a real test on Windows (e.g. real winget installs).
