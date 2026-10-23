# NovaNote

**Kurze Beschreibung:** Leichter Windows-Texteditor mit AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check und Code-Folding — **optional mit lokaler KI-Anbindung**. Die KI läuft gegen einen eigenen Server (LM Studio, Ollama oder `llama-server`), es werden keine Daten in die Cloud geschickt und ohne Server bleibt NovaNote ein rein lokaler Editor.

Aktuelle Version: **0.6.1**  
GitHub Release: https://github.com/jsabrautzky-oss/NovaNote/releases/tag/v0.6.1

## Neu in 0.6.1
- Der letzte Tab schließt jetzt das Fenster (wie Chrome) — statt einen neuen leeren Tab zu erzeugen; „Alle Tabs schließen“ und `Strg+W` auf dem letzten Tab machen dasselbe
- Bugfix: Nach „Nein“ im Speichern-Dialog bleibt ein Tab als „nicht gespeichert“ markiert — auch nach dem Neustart (Session merkt `Dirty`), und beim nächsten Schließen wird wieder gefragt
- Eine beantwortete „Nein“-Abfrage wird pro Schließvorgang nur einmal gestellt (kein Doppel-Dialog bei „Alle Tabs schließen“ + Fenster-Schließen)

## Neu in 0.6.0
- **Bilder im Chat (Vision)**: `+`-Buttonanhänge (Mehrfachauswahl), Drag & Drop auf das Eingabefeld und `Strg+V` für Screenshots aus der Zwischenablage
- **Vision-Erkennung**: Server-Metadaten (`vision`, `capabilities`/`modalities`, `projector`), Modellnamen-Heuristik (`qwen…-vl`, `llava`, `pixtral`, `gemma-3`, `minicpm-v`, `moondream`, `gpt-4o`, `claude-3`, `gemini`, …) und manueller „Vision erzwingen“-Schalter in den KI-Einstellungen
- Ohne Vision ist der `+`-Button deaktiviert (Tooltip erklärt es) und Drag & Drop blockiert; angehängte Bilder werden nur dann gesendet
- Anhänge als Thumbnail-Chips mit `✕` vor dem Senden; im Chat als Vorschaubilder mit Lightbox (Klick vergrößert, `Escape` schließt)
- Sprache: „Keine“ heißt jetzt „Text“ und erscheint in der Statusleiste; ein Klick auf die Sprache öffnet ein Menü mit allen Sprachen, und eine manuelle Auswahl schaltet „Automatisch erkennen“ aus
- Beim Schließen fragt NovaNote jeden ungespeicherten Tab einzeln ab („Speichern?/Verwerfen/Abbrechen“); der abgefragte Tab bekommt vorher den Fokus. Die Sammel-Abfrage „Alle Tabs schließen“ gibt es nicht mehr. „Datei → Vor dem Schließen nach Speichern fragen“ schaltet die Abfrage aus (dann werden Änderungen verworfen)
- Große Bilder werden speicherschonend auf 2048 px skaliert und als JPEG gesendet; maximal 6 Bilder pro Nachricht, Bilder aus älteren Nachrichten werden im Verlauf entfernt
- Die ganze Statusleiste ist per Klick bedienbar: Syntax-Check an/aus, Encoding, Zeilenende und Schriftgröße (Schieberegler 40–300 %); Menüs und Regler folgen dem Theme (Dark Mode: dunkler Popup, heller Text)

