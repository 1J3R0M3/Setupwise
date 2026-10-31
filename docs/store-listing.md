# Microsoft Store listing texts

Texts for the Store listings in Partner Center (*Store listings → German / English*). Keep them in sync with the app:
numbers (apps, categories, profiles) come from `catalog/catalog.json`. Policy 10.2.4 requires the winget dependency at
the **beginning** of the description. Rules and checklist: [store.md](store.md).

Screenshots: rendered locally with `SETUPWISE_STORE_SCREENSHOTS=<icon cache folder>` and
`dotnet test --project tests/Setupwise.App.UiTests --filter-class Setupwise.App.UiTests.StoreScreenshots`
→ `artifacts/store-screenshots` (2160 × 1350 PNG, de/en × light/dark). Upload order: start, selection, category,
updates, search, installed.

---

## Deutsch (Deutschland)

### Beschreibung

```
Setupwise benötigt winget (Windows-Paketverwaltung), das als „App-Installer“ in Windows 10 und 11 enthalten ist. Fehlt winget, zeigt Setupwise den Weg zum App-Installer im Microsoft Store.

Neuer PC, alles neu installieren? Setupwise richtet deinen Windows-PC in wenigen Minuten ein: Programme aussuchen, auf „Installieren“ klicken, fertig. Kein Durchklicken durch Dutzende Installer, keine Suche nach Download-Seiten, keine Toolbars.

So funktioniert es:
• Wähle ein Profil wie „Grundausstattung“, „Homeoffice“ oder „Gaming-PC“ – oder stelle dir deine Auswahl aus über 80 bewährten Programmen in 11 Kategorien zusammen.
• Setupwise installiert alles nacheinander über winget, direkt aus den offiziellen Quellen der Hersteller, und zeigt dir den Fortschritt jedes Programms.
• Fehlt etwas im Katalog? Durchsuche den gesamten winget-Katalog mit tausenden Programmen.

Auch danach nützlich:
• Updates: Setupwise zeigt, für welche installierten Programme es neue Versionen gibt, und aktualisiert sie auf Wunsch alle auf einmal.
• Übersicht aller installierten Programme.
• Programme von Updates ausnehmen oder eine einzelne Version überspringen.
• Deine Auswahl als Datei speichern und auf dem nächsten PC wieder laden.
• Eigene Kategorien anlegen.

Für Neugierige: Installationsoptionen wie stille Installation, nur für mich oder für alle Benutzer, Architektur und Sprache des Installers; eine Konsole für eigene winget-Befehle und ein ausführliches Protokoll.

Setupwise ist kostenlos und quelloffen (GPL-3.0), ohne Werbung, ohne Konto und ohne Telemetrie. Oberfläche auf Deutsch und Englisch, mit hellem und dunklem Design.
```

### Produktfeatures

```
Programme aus über 80 bewährten Apps in 11 Kategorien auswählen
Profile wie Grundausstattung, Homeoffice, Gaming-PC, Creator und Entwickler
Alles mit einem Klick über winget installieren – aus den offiziellen Quellen der Hersteller
Den gesamten winget-Katalog durchsuchen
Updates für alle installierten Programme anzeigen und auf einmal installieren
Programme von Updates ausnehmen oder eine Version überspringen
Auswahl als Datei speichern und auf dem nächsten PC laden
Eigene Kategorien anlegen
Installationsoptionen: still, nur für mich oder für alle Benutzer, Architektur, Sprache
Kostenlos und quelloffen, ohne Werbung, ohne Konto, ohne Telemetrie
```

### Kurzbeschreibung (max. 270 Zeichen)

```
Neuen PC in Minuten einrichten: Programme aus einem kuratierten Katalog oder Profilen auswählen und mit einem Klick über winget installieren und aktualisieren. Kostenlos, quelloffen, ohne Werbung und ohne Telemetrie.
```

### Schlüsselwörter (max. 7)

```
winget
Programme installieren
PC einrichten
Software aktualisieren
Paketverwaltung
neuer PC
App-Installer
```

