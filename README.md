# NovaNote

**Kurze Beschreibung:** Leichter Windows-Texteditor mit AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check und Code-Folding — **optional mit lokaler KI-Anbindung**. Die KI läuft gegen einen eigenen Server (LM Studio, Ollama oder `llama-server`), es werden keine Daten in die Cloud geschickt und ohne Server bleibt NovaNote ein rein lokaler Editor.

Aktuelle Version: **0.5.0**  
GitHub Release: https://github.com/jsabrautzky-oss/NovaNote/releases/tag/v0.5.0

## Neu in 0.5.0
- **KI-Assistent im rechten Chat-Panel** (optional, standardmäßig geschlossen): Antworten, Suche, Zusammenfassung und Editor-Aktionen
- **Tool-Calls**: Die KI liest das Dokument selbst (`get_document`, `search_document`, `goto_line`, `select_lines`) und ändert es (`replace_all_text`, `replace_lines`, `insert_at_line`, `syntax_check`, `format_code`); der Dokumenttext wird nicht automatisch geschickt — das spart Tokens. Änderungen erst auf „Übernehmen“ oder automatisch
- **Anbieter-Verwaltung** mit Auto-Scan: Modelle, Kontextgröße, Vision und Tool-Support; Adresse und API-Key frei editierbar, API-Key per DPAPI verschlüsselt
- **Text-Fallback** für Modelle ohne native Tools (```nova```-Block)
- **Codeübernahme** aus Antwort-Codeblöcken, Multi-Turn-Chat, Abbrechen, Panel-Breite bleibt erhalten
- Dialog- und ComboBox-Fixes: Auswahl wird im geschlossenen Feld sichtbar, keine überlappenden Zeilen im KI-Einstellungsdialog

## Was kann NovaNote?
- Dateien bearbeiten: `.txt`, `.md`, `.markdown`, `.py`, `.pyw`, `.cs`, `.csx`, `.yaml`, `.yml`, `.json`, `.jsonc`, `.json5`, `.xml`, `.xsl`, `.xhtml`, `.svg`, `.html`, `.htm`, `.css`, `.js`, `.mjs`, `.ts`, `.cpp`, `.c`, `.h`, `.hpp`
- Syntax-Check mit roter Wellenlinie und `F4` für den nächsten Fehler
- Rechtsklick-Menü: Aktionen erscheinen nur, wenn sie passen (Kopieren/Ausschneiden nur mit Auswahl, Einfügen nur mit Zwischenablage, JSON-/Code-Formatierung, Python-Completion, Markdown-Vorschau je nach Sprache)
- Code-Folding (abschaltbar) und Whitespace-Anzeige
- Mehrfach-Tabs und Tabs in eigene Fenster lösen
- Session-Wiederherstellung: Fenster und Tabs beim nächsten Start wieder öffnen
- Suchen/Ersetzen, Gehe zu Zeile, Zoom, Word-Wrap, Line Numbers, Schriftart, `F5` für Datum/Uhrzeit
- JSON-Formatierung mit `Strg+Umschalt+J`, Code-Formatierung Python/C# mit `Strg+Umschalt+F`
- Encoding-Einstellungen: UTF-8, UTF-8 BOM, UTF-16 LE/BE, ANSI
- Zeilenenden: CRLF, LF, CR
- Drucken mit Seiteneinrichtung
- Themes: Hell, Dunkel, System
- Drag & Drop von Dateien
- Optional: lokaler KI-Assistent (siehe unten)

## Optionale lokale KI
Ohne KI nutzt NovaNote nur den Editor — das Panel ist zu, es laufen keine Anfragen.

1. Server starten, z. B. LM Studio (`http://localhost:1234/v1`), Ollama (`http://localhost:11434/v1`) oder
   `llama-server` auf einem anderen Rechner (z. B. `http://192.168.178.81:8081/v1`). Die Adresse ist frei editierbar.
2. NovaNote: Menü „KI → KI-Einstellungen…“ → Anbieter auswählen, Adresse prüfen, dann „Modelle scannen“.
3. Panel öffnen über „KI → KI-Panel“ oder `Strg+Alt+K`; aktives Modell und Anbieter stehen als Dropdown im Panel.
   Die Breite bleibt nach dem Zerren am Trenner gespeichert.
4. Die KI bekommt nur Dateiinfos (Name, Sprache, Zeilenzahl) als Systemprompt. Sie entscheidet selbst, ob sie über
   `get_document`/`search_document` im Dokument liest, und antwortet dann in Text oder ruft ein Bearbeitungs-Werkzeug auf.