## Neu in 0.5.2
- **Chat als WebView2-Ansicht** statt reinem Text: Markdown mit Überschriften, Listen, Tabellen und Syntax-Highlighting, Antworten werden live gestreamt
- **Tool-Calls als aufklappbare Karten** mit Status (⏳ Läuft, 🟡 Warten auf Bestätigung, ✅ Abgeschlossen, ❌ Fehler, 🚫 Verwerpen), formatierten Arguments und Ergebnis
- **Bestätigen/Verwerfen direkt im Tool-Card** statt eigener Buttons; Codeblöcke bekommen „Kopieren“ und „In Editor einfügen“ (am Cursor)
- Eingabefeld und Buttons (Senden, Abbrechen, Verlauf löschen) bleiben native WPF-Controls; Chat-Farben folgen dem Theme
- Chat-Frontend liegt lokal im Programm (`Assets\Chat\`, mit mitgeliefertem `marked`/`highlight`) — kein CDN, kein Internet nötig
- Panelbreite: ohne gespeicherten Wert startet das KI-Panel mit halber Fensterbreite; nach dem Zerren am Trenner bleibt die Breite gespeichert
- Ohne WebView2-Runtime fallbackt der Chat auf die einfache Textausgabe; `WebView2Loader.dll` ist im Paket enthalten

## Neu in 0.5.1
- Bugfix: „Update prüfen → Setup herunterladen“ startet das Setup jetzt korrekt (der Download hielt die Datei beim Start offen)

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
- Statusleiste: alles per Klick änderbar — Sprache (Menü, manuelle Auswahl schaltet „Automatisch erkennen“ aus), Encoding (UTF-8, UTF-8 mit BOM, UTF-16 LE/BE, ANSI), Zeilenende (Windows/Unix/Macintosh), Schriftgröße (Schieberegler 40–300 %) und Syntax-Check (Klick schaltet an/aus). Menüs und Regler folgen dem Theme (Dark Mode: dunkler Popup-Hintergrund, heller Text)
- Code-Folding (abschaltbar) und Whitespace-Anzeige
- Mehrfach-Tabs und Tabs in eigene Fenster lösen; der letzte Tab schließt das Fenster (wie Chrome)
- Session-Wiederherstellung: Fenster und Tabs beim nächsten Start wieder öffnen; ungespeicherte Tabs bleiben dabei als „nicht gespeichert“ markiert
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
   Ohne gespeicherten Wert ist das Panel halb so breit wie das Fenster; nach dem Zerren am Trenner bleibt die Breite gespeichert.
4. Die KI bekommt nur Dateiinfos (Name, Sprache, Zeilenzahl) als Systemprompt. Sie entscheidet selbst, ob sie über
   `get_document`/`search_document` im Dokument liest, und antwortet dann in Text oder ruft ein Bearbeitungs-Werkzeug auf.
5. Editor-Aktionen werden standardmäßig erst nach „Bestätigen“ im Tool-Card ausgeführt; „KI → Aktionen automatisch anwenden“
   führt sie sofort aus. `Strg+Z` macht übernommene Änderungen rückgängig.
6. Codeblöcke in der Antwort haben „Kopieren“ und „In Editor einfügen“; Einfügen setzt den Code am Cursor (bestehende Auswahl wird ersetzt).
7. Modelle ohne native Tool-Aufrufe nutzen ein Text-Format (```nova```-Block), das NovaNote ebenfalls ausführt.
8. Der API-Key wird verschlüsselt (DPAPI, aktueller Benutzer) in `%APPDATA%\NovaNote\ki.json` gespeichert.
9. Manche Server (z. B. `llama-server`) melden Tool-Support nicht in `/v1/models`. NovaNote versucht Tool-Aufrufe trotzdem
   und setzt das „Tools“-Flag im Panel automatisch, sobald ein Aufruf akzeptiert wurde.
10. Bilder: Wenn das aktive Modell Vision unterstützt, hängst du über den `+`-Button Bilder an (auch Drag & Drop auf das
    Eingabefeld oder `Strg+V` für Screenshots). Ohne Vision ist der Button deaktiviert und Drag & Drop blockiert; unbekannte
    Custom-Modelle kannst du in „KI → KI-Einstellungen…“ mit „Vision erzwingen“ freigeben.

Die KI ist rein optional: ohne laufenden Server passiert nichts, NovaNote bleibt ein lokaler Editor.

## Unterstützte Sprachen
| Sprache | Syntax-Highlighting | Syntax-Check | Code-Folding | Hinweis |
|---|---:|---:|---:|---|
| Text | ❌ | ❌ | ❌ | kein Sprachmodus (ohne Endung) |
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
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.6.1/NovaNote-win-Setup.exe

## Installation als Portable-Version
1. `NovaNote-win-Portable.zip` von der GitHub-Release-Seite herunterladen.
2. Das ZIP in einen Ordner entpacken.
3. `NovaNote.exe` starten.

Direkter Portable-Download:  
https://github.com/jsabrautzky-oss/NovaNote/releases/download/v0.6.1/NovaNote-win-Portable.zip

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
- WebView2-Runtime (in Chrome/Edge 90+ enthalten) für das Chat-Panel; ohne Runtime läuft der Chat als Textausgabe
- Für die KI: ein lokaler OpenAI-kompatibler Server (LM Studio, Ollama oder `llama-server`)
- Für das Chat-Panel: WebView2-Runtime (auf Windows 11 dabei, auf Windows 10 meist installiert); ohne Runtime zeigt der Chat die einfache Textausgabe

## Systeme
NovaNote läuft für:
- Windows 10 x64
- Windows 11 x64

Es gibt aktuell keine Linux-, macOS- oder ARM-/32-bit-Versionen.

## Erstellt mit lokaler KI
- Opencode Desktop
- Strata 0.2.2 · Engine 0.1.47
- Model: Qwen3.8-Flash-Next Gyro-S