### Urheberrecht- und Markeninformationen

```
Copyright © 2026 Setupwise contributors. Lizenz: GPL-3.0-or-later.
```

### Zusätzliche Lizenzbedingungen

```
Setupwise ist freie Open-Source-Software unter der GNU General Public License, Version 3 oder (nach Ihrer Wahl) jeder späteren Version (GPL-3.0-or-later). Sie dürfen das Programm unter den Bedingungen dieser Lizenz nutzen, untersuchen, weitergeben und verändern. Es gibt keine Kosten, kein Abo, keine In-App-Käufe und keine Werbung.

Quellcode: https://github.com/1J3R0M3/Setupwise
Lizenztext: https://www.gnu.org/licenses/gpl-3.0.html

Setupwise wird, soweit gesetzlich zulässig, OHNE JEDE GEWÄHRLEISTUNG bereitgestellt. Die Programme, die Sie mit Setupwise installieren, stammen von den jeweiligen Herstellern über winget (Windows-Paketverwaltung) und unterliegen deren eigenen Lizenzbedingungen.
```

### Entwickelt von

```
1J3R0M3
```

---

## English (United States)

### Description

```
Setupwise requires winget (Windows Package Manager), which comes with Windows 10 and 11 as "App Installer". If winget is missing, Setupwise shows you the way to App Installer in the Microsoft Store.

New PC, everything to install again? Setupwise sets up your Windows PC in minutes: pick your apps, click "Install", done. No clicking through dozens of installers, no hunting for download pages, no toolbars.

How it works:
• Pick a profile such as "Essentials", "Home office" or "Gaming PC" – or build your own selection from more than 80 proven apps in 11 categories.
• Setupwise installs everything one after another with winget, straight from the vendors' official sources, and shows the progress of each app.
• Missing something in the catalog? Search the whole winget catalog with thousands of apps.

Useful afterwards, too:
• Updates: Setupwise shows which installed apps have a newer version and updates all of them at once if you like.
• An overview of all installed apps.
• Exclude apps from updates or skip a single version.
• Save your selection to a file and load it on your next PC.
• Create your own categories.

For the curious: install options such as silent install, just for me or for all users, architecture and installer language; a console for your own winget commands and a detailed log.

Setupwise is free and open source (GPL-3.0), with no ads, no account and no telemetry. Available in English and German, with light and dark theme.
```

### Product features

```
Pick from more than 80 proven apps in 11 categories
Profiles: Essentials, Home office, Gaming PC, Creator and Developer
Install everything with one click via winget – from the vendors' official sources
Search the whole winget catalog
See updates for all installed apps and install them at once
Exclude apps from updates or skip a version
Save your selection to a file and load it on your next PC
Create your own categories
Install options: silent, just for me or all users, architecture, language
Free and open source, no ads, no account, no telemetry
```

### Short description (max. 270 characters)

```
Set up a new PC in minutes: pick apps from a curated catalog or profiles and install and update them with one click via winget. Free, open source, no ads and no telemetry.
```

### Keywords (max. 7)

```
winget
install apps
PC setup
software updates
package manager
new PC
app installer
```

### Copyright and trademark info

```
Copyright © 2026 Setupwise contributors. Licensed under GPL-3.0-or-later.
```

### Additional license terms

```
Setupwise is free and open-source software, licensed under the GNU General Public License, version 3 or (at your option) any later version (GPL-3.0-or-later). You may use, study, share and modify it under the terms of that license. There is no fee, no subscription, no in-app purchase and no advertising.

Source code: https://github.com/1J3R0M3/Setupwise
Full license text: https://www.gnu.org/licenses/gpl-3.0.html

Setupwise comes with ABSOLUTELY NO WARRANTY, to the extent permitted by applicable law. The apps you choose to install with Setupwise are provided by their respective vendors through winget (Windows Package Manager) and are subject to their own license terms.
```

### Developed by

```
1J3R0M3
```
