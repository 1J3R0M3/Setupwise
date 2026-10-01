# Setupwise – requirements and roadmap

Collected requirements for the next versions of Setupwise. These are **requirements, not issues**: each entry says
what is wanted, why, how it could be built and how big it is. Entries that are being worked on or are done carry a
status. The same entries are tracked as draft items on the
[GitHub Project "Setupwise roadmap"](https://github.com/users/1J3R0M3/projects/1) (see R-01).

Size: **S** = hours, **M** = a day or two, **L** = several days, needs design and real testing on Windows.

| ID | Requirement | Size | Status |
|---|---|---|---|
| R-01 | [Plan the work in a GitHub Project](#r-01-plan-the-work-in-a-github-project) | S | done |
| R-02 | [Bug: "New category" dialog opens a second time](#r-02-bug-new-category-dialog-opens-a-second-time) | S | done (unreleased) |
| R-03 | [Alpha/beta releases](#r-03-alphabeta-releases) | S | done (unreleased) |
| R-04 | [Install from the context menu, with options](#r-04-install-from-the-context-menu-with-options) | M | done (unreleased) |
| R-05 | [Console for own winget commands](#r-05-console-for-own-winget-commands) | M | done (unreleased) |
| R-06 | [Page "Installed apps"](#r-06-page-installed-apps) | M | done (unreleased) |
| R-07 | [Exclude apps from updates](#r-07-exclude-apps-from-updates) | M | done (unreleased) |
| R-08 | [German translation polish](#r-08-german-translation-polish) | S | first pass done, more examples welcome |
| R-09 | [More curated categories (Ninite-like)](#r-09-more-curated-categories-ninite-like) | M | done (unreleased) |
| R-10 | [Detect apps that are still running during an update](#r-10-detect-apps-that-are-still-running-during-an-update) | M | step 1 done (unreleased) |
| R-11 | [One admin prompt per run (like UniGetUI)](#r-11-one-admin-prompt-per-run-like-unigetui) | L | open |
| R-12 | [Optional Chocolatey support](#r-12-optional-chocolatey-support) | L | open |
| R-13 | [Microsoft Store package](#r-13-microsoft-store-package) | L | open |
| R-14 | [Optional auto-updater](#r-14-optional-auto-updater) | M | open |
| R-15 | [Code signing](#r-15-code-signing) | – | decision needed |
| R-16 | [License: GPL or MIT](#r-16-license-gpl-or-mit) | – | decision needed |
| R-17 | [Release policy](#r-17-release-policy) | – | proposal below |

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

**Proposal:** an **MSIX** package (Windows Application Packaging Project or single-project MSIX). Advantages: the
Store signs MSIX packages for free (see R-15), updates come from the Store, registration is free for individual
developers. Points to solve:
- Separate build flavour `Store`: the own update check and auto-updater (R-14) are off there.
- The file association for `.setupwise` moves to the package manifest.
- *Run as administrator* from a packaged app: check whether `runas` works for the full-trust package or whether the
  restricted capability `allowElevation` is needed (needs Store approval).
- `%APPDATA%` writes of packaged apps are redirected to the package folder – settings survive, but a later switch
  between the installer and the Store version does not migrate them automatically.
- Store listing (screenshots, privacy policy URL – `PRIVACY.md` exists), age rating questionnaire.

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