5. Editor-Aktionen werden standardmäßig erst nach „Übernehmen“ ausgeführt; „KI → Aktionen automatisch anwenden“
   führt sie sofort aus. `Strg+Z` macht übernommene Änderungen rückgängig.
6. Modelle ohne native Tool-Aufrufe nutzen ein Text-Format (```nova```-Block), das NovaNote ebenfalls ausführt.
7. Der API-Key wird verschlüsselt (DPAPI, aktueller Benutzer) in `%APPDATA%\NovaNote\ki.json` gespeichert.
8. Manche Server (z. B. `llama-server`) melden Tool-Support nicht in `/v1/models`. NovaNote versucht Tool-Aufrufe trotzdem
   und setzt das „Tools“-Flag im Panel automatisch, sobald ein Aufruf akzeptiert wurde.

Die KI ist rein optional: ohne laufenden Server passiert nichts, NovaNote bleibt ein lokaler Editor.

## Unterstützte Sprachen
| Sprache | Syntax-Highlighting | Syntax-Check | Code-Folding | Hinweis |
|---|---:|---:|---:|---|
| Plain Text | ❌ | ❌ | ❌ | kein Sprachmodus |
| Markdown | ✓ | ❌ | ❌ | Live-Vorschau für `.md`/`.markdown` mit MdXaml |
| Python | ✓ | ✓ | ❌ | Live-Syntax/Diagnosen über LSP-Pull/CLI, `.` / `Strg+Leertaste` für Completion, Formatierung |
| C# | ✓ | ✓ | ✓ | Syntax-Check mit Roslyn, Formatierung mit `Strg+Umschalt+F` |
| YAML | ✓ | ✓ | ✓ | Syntax-Check mit YamlDotNet, Folding nach Einrückung |
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
- Dateiendungen `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` werden nach dem Installer-Setup mit NovaNote verknüpft
- Große Dateien bleiben nutzbar durch Limits für Syntax-Check/Folding
- Portable-Version ohne Registry-Einträge möglich
- KI läuft lokal, keine Cloud-Dienste nötig

## Installationsoptionen
1. **Velopack-Installer**  
   Empfohlen für normale Nutzung. Installiert NovaNote für den aktuellen Benutzer, erstellt Shortcuts und verknüpft `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` mit NovaNote. Andere Dateien öffnest du über „Öffnen…“ oder per Kommandozeile.
2. **Portable-Zip**  
   Ohne Installation: ZIP entpacken und `NovaNote.exe` starten. Keine Registry-Einträge durch den Installer.
3. **Direkter Start mit Datei**  
   `NovaNote.exe datei.txt` öffnet die Datei. Nach dem Installer-Setup kann eine verknüpfte Datei per Doppelklick geöffnet werden.

## Installation mit Installer
1. `NovaNote-win-Setup.exe` von der GitHub-Release-Seite herunterladen.
2. Die Datei ausführen.
3. NovaNote über Desktop- oder Startmenü-Shortcut starten.
4. Optional: `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` per Doppelklick mit NovaNote öffnen.

Direkter Installer-Download:  
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.5.0/NovaNote-win-Setup.exe

## Installation als Portable-Version
1. `NovaNote-win-Portable.zip` von der GitHub-Release-Seite herunterladen.
2. Das ZIP in einen Ordner entpacken.
3. `NovaNote.exe` starten.

Direkter Portable-Download:  
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.5.0/NovaNote-win-Portable.zip

## Updates
- NovaNote kann beim Start automatisch prüfen, ob auf GitHub ein neuer Release mit `NovaNote-win-Setup.exe` vorhanden ist.
- Wenn ein Update gefunden wird, fragt NovaNote, ob das Setup heruntergeladen und ausgeführt wird.
- Die automatische Update-Prüfung kann im Menü „Hilfe → Update → Automatisch auf Updates prüfen“ deaktiviert werden.
- „Über NovaNote“ enthält einen Button „Update prüfen“, der einen sofortigen Update-Check ausführt.

## Voraussetzungen
- 64-bit Windows 10 oder Windows 11
- Internetzugang für Download und spätere Updates (nicht für die lokale KI)
- Genügend Speicherplatz für die App und entpackte Portable-Version
- Kein Administrator für die Benutzer-Installation nötig
- Kein separate .NET-Installation nötig
- Für die KI: ein lokaler OpenAI-kompatibler Server (LM Studio, Ollama oder `llama-server`)

## Systeme
NovaNote läuft für:
- Windows 10 x64
- Windows 11 x64

Es gibt aktuell keine Linux-, macOS- oder ARM-/32-bit-Versionen.

## Erstellt mit lokaler KI
- Opencode Desktop
- Strata 0.2.2 · Engine 0.1.47
- Model: Qwen3.8-Flash-Next Gyro-S
