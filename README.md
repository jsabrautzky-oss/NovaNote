# NovaNote

NovaNote ist ein leichter Windows-Texteditor mit AvalonEdit, Themes, Mehrfach-Tabs und Syntax-Check. Aktuell als öffentliche Beta `0.1.0-beta` auf GitHub verfügbar.

GitHub Release:  
https://github.com/jsabrautzky-oss/NovaNote/releases/tag/v0.1.0-beta

## Was kann NovaNote?
- Dateien bearbeiten: `.txt`, `.json`, `.xml`, `.html`, `.css`, `.js`, `.cpp`
- Mehrfach-Tabs und Tabs in eigene Fenster lösen
- Session-Wiederherstellung: Fenster und Tabs beim nächsten Start wieder öffnen
- Suchen/Ersetzen, Gehe zu Zeile, Zoom, Word-Wrap, Line Numbers
- Encoding-Einstellungen: UTF-8, UTF-8 BOM, UTF-16 LE/BE, ANSI
- Zeilenenden: CRLF, LF, CR
- Drucken mit Seiteneinrichtung
- Themes: Hell, Dunkel, System
- Drag & Drop von Dateien

## Unterstützte Sprachen
| Sprache | Syntax-Highlighting | Syntax-Check | Code-Folding | Hinweis |
|---|---:|---:|---:|---|
| Plain Text | ❌ | ❌ | ❌ | kein Sprachmodus |
| JSON | ✓ | ✓ | ✓ | mit roter Fehlermarkierung und `F4` |
| XML | ✓ | ✓ | ✓ | mit roter Fehlermarkierung und `F4` |
| HTML | ✓ | ✓ | ✓ | mit roter Fehlermarkierung und `F4` |
| CSS | ✓ | ✓ | ✓ | mit roter Fehlermarkierung und `F4` |
| JavaScript | ✓ | ✓ | ✓ | Grundprüfung mit Acornima, nicht vollständig |
| C++ | ✓ | ✓ | ✓ | Grundprüfung für Klammern/Kommentare, kein Compiler-Check |
| TypeScript | ✓ als JavaScript | ❌ | ✓ als JavaScript | TypeScript wird erkannt, aber nicht geprüft |

Hinweis: Weitere Sprachen sollen später folgen.

## Vorteile
- Modernes, minimalistisches Windows-UI
- Kein separates .NET nötig: die App wird selbst-contained ausgeliefert
- Benutzer-Installation ohne Administratorrechte möglich
- Dateiendungen werden nach Installer-Setup mit NovaNote verknüpft
- Große Dateien bleiben nutzbar durch Limits für Syntax-Check/Folding
- Portable-Version ohne Registry-Einträge möglich

## Installationsoptionen
1. **Velopack-Installer**  
   Empfohlen für normale Nutzung. Installiert NovaNote für den aktuellen Benutzer, erstellt Shortcuts und verknüpft `.txt`, `.json`, `.xml`, `.html` mit NovaNote.
2. **Portable-Zip**  
   Ohne Installation: ZIP entpacken und `NovaNote.exe` starten. Keine Registry-Einträge durch den Installer.
3. **Direkter Start mit Datei**  
   `NovaNote.exe datei.txt` öffnet die Datei. Nach dem Installer-Setup kann eine verknüpfte Datei per Doppelklick geöffnet werden.

## Installation mit Installer
1. `NovaNote-win-Setup.exe` von der GitHub-Release-Seite herunterladen.
2. Die Datei ausführen.
3. NovaNote über Desktop- oder Startmenü-Shortcut starten.
4. Optional: `.txt`, `.json`, `.xml`, `.html` per Doppelklick mit NovaNote öffnen.

Direkter Installer-Download:  
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.1.0-beta/NovaNote-win-Setup.exe

## Installation als Portable-Version
1. `NovaNote-win-Portable.zip` von der GitHub-Release-Seite herunterladen.
2. Das ZIP in einen Ordner entpacken.
3. `NovaNote.exe` starten.

Direkter Portable-Download:  
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.1.0-beta/NovaNote-win-Portable.zip

## Voraussetzungen
- 64-bit Windows 10 oder Windows 11
- Internetzugang für Download und spätere Updates
- Genügend Speicherplatz für die App und entpackte Portable-Version
- Kein Administrator für die Benutzer-Installation nötig
- Kein separate .NET-Installation nötig

## Systeme
NovaNote läuft für:
- Windows 10 x64
- Windows 11 x64

Es gibt aktuell keine Linux-, macOS- oder ARM-/32-bit-Versionen.
