# Setupwise – requirements and roadmap

Collected requirements for the next versions of Setupwise. These are **requirements, not issues**: each entry says
what is wanted, why, how it could be built and how big it is. Entries that are being worked on or are done carry a
status. The same entries are tracked as draft items on the
[GitHub Project "Setupwise roadmap"](https://github.com/users/1J3R0M3/projects/1) (see R-01).

Size: **S** = hours, **M** = a day or two, **L** = several days, needs design and real testing on Windows.

| ID | Requirement | Size | Status |
|---|---|---|---|
| R-01 | [Plan the work in a GitHub Project](#r-01-plan-the-work-in-a-github-project) | S | done |
| R-02 | [Bug: "New category" dialog opens a second time](#r-02-bug-new-category-dialog-opens-a-second-time) | S | done (0.3.0) |
| R-03 | [Alpha/beta releases](#r-03-alphabeta-releases) | S | done (0.3.0) |
| R-04 | [Install from the context menu, with options](#r-04-install-from-the-context-menu-with-options) | M | done (0.3.0) |
| R-05 | [Console for own winget commands](#r-05-console-for-own-winget-commands) | M | done (0.3.0) |
| R-06 | [Page "Installed apps"](#r-06-page-installed-apps) | M | done (0.3.0) |
| R-07 | [Exclude apps from updates](#r-07-exclude-apps-from-updates) | M | done (0.3.0) |
| R-08 | [German translation polish](#r-08-german-translation-polish) | S | first pass done, more examples welcome |
| R-09 | [More curated categories (Ninite-like)](#r-09-more-curated-categories-ninite-like) | M | done (0.3.0) |
| R-10 | [Detect apps that are still running during an update](#r-10-detect-apps-that-are-still-running-during-an-update) | M | step 1 done (0.3.0) |
| R-11 | [One admin prompt per run (like UniGetUI)](#r-11-one-admin-prompt-per-run-like-unigetui) | L | open |
| R-12 | [Optional Chocolatey support](#r-12-optional-chocolatey-support) | L | open |
| R-13 | [Microsoft Store package](#r-13-microsoft-store-package) | L | step 1 done (MSIX build), Store submission open |
| R-14 | [Optional auto-updater](#r-14-optional-auto-updater) | M | open |
| R-15 | [Code signing](#r-15-code-signing) | – | decision needed |
| R-16 | [License: GPL or MIT](#r-16-license-gpl-or-mit) | – | decision needed |
| R-17 | [Release policy](#r-17-release-policy) | – | proposal below |
| R-18 | [Catalog based on WinUtil](#r-18-catalog-based-on-winutil) | M | done (unreleased) |
| R-19 | [Windows tweaks (installer build only)](#r-19-windows-tweaks-installer-build-only) | L | step 1 built (registry tweaks), needs a test on Windows |
| R-20 | [Usability pass](#r-20-usability-pass) | M | built (branch `feature/r20-usability`), needs a test on Windows |

---

## R-01 Plan the work in a GitHub Project

**Wanted:** plan with a GitHub Project (board) instead of only the repository; entries are requirements, not issues.

**Proposal:** one user-level Project "Setupwise roadmap" linked to the repository, with **draft items** (they are not
issues and do not show up in the issue tracker). Fields: *Status* (Idea / Planned / In progress / Done), *Size*
(S/M/L), *Version* (iteration or text, e.g. 0.3.0). Each draft item links to its section in this file. Only when a
requirement is picked up and needs discussion is it converted into an issue.

**Done:** [Setupwise roadmap](https://github.com/users/1J3R0M3/projects/1) (private, linked to the repository) with
one draft item per requirement and the fields *Status* (Todo / In Progress / Done), *Size* (S/M/L/–), *Kind*
(Bug / Feature / Decision / Process) and *Version*. Requirements that are built but not yet released are
*In Progress* with version "next release"; they move to *Done* after the release. This file stays the place for the
details; when a requirement changes here, update its board item too.

## R-02 Bug: "New category" dialog opens a second time

**Problem:** after creating a category via "New category" in the navigation, the dialog appears again right away.

**Cause:** "New category" is an entry of the navigation list that runs an action instead of showing a page. When it
was clicked, the view model switched the selection back to the previous page *while the ListBox was still processing
the click*. The ListBox then re-applied its own selection, which started the action a second time.

**Fix:** the selection is switched back after the ListBox has finished (dispatcher), and an action that is already
running is not started again.

## R-03 Alpha/beta releases

**Wanted:** pre-release versions (alpha, beta) for testers.

**Built:**
- Tags such as `v0.3.0-beta.1` already become GitHub *pre-releases* (`release.yml`); the version in the app now keeps
  the suffix (`0.3.0-beta.1`) and the settings page shows it.
- New setting *"Include beta versions"* in the update check (off by default). Pre-releases are compared by SemVer
  rules: `0.3.0-alpha.1 < 0.3.0-beta.1 < 0.3.0-beta.2 < 0.3.0`.
- Release steps for a pre-release: keep `VersionPrefix` at the coming version (e.g. `0.3.0`), tag
  `git tag -a v0.3.0-beta.1 -m "Setupwise 0.3.0 beta 1"`.

## R-04 Install from the context menu, with options

**Wanted:** right-click an app → install it right away; the winget options should also be available there.

**Built:** the context menu of every app card has
- *Install now* / *Update now* – runs just this app with the options from the settings,
- *Install with…* – the same, but with a one-off option: show installer (interactive), only for me, for all users,
  force reinstall.

While the queue is running, these entries are disabled.

## R-05 Console for own winget commands

**Wanted:** an icon that opens a console to enter own commands.

**Built:** a *Console* button in the bottom bar opens the log panel with an input line. Whatever is typed there is
passed to **winget** (e.g. `show --id Git.Git`, `source update`, `pin list`), the output appears in the log.
Arguments are split like a command line (quotes supported) and passed as separate arguments, so nothing goes through
a shell. Only winget can be started this way – deliberately, so the feature cannot be misused to run arbitrary
programs. A running command can be stopped.

## R-06 Page "Installed apps"

**Wanted:** a menu entry for installed packages.

**Built:** new page *Installed* that lists every app winget knows as installed from the winget source, with version,
update and "excluded from updates" badge. From the context menu: update, exclude from updates, skip a version.

**Later:** uninstall (`winget uninstall`) from this page – needs a confirmation dialog and its own result texts.

## R-07 Exclude apps from updates

**Wanted:** exclude an app from winget updates – either completely or only the current version – best via the context
menu.

**Built (facts checked against the winget docs and source):**
- *Exclude from updates* → `winget pin add --id <id> --exact --blocking`. A *blocking* pin stops `winget upgrade`
  for this app altogether, also outside Setupwise, until it is removed. `winget upgrade` without `--include-pinned`
  does not list pinned apps, so they disappear from the Updates page.
- *Allow updates again* → `winget pin remove --id <id> --exact`.
- *Skip this version (x.y)* → stored in Setupwise's settings (`SkippedUpdates`). The update is hidden until a newer
  version than the skipped one appears. winget itself has no "skip one version" – a gating pin (`--version`) would
  block all later versions too.
- Pins are read with `winget pin list` when the installed apps are loaded.

## R-08 German translation polish

**Wanted:** some German texts are not written the way one would write them.

**Done (first pass):** app names in the catalog can now be translated (e.g. "Mozilla Firefox (Deutsch)" instead of
"(German)" in the German UI), outdated settings subtitle, a few wordings.

**Next:** please collect the concrete texts that sound wrong (screenshot or key) – without examples this stays guesswork.

## R-09 More curated categories (Ninite-like)

**Wanted:** more ready-made categories and apps, inspired by Ninite.

**Built:** new categories *Graphics & photos*, *Security*, *Cloud storage*, *Runtimes* and more apps in the existing
categories (messengers, media players, utilities, developer tools). Every id was checked against
`microsoft/winget-pkgs`; only well-known apps from the official vendor (as before).

## R-10 Detect apps that are still running during an update

**Wanted:** check whether an app that is being updated is still running, tell the user, maybe close it after
confirmation.

**Step 1 (built):** winget reports "application is currently running" (`0x8A150101`, `0x8A150103`, `0x8A150111`) for
installers that support it. Setupwise now shows *"Close the app and try again"* instead of a generic error; the app
stays selected for a retry.

**Step 2 (open, M):** before an update, find running processes that belong to the app. winget does not tell where an
app is installed, so the install location has to come from the uninstall registry (`InstallLocation` of the matching
ARP entry). If a process runs from there: dialog *"Firefox is open. Close it now?"* → `CloseMainWindow()` (lets the app
save), after a timeout offer *"Force close"*. Needs real testing on Windows with several apps.

## R-11 One admin prompt per run (like UniGetUI)

**Wanted:** only one UAC prompt per update run instead of one per installer.

**How UniGetUI does it:** it ships a copy of gsudo ("UniGetUI Elevator") with a credential cache, so later elevations
in the same run do not prompt again.

**Proposal for Setupwise:** an own elevated worker instead of a third-party binary: Setupwise starts itself once with
`runas` in a worker mode (`Setupwise.exe --worker <pipe-name>`), the main window sends the queue over a named pipe and
receives progress and results. Points to solve:
- Per-user apps must **not** run in the admin context: if the admin is a different account, they would be installed
  for that account, and winget refuses some operations there (`0x8A15007D`). So only machine-scope operations go to
  the worker; the rest runs as before.
- The pipe must only accept the own process (ACL on the pipe, random name).
- Effort L, must be tested on Windows with standard user + separate admin account.

## R-12 Optional Chocolatey support

**Wanted:** use Chocolatey as an additional source – strictly optional.

**Proposal:** `IPackageManager` already abstracts winget. A `ChocolateyCli` backend implements search, list, outdated,
install and upgrade via `choco.exe`. The setting *"Use Chocolatey"* is off by default and only visible when
`choco.exe` is found; Setupwise does not install Chocolatey itself. Packages get a source badge (winget / choco), the
store key becomes source + id. The curated catalog stays winget-only. Most choco installs need admin rights, so this
benefits from R-11. Effort L.

## R-13 Microsoft Store package

**Wanted:** offer Setupwise in the Microsoft Store.

**Proposal:** an **MSIX** package. Advantages: the Store signs MSIX packages for free (see R-15), updates come from
the Store, Partner Center registration is free. Details, verified facts and the maintainer checklist:
[`docs/store.md`](store.md).

**Step 1 – MSIX build (done, tested on Windows):**
- Built by `scripts/Build-Msix.ps1` (hand-written manifest + `MakeAppx.exe`): single-project MSIX is WinUI-only, and a
  `.wapproj` would break `dotnet build` on Linux. CI artifact `Setupwise-msix-ci-N`; `release.yml` builds
  `Setupwise-msix-vX.Y.Z` (not a release asset – unsigned until the Store signs it).
- Build flavour `Store` (`-p:SetupwiseFlavor=Store`, `AppFeatures` in Core): own update check and auto-updater (R-14)
  off, their settings hidden.
- `.setupwise` file association in the package manifest.
- *Run as administrator:* Microsoft does not accept Store apps that need elevation for any part of their
  functionality, and `allowElevation` (auto-elevation) is a restricted capability with strict approval. Whether a
  plain `runas` works from the package is not documented. So the Store build offers no elevation of its own
  (*Restart as administrator* and *Unlock security options* hidden); installers still ask via UAC themselves.
- `%APPDATA%`: new files go to a private per-package location (removed on uninstall); files that already exist in the
  real folder are read and changed there, so settings of an earlier installer version are picked up and shared.
  *Open log folder* opens the redirection target.

**Step 2 – Store submission (open, maintainer):** account, name reservation, listing, age rating, submission – see
the checklist in `docs/store.md`. The Store only accepts versions ≥ 1.0.0 (first number must not be 0), so the first
submission is 1.0.0.

Also worth it and much smaller: submit Setupwise to **winget-pkgs** itself (`winget install Setupwise`), which also
enables R-14 via winget.

## R-14 Optional auto-updater

**Wanted:** Setupwise updates itself, if the user turns it on.

**Proposal:** setting *"Install Setupwise updates automatically"* (off by default, not in the Store build). On start,
when the update check finds a new version: download the installer from the GitHub release, compare it with
`SHA256SUMS.txt`, ask once, then start it with `/SILENT` and close Setupwise. Without code signing (R-15) the
checksum only protects against broken downloads, not against a compromised release – this has to be said honestly in
`SECURITY.md`. Simpler alternative once Setupwise is in winget-pkgs: update itself with
`winget upgrade --id <Setupwise id>`, which gives winget's hash check and Microsoft's SmartScreen scan of the
manifest's installer. Effort M.

## R-15 Code signing

**Wanted:** solve signing somehow. SignPath was already rejected and is not an option.

| Option | Cost | What it gives | Catch |
|---|---|---|---|
| **Microsoft Store (MSIX)** | free | Microsoft signs the package, no SmartScreen warning, Store updates | Only for the Store package (R-13); installer/zip on GitHub stay unsigned. Publisher in the certificate is a Store id, not a personal name. |
| **Certum Open Source Code Signing** (cloud/SimplySign) | from about €49 for the first year | Signed installer and exe for GitHub downloads; builds SmartScreen reputation | Identity check as a private person; the **real name is part of the certificate** and visible to everyone. Key lives in Certum's cloud (signing via SimplySign, hard to automate in CI). Since Feb 2026 certificates last at most 459 days. |
| **Azure Artifact Signing** (formerly Trusted Signing) | about $10/month | Cheap, CI-friendly, Microsoft-issued certificate | Individuals only in the USA and Canada; in the EU only organisations. Not available to a private developer in Germany today. |
| **Commercial OV certificate** (DigiCert, Sectigo, …) | €200–500/year | Like Certum | Expensive, real name in the certificate, hardware token or cloud HSM required. |
| **Self-signed** | free | Nothing for users | Windows does not trust it; SmartScreen still warns. Not useful for public downloads. |
| **Unsigned + winget-pkgs** | free | Installs via winget are hash-checked and the manifest is reviewed/scanned by Microsoft | SmartScreen still warns when the exe is downloaded directly from GitHub. |

There is no legitimate shortcut around SmartScreen; warnings only go away with a trusted signature and reputation.

**Recommendation:** Store MSIX (R-13) + winget-pkgs. Both are free, keep the real name out of the certificate (which
fits the project rule of not publishing it) and cover the normal-user audience. Certum only if signed GitHub
downloads become important *and* publishing the real name is acceptable.

## R-16 License: GPL or MIT

Setupwise is GPL-3.0-or-later today.

| | GPL-3.0 (now) | MIT |
|---|---|---|
| Others may use, change and sell it | yes | yes |
| Changed versions that are distributed must publish their source | **yes** | no |
| Someone can repackage Setupwise closed-source, e.g. with adware/bundleware | no | **yes** |
| Usable in closed-source/commercial products | only if they become GPL | yes |
| Contributions from companies | sometimes harder (legal departments avoid GPL) | easier |
| Microsoft Store / winget-pkgs | allowed (VLC, Notepad++, Inkscape are GPL apps in both) | allowed |
| Dependencies (WPF-UI, CommunityToolkit.Mvvm: MIT) | compatible | compatible |
| Switching later | GPL → MIT needs consent of **every** contributor; easy only while the maintainer wrote (almost) all code | MIT → GPL is always possible for new versions |

For an installer tool the main risk is a fork that puts adware into the installers it ships – exactly what GPL
prevents (the source of such a fork must be published). Setupwise is an end-user app, not a library others embed,
so MIT's main advantage counts less.

**Recommendation:** keep **GPL-3.0-or-later**. If the license should change, it is easiest now, while there are no
outside contributors.

## R-17 Release policy

**Wanted:** a rule when to release – e.g. after how many changes.

**Proposal:** not a number of changes but the kind of change (SemVer, still 0.x):
- **Patch (0.3.1):** as soon as a bug fix is on `main` that users notice (crash, wrong install, broken page). Do not
  wait for other changes.
- **Minor (0.4.0):** when a coherent set of features is done and has been tested on Windows – roughly every 2–4 weeks,
  not more often than weekly (users get an update notification each time).
- **Beta (0.4.0-beta.1):** before a minor release with risky changes (e.g. R-11, R-12, R-13); testers opt in via the
  beta setting (R-03).
- **1.0:** when the Store package (R-13) is live and the features above are stable.
- Before each release: `[Unreleased]` in `CHANGELOG.md` is complete, CI is green, the CI screenshots were checked, and
  at least one real install and update ran on Windows.

---

## R-18 Catalog based on WinUtil

**Wanted:** take over the app categories and the app-to-category assignment of
[WinUtil](https://github.com/ChrisTitusTech/winutil) by Chris Titus Tech, keep Setupwise's own icons (loaded from the
vendor homepages) and credit WinUtil.

**Built:** `catalog/catalog.json` uses WinUtil's ten categories and contains all of its winget apps (256 apps; the two
`msstore:` entries – ChatGPT and WhatsApp – are left out because the catalog only takes winget ids). Apps Setupwise
already had keep their texts; the 19 apps WinUtil does not list were sorted into the closest category. All
descriptions are Setupwise's own, in English and German. Every id was checked against `microsoft/winget-pkgs`.
WinUtil is MIT-licensed: the license text is in the `catalog.json` header (embedded into the app) and in
`THIRD-PARTY-NOTICES.md`; *Settings → About* names WinUtil. `"hideInStore": true` keeps a few apps out of the Store
build (`AppCatalog.For(BuildFlavor)`).

**Later:** a small script that compares the catalog with WinUtil's current `applications.json` and lists new apps and
changed categories.

## R-19 Windows tweaks (installer build only)

**Wanted:** WinUtil's tweaks (privacy, Explorer and taskbar settings, services, cleanup) in Setupwise – but only in the
installer/zip build. The Store build stays an app installer and links to the GitHub release for the full version.

**Proposal:** a separate project `Setupwise.Tweaks` that only the Standard flavour references, so the Store package
contains no tweak code at all. Tweaks are data (registry values, services, commands) with an undo entry each, bundled
with the app and never downloaded at runtime. Every tweak needs admin rights, so it builds on R-11. Tweaks that turn off
security features (Defender, SmartScreen, UAC, Windows Update) stay out or need an explicit warning. WinUtil's
`config/tweaks.json` (MIT) can be the source; credit it like the catalog.

**Size:** L – needs real testing on Windows for each tweak and its undo.

**Built (step 1):** `catalog/tweaks.json` with 19 tweaks taken from WinUtil (privacy, Explorer & taskbar, system &
input) – registry values only, each with its undo value; nothing that weakens security, nothing that removes Windows
parts. Logic in `Setupwise.Core/Tweaks` (loader + validation, state from the registry, PowerShell script that sets
every value on its own and logs it, optional restore point, Explorer restart). Page *Windows tweaks* only in the
Standard flavour (`AppFeatures.Tweaks`); `tweaks.json` is only embedded into that build, so the Store package does
not contain the tweak list. Deviation from the proposal: no separate project – the engine is small, pure code in
Core; what matters for the Store is that no tweak data and no page ship there. One UAC prompt per run (none if only
HKCU values change). Known limit: when a standard user elevates with a different admin account, HKCU values land in
that account (same as WinUtil).

**Later:** services, scheduled tasks and script tweaks (e.g. disk cleanup, hibernation), presets ("recommended").

## R-20 Usability pass

**Wanted (maintainer):** clearer and more intuitive to use, so a normal user knows what to do where.

**Built:**
- **Per-app options in the context menu:** *Install for* (as in the settings / only for me / all users) and
  *Installation mode* (silent / with progress / with installer wizard). The choice is kept per app
  (`AppChoices` in `settings.json`), shown as a small pill on the card and used for every later install or update.
  Architecture and installer language stay settings only. The old one-off *Install with* submenu is gone.
- **Only when something failed:** *Try again* in the context menu of a failed app – with the same options, with the
  installer wizard (to see its error), with `--force` or with `--skip-dependencies`. The error message points there.
- **Skipped updates in the navigation:** an entry right below *Updates*, only visible while updates are skipped, with a
  count badge; *Show all again* and *Show update again* per app.
- **Start page:** welcome banner with *Browse apps* and *Take the tour*, "How it works" in three steps (with the number
  of apps, the selection and the available updates as links), then the profiles.
- **Welcome tour:** eight short steps that each show the page they talk about; starts by itself on the first start,
  *Skip* / Esc at any step, can be started again from the start page and *Settings → Advanced*.
- **Update without hash check** in the context menu of an update (and under *Try again*): `--ignore-security-hash`
  for that one run. winget only accepts it after `InstallerHashOverride` was enabled (admin); the tooltip says how.
- **German texts reworked** (maintainer feedback: partly awkward, partly misleading).
