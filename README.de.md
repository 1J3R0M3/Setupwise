<p align="center">
  <img src="assets/setupwise-256.png" width="96" alt="Setupwise-Logo">
</p>

<h1 align="center">Setupwise</h1>

<p align="center">
  <b>Deinen Windows-PC in Minuten einrichten.</b><br>
  Profil wählen, gewünschte Programme anhaken, auf <i>Installieren</i> klicken – fertig.
</p>

<p align="center"><a href="README.md">English version</a></p>

---

Setupwise ist eine freundliche Windows-App für alle, die PCs einrichten – den eigenen, den der Familie oder die im Büro. Programme werden über **winget** installiert und aktualisiert, den Paketmanager, der in Windows eingebaut ist. Jede App kommt also direkt aus der offiziellen Quelle.

## Funktionen

- **Profile** – *Grundausstattung*, *Homeoffice*, *Gaming-PC*, *Creator*, *Entwickler*: ein Klick wählt alles aus, was zusammengehört.
- **Kuratierter Katalog** – beliebte Programme in Kategorien, mit Beschreibungen auf Deutsch und Englisch.
- **Suche** – findet jedes der tausenden Programme im winget-Katalog.
- **Updates** – zeigt, welche installierten Programme veraltet sind, und aktualisiert sie in einem Rutsch.
- **Einrichtung mitnehmen** – Auswahl als `.setupwise`-Datei speichern und auf dem nächsten PC öffnen.
- **Windows-11-Look** – Mica, heller und dunkler Modus, echte Programm-Icons.
- **Datenschutzfreundlich** – keine Telemetrie, kein Konto. Icons kommen von der Website des jeweiligen Herstellers (abschaltbar).

## Installation

1. **`Setupwise-x.y.z-Setup-x64.exe`** aus dem [neuesten Release](https://github.com/1J3R0M3/Setupwise/releases/latest) herunterladen.
2. Ausführen und dem Assistenten folgen. Adminrechte sind nicht nötig.

Lieber ohne Installation? Dann die **portable ZIP** von derselben Seite nehmen.

**Voraussetzungen:** Windows 10 (1809) oder Windows 11, x64, sowie der *App-Installer* aus dem Microsoft Store (auf aktuellen Windows-Versionen vorinstalliert; Setupwise meldet sich, falls er fehlt).

> **Hinweis:** Die Releases sind nicht signiert, daher kann Windows SmartScreen warnen. Dann *Weitere Informationen → Trotzdem ausführen* wählen. Den Download kannst du mit `SHA256SUMS.txt` prüfen.

## Mitmachen

Beiträge sind sehr willkommen – besonders **neue Programme für den Katalog** und **Übersetzungen**. Dafür braucht man keine C#-Kenntnisse. Siehe [CONTRIBUTING.md](CONTRIBUTING.md) (auf Englisch).

## Datenschutz

Keine Telemetrie, kein Konto. Setupwise kontaktiert nur die Websites der Hersteller (für Icons) und GitHub (für die Update-Prüfung) – beides abschaltbar. Siehe [Datenschutzerklärung](PRIVACY.md#datenschutzerklärung).

## Lizenz

Setupwise ist freie Software unter der [GNU General Public License v3.0](LICENSE) oder neuer.
Setupwise steht in keiner Verbindung zu Microsoft. Die installierten Programme stammen von ihren jeweiligen Herstellern und unterliegen deren Lizenzen.
