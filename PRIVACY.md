# Privacy policy

*Last updated: 2026-09-30 · [Deutsche Fassung unten](#datenschutzerklärung)*

Setupwise has **no telemetry, no analytics, no account and no servers of its own.**
It does not send any information about you or your PC to the Setupwise project.

## Network connections

| When | To whom | What is transferred | Can be turned off |
|---|---|---|---|
| You search, install or update apps | Microsoft's winget source and the download servers of the respective app vendors (done by `winget`, part of Windows) | What winget needs to find and download the app | Only happens when you ask for it |
| Showing app icons | The website of the respective app vendor (e.g. `code.visualstudio.com`) | A normal web request for the homepage and the icon file, including your IP address | **Yes:** *Settings → Privacy → Load app icons* |
| At startup | GitHub (`api.github.com`) | A request for the latest Setupwise version, including your IP address | **Yes:** *Settings → Privacy → Check for Setupwise updates* |

The privacy policies of those services apply to these requests:
[Microsoft](https://privacy.microsoft.com/privacystatement), [GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement), and the websites of the app vendors.

## Data stored on your PC

- Settings: `%APPDATA%\Setupwise\settings.json`
- Logs and icon cache: `%LOCALAPPDATA%\Setupwise`
- Selections you save yourself (`*.setupwise` files)

This data never leaves your PC. You can delete the folders at any time; the uninstaller keeps them so that a reinstall remembers your settings.

---

## Datenschutzerklärung

Setupwise hat **keine Telemetrie, keine Nutzungsanalyse, kein Konto und keine eigenen Server.**
Es werden keine Informationen über dich oder deinen PC an das Setupwise-Projekt gesendet.

### Netzwerkverbindungen

| Wann | Zu wem | Was übertragen wird | Abschaltbar |
|---|---|---|---|
| Du suchst, installierst oder aktualisierst Programme | Die winget-Quelle von Microsoft und die Download-Server der jeweiligen Hersteller (erledigt `winget`, Teil von Windows) | Was winget braucht, um das Programm zu finden und herunterzuladen | Passiert nur auf deinen Wunsch |
| Anzeige von Programm-Icons | Die Website des jeweiligen Herstellers (z. B. `code.visualstudio.com`) | Eine normale Web-Anfrage nach Startseite und Icon, inklusive deiner IP-Adresse | **Ja:** *Einstellungen → Datenschutz → Programm-Icons laden* |
| Beim Start | GitHub (`api.github.com`) | Eine Anfrage nach der neuesten Setupwise-Version, inklusive deiner IP-Adresse | **Ja:** *Einstellungen → Datenschutz → Nach Setupwise-Updates suchen* |

Für diese Anfragen gelten die Datenschutzerklärungen der jeweiligen Dienste:
[Microsoft](https://privacy.microsoft.com/de-de/privacystatement), [GitHub](https://docs.github.com/de/site-policy/privacy-policies/github-general-privacy-statement) und die Websites der Hersteller.

### Auf deinem PC gespeicherte Daten

- Einstellungen: `%APPDATA%\Setupwise\settings.json`
- Protokolle und Icon-Cache: `%LOCALAPPDATA%\Setupwise`
- Auswahldateien, die du selbst speicherst (`*.setupwise`)

Diese Daten verlassen deinen PC nie. Du kannst die Ordner jederzeit löschen; die Deinstallation behält sie, damit eine Neuinstallation deine Einstellungen kennt.
