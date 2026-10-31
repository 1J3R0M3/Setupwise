# Microsoft Store package (R-13)

Setupwise is also built as an **MSIX package** for the Microsoft Store. This page describes how the Store build
differs, how to build and test it, what was verified in Microsoft's documentation, and what the maintainer has to do
in Partner Center. Requirement: [R-13](requirements.md#r-13-microsoft-store-package).

All facts below were checked on learn.microsoft.com on 2026-10-31 (links in [Sources](#sources)). Things the
documentation does not answer are marked **not documented – test on Windows**.

## What is different in the Store build

The Store build is the normal app published with `-p:SetupwiseFlavor=Store`. The flavour is stored as assembly
metadata and read by `AppPaths.Flavor`; `AppFeatures` (in `Setupwise.Core/Distribution`) decides what is on.

| | Installer / zip (Standard) | Store (MSIX) |
|---|---|---|
| Own update check (GitHub Releases API) | yes, can be turned off | **off**, the setting is hidden – the Store updates the app |
| Auto-updater (R-14, not built yet) | planned | **off** (`AppFeatures.AutoUpdate`) |
| *Restart as administrator* | yes | **hidden**, see [Elevation](#run-as-administrator-runas) |
| *Unlock security options* (elevated `winget settings --enable`) | yes | **hidden**, same reason; installers that need admin rights still show their own UAC prompt |
| App catalog | all apps | without apps marked `"hideInStore": true` in `catalog.json` (tools that change Windows itself or replace other vendors' clients, e.g. MSEdgeRedirect, OFGB); `AppCatalog.For(flavor)` |
| `.setupwise` file association | registry, by Inno Setup | package manifest (`uap3:FileTypeAssociation`) |
| Settings, categories, logs, icon cache | `%APPDATA%` / `%LOCALAPPDATA%` | same paths, but redirected by Windows, see [AppData](#appdata-redirection) |
| *Open log folder* | opens the folder | opens the redirection target, because File Explorer does not see redirected files |
| Signing | unsigned (R-15) | the Store signs the package |
| Version in *Settings → About* | `0.3.0` | `0.3.0 (Microsoft Store)` |

## Build and test

```powershell
pwsh ./scripts/Build-Msix.ps1                    # artifacts/msix/Setupwise-<version>-x64.msix + layout/
pwsh ./scripts/New-MsixAssets.ps1                # only when the app icon changed: regenerates the logos
```

`Build-Msix.ps1` needs Windows with the Windows SDK (`MakeAppx.exe`, `MakePri.exe`). It publishes the Store flavour
self-contained for x64, fills in `installer/msix/AppxManifest.xml` (identity from `installer/msix/identity.json`),
indexes the logos into `resources.pri` and packs everything with `MakeAppx.exe pack`.

**CI:** the `msix` job in `ci.yml` uploads the artifact `Setupwise-msix-ci-N` (the `.msix` and the unpacked
`layout/` folder); the UI test also runs against the Store build and uploads `screenshots-store-N`. `release.yml`
builds the MSIX for every tag as workflow artifact `Setupwise-msix-vX.Y.Z` – it is **not** attached to the GitHub
release, because an MSIX that is not signed cannot be installed by users anyway.

**Test on Windows (without signing):** with *Developer Mode* on (Settings → System → For developers), register the
unpacked folder from the CI artifact:

```powershell
Add-AppxPackage -Register .\layout\AppxManifest.xml   # installs "Setupwise" into the Start menu
Get-AppxPackage 1J3R0M3.Setupwise | Remove-AppxPackage # uninstall again
```

Put `layout` in a folder with a **short path** (e.g. `C:\Temp\swmsix`): from a deep folder the registration failed
with `0x80073CF0` / `0x80070003` ("path not found"). The package runs from that folder, so keep it until the package
is removed again.

`-Register` "registers an application in development mode … from a folder of unpackaged files". The `.msix` itself
can only be installed after signing; the Store does that. Before a submission, run the **Windows App Certification
Kit** on the `.msix` (Microsoft recommends it to find manifest and image problems).

### Why MakeAppx and not a packaging project

- *Single-project MSIX* only supports WinUI 3 apps (Windows App SDK) – Setupwise is WPF.
- The *Windows Application Packaging Project* (`.wapproj`) needs Visual Studio's MSIX tooling; added to
  `Setupwise.slnx` it would break `dotnet build` on Linux (CI and development).
- Packaging "manually" with a hand-written manifest and `MakeAppx.exe` is a documented way for Win32/WPF apps and
  only needs the Windows SDK, which the `windows-latest` runner has.

`MakeAppx.exe` cannot create `.msixupload` files; Partner Center accepts `.msix` too (without symbols, so no crash
analytics in Partner Center).

## Findings

### Run as administrator (`runas`)

- Microsoft's packaging checklist: *"apps that require elevation for any part of their functionality won't be
  accepted into the Store."*
- `allowElevation` is a **restricted capability** for apps that *"depend on auto-elevation, either at launch or
  during runtime"*. For Store submissions it *"is subject to approval under strict criteria"*; Microsoft asks to
  contact `reportapp@microsoft.com` in advance with a justification.
- Whether a user-started `ShellExecute` with verb `runas` on the packaged `Setupwise.exe` works **without**
  `allowElevation` is **not documented – test on Windows**. Only community answers exist, and they contradict each
  other.

**Decision in the Store build:** Setupwise runs as the normal user (`asInvoker`, as before) and offers no elevation of
its own. `allowElevation` is not declared. Installs still work: winget starts each installer, and installers that need
admin rights show their own UAC prompt – that is the normal behaviour of the Standard build when it is not elevated.
If a Windows test shows that `runas` works from the package and the Store accepts it, switching
`AppFeatures.SelfElevation` back on is a one-line change.

### AppData redirection

For packaged desktop apps on Windows 10 1903 and later (documented in *Understanding how packaged desktop apps run on
Windows*):

- **New** files and folders under `AppData\Local` and `AppData\Roaming` are written to a private per-user,
  per-package location and merged back into the real path – for the app itself.
- When the app opens a file, Windows first looks in the private location, then in the real `AppData`. A file opened
  from the real location is **not** virtualized: changes go to the real file.
- On uninstall, the redirected files are removed.

What this means for Setupwise (`%APPDATA%\Setupwise\settings.json`, `categories.json`,
`%LOCALAPPDATA%\Setupwise\{Logs,IconCache}`):

| Situation | Result |
|---|---|
| Only the Store version was ever used | Settings live in the private location and are removed on uninstall. A later installer version starts with defaults. |
| The installer version ran before | The Store version finds the existing `settings.json` / `categories.json` in the real folder and changes them there – both versions share these two files. New files (new log days, new icons) are private. |
| User opens the log folder | File Explorer runs outside the package and only sees the real folder. Setupwise therefore asks Windows for the redirection target (`SHGetKnownFolderPath` with `KF_FLAG_RETURN_FILTER_REDIRECTION_TARGET`, documented for exactly this) and opens that folder if it exists. |

The capability `unvirtualizedResources` could turn redirection off, but Microsoft says it is meant for certain games
and packages with external location and *"is not intended to be used for other scenarios"* – not used.

**Test on Windows:** where `settings.json` really lands on a fresh install, and that *Open log folder* shows today's
log.

### File association

`installer/msix/AppxManifest.xml` declares `uap3:FileTypeAssociation Name="setupwise"` for `.setupwise` with
`Parameters="&quot;%1&quot;"`; Setupwise reads the path from the command line as before. Microsoft's docs ask to quote
`%1` because paths may contain spaces. The name must be lower case. The installer's ProgID `Setupwise.Selection` is not
migrated (`rescap3:MigrationProgIds` would allow that; left out to keep the manifest small).
**Test on Windows:** double-click on a `.setupwise` file opens it in the Store version.

### Package version

The Store requires `Major.Minor.Build.0`: the fourth number is reserved for the Store, and **the first number must
not be 0**. So `0.x` versions can be built and tested, but **not submitted** – the first Store submission has to be
**1.0.0** or later. `Build-Msix.ps1` warns about this. R-17 plans 1.0 "when the Store package is live"; in practice
1.0.0 is the version that goes to the Store (see the checklist).

### Store policies that matter for Setupwise (version 7.20)

- **10.2.3** – a product *"must not offer to install secondary software that is not developed by you and does not
  enhance the functionality of your product"*. Installing apps the user picks is Setupwise's core function, not
  bundled software – say so in the certification notes.
- **10.2.4** – a dependency on other software must be disclosed *"at the beginning of the description"*: Setupwise
  needs **winget (App Installer)**.
- **10.2.2** – no downloading and running of code that changes the described functionality. Setupwise only runs
  winget with the user's choices.
- **10.5.1** – a privacy policy is required if the product accesses, collects or transmits personal information.
  Setupwise's icon and winget requests transmit the user's IP address, so provide `PRIVACY.md` as privacy policy URL.
- **11.11** – an age rating via the IARC questionnaire is required.
- **10.2.9** (EXE/MSI submission instead of MSIX) requires an installer signed with a CA certificate – not possible
  without code signing (R-15), so MSIX is the only way for Setupwise.

## Maintainer checklist

These steps need the maintainer's account and cannot be done by Claude.

1. **Choose the account type – before registering.** Since the new onboarding there are **no registration fees** for
   individual and company accounts. Start at <https://storedeveloper.microsoft.com> (only this entry point uses the
   free flow).
   - **Individual:** Microsoft account + identity check with a government ID and a selfie. Microsoft: *"Choose an
     Individual account if you want to publish apps under your own name."* In practice (2026-10-31) the account's
     publisher display name is **`1J3R0M3`**, not the real name – Partner Center checks that the package's
     `PublisherDisplayName` matches it exactly.
   - **Company:** needs a registered business (D-U-N-S number or official documents) and a work e-mail address on the
     company's domain.
2. **Reserve the name** "Setupwise" in Partner Center (Apps and games → New product → MSIX or PWA app).
3. ~~**Send the product identity to Claude**~~ – done 2026-10-31 (`1J3R0M3.Setupwise`): Product management → Product identity → `Package/Identity/Name`,
   `Package/Identity/Publisher` (`CN=…`) and `Package/Properties/PublisherDisplayName`. They go into
   `installer/msix/identity.json` (they are public in every Store package). A package built with other values is
   rejected on upload (*"PublisherDisplayName … does not match your publisher display name"*).
4. **Release 1.0.0** (see [Package version](#package-version)); the `release.yml` run for `v1.0.0` produces the
   artifact `Setupwise-msix-v1.0.0`. Run the Windows App Certification Kit on it.
5. **Start the submission** and fill in:
   - *Pricing and availability:* free, markets (default: all), public, visible in the Store.
   - *Properties:* category (e.g. *Utilities & tools*); privacy policy URL
     `https://github.com/1J3R0M3/Setupwise/blob/main/PRIVACY.md`; website `https://github.com/1J3R0M3/Setupwise`.
   - *Age ratings:* answer the IARC questionnaire (no user content, no purchases, no ads; the app downloads other
     apps from winget only on the user's request).
   - *Packages:* upload the `.msix`. The warning *"restricted capabilities require approval: runFullTrust"* is
     expected; the justification goes into *Submission options*.
   - *Store listings* (English and German): description that **starts** with the winget/App Installer requirement,
     at least 1 screenshot (4+ recommended, PNG, desktop 1366 × 768 or larger, max. 10) – the CI screenshots
     (`screenshots-store-N`, 1:1 size) are a starting point; a 300 × 300 app tile icon is strongly recommended
     (`assets/setupwise-512.png` scaled).
   - *Submission options → Restricted capabilities:* explain `runFullTrust`, e.g. *"Setupwise is a classic WPF
     desktop app (.NET) packaged as a full-trust desktop app. It needs runFullTrust to run as a desktop process and to
     start winget (Windows Package Manager), which installs and updates the apps the user selects."*
     *Notes for certification:* Setupwise installs the apps the user selects via winget, bundles no software, needs
     no elevation; how to test (pick an app, *Install*).
6. **Submit for certification** and tell Claude the result. When the app is live: R-13 → done, roadmap board → *Done*.

## Only testable on Windows or in Partner Center

**Checked on 2026-10-31** (Windows 11, CI build `0.3.0-ci.18`, registered with `Add-AppxPackage -Register`, no
installer version on the PC):
- Starts from the Start menu entry; the log says `Store, packaged: True`. winget works from inside the package
  (version, list of installed apps, available updates).
- AppData redirection as documented: the real `%APPDATA%\Setupwise` and `%LOCALAPPDATA%\Setupwise` were not created;
  `settings.json`, logs and icon cache are in
  `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalCache\{Roaming,Local}\Setupwise`.
- Opening a `.setupwise` file (path with a space) starts the packaged Setupwise with the quoted path as argument.

**Still open:**
- Real winget installs and updates from the packaged app.
- *Open log folder* (should open the `LocalCache` folder above), the imported selection on the page *Your selection*.
- Whether `runas` works from the package (only relevant if self-elevation should come back).
- Sharing settings with an installed installer version.
- The logos in Start menu, taskbar and *Open with*.
- Windows App Certification Kit result.
- Everything in Partner Center: name reservation, package validation (identity, version), certification, IARC rating,
  how the publisher name is shown.

## Sources

- [Understanding how packaged desktop apps run on Windows](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-behind-the-scenes) – AppData, registry, uninstall
- [Prepare to package a desktop application](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-prepare) – elevation and Store acceptance
- [App capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations) – `runFullTrust`, `allowElevation`, `unvirtualizedResources`
- [KNOWN_FOLDER_FLAG](https://learn.microsoft.com/windows/win32/api/shlobj_core/ne-shlobj_core-known_folder_flag) – `KF_FLAG_RETURN_FILTER_REDIRECTION_TARGET`
- [GetCurrentPackageFamilyName](https://learn.microsoft.com/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefamilyname)
- [Generating MSIX package components](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-manual-conversion), [MakeAppx.exe](https://learn.microsoft.com/windows/msix/package/create-app-package-with-makeappx-tool)
- [Single-project MSIX](https://learn.microsoft.com/windows/apps/windows-app-sdk/single-project-msix), [Packaging project](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-packaging-dot-net)
- [Application element](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-application), [uap3:FileTypeAssociation](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-uap3-filetypeassociation), [Packaging extensions](https://learn.microsoft.com/windows/apps/desktop/modernize/desktop-to-uwp-extensions)
- [App package requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements) – signing, version numbers, package types
- [Microsoft Store Policies 7.20](https://learn.microsoft.com/windows/apps/publish/store-policies)
- [Open a developer account](https://learn.microsoft.com/windows/apps/publish/partner-center/open-a-developer-account) – fees, account types
- [Create an app submission](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/create-app-submission), [Screenshots and images](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [Add-AppxPackage](https://learn.microsoft.com/powershell/module/appx/add-appxpackage) – `-Register`
