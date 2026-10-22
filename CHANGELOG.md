# Changelog

## 0.5.1
Bugfix-Release: Der Update-Setup konnte nicht gestartet werden („Datei wird von einem anderen Prozess verwendet“), weil der Download die Datei beim Start noch offen hielt.

### Behoben / Fixes
- `UpdateService.DownloadSetupAsync` schließt alle Download-Streams, bevor `LaunchSetup` das Setup startet; der Update-Dialog „Update prüfen → Herunterladen“ funktioniert wieder
- README enthält unten die Credits „Erstellt mit lokaler KI“

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Delta-Paket von 0.5.0 (klein, nur geänderte Dateien)

## 0.5.0
NovaNote 0.5.0 ist die erste Release-Version mit der optionalen lokalen KI-Anbindung: Editor ohne Cloud-Zwang, KI nur gegen den eigenen Server.

### Neue Features
- Lokales KI-Panel rechts im Fenster mit GridSplitter, Toggle über „KI → KI-Panel“ oder `Strg+Alt+K`; die Panel-Breite wird nach dem Zerren am Trenner in den Einstellungen gespeichert (`AiPanelWidth`, 180–720 px)
- KI ist optional und standardmäßig geschlossen: ohne Server laufen keine Anfragen, der Editor bleibt unverändert
- KI-Menügruppe: Panel-Umschaltung, „Aktionen automatisch anwenden“, „Modelle scannen“, „KI-Einstellungen…“
- Anbieter-Verwaltung in `%APPDATA%\NovaNote\ki.json`: LM Studio, Ollama, OpenAI-kompatibel oder eigene Anbieter mit Basis-URL und API-Key
- API-Key mit DPAPI (aktueller Benutzer) verschlüsselt gespeichert, im Dialog maskiert („Anzeigen“ schaltet um)
- Modell-Auto-Scan: `/v1/models` plus LM Studio `/api/v1/models` und Ollama `/api/tags` + `/api/show`; erfasst Name, Modell-ID, Kontextgröße, Vision und Tool-Support; manuelle Modelle bleiben erhalten
- Scan liest auch `contextWindow`/`context_length`/`n_ctx` und Vision-Marker wie `multimodal`, damit `llama-server` (auch im Netzwerk) korrekt erkannt wird
- Das „Tools“-Flag wird automatisch auf Ja gesetzt, wenn der Server einen Tool-Aufruf akzeptiert hat (llama-server meldet das nicht in `/v1/models`)
- Aktives Modell pro Anbieter per ComboBox im Panel; Statuszeile zeigt Anbieter, Modell, Kontext, Vision und Tools
- Chat mit Multi-Turn-Verlauf; das Dokument wird nicht automatisch als Kontext geschickt — die KI liest bei Bedarf selbst über `get_document`/`search_document` (spart Tokens)
- Agent-Loop mit bis zu 6 Tool-Runden und Abbrechen; Werkzeuge: `get_document`, `search_document`, `goto_line`, `select_lines`, `replace_all_text`, `replace_lines`, `insert_at_line`, `syntax_check`, `format_code`
- Editor-Aktionen brauchen Bestätigung per „Übernehmen“ (abschaltbar über „Aktionen automatisch anwenden“); Änderungen bleiben eine Undo-Group
- Codeblöcke aus der KI-Antwort werden per „Code übernehmen“ in den Editor geschrieben; Modelle ohne Tool-Support nutzen ein ```nova```-Textformat
- Neue Theme-Stile für ToggleButton, ComboBox und PasswordBox im Windows-11-Look
- Rechtsklick-Menü im Editor mit dynamischen Aktionen: Cut/Copy/Delete nur mit Auswahl, Paste nur mit Zwischenablage, Undo/Redo nur wenn verfügbar, „JSON formatieren“ nur im JSON-Modus, „Code formatieren“ für Python/C#, „Python-Completion“ nur bei bereititem LSP, „Markdown-Vorschau aktualisieren“ nur bei offener Vorschau, sowie Zoom, Zeilenumbruch, Syntax-Check, Folding und nächster Syntaxfehler
- Editor-Tabs werden beim Schließen, Detach und Beenden sauber disposed

### Behoben / Fixes
- Update-Setup: der Download hielt die Datei bis zum Start offen (`using var file` im selben Block), wodurch `Process.Start` mit „von einem anderen Prozess verwendet“ fehlschlug. Streams werden jetzt vor dem Start geschlossen.
- ComboBoxen zeigen das ausgewählte Element im geschlossenen Feld (vorher leer, weil der eigene Style keinen Inhalt gebunden hatte)
- KI-Einstellungsdialog: „Neu“/„Löschen“ und „Editor-Aktionen automatisch anwenden“ bekommen eigene Zeilen und überlappen nicht mehr; der Dialog passt sich per SizeToContent an den Inhalt an
- Hell-Schema: `SelectionColor` war `#005FB859` (Alpha `00` = komplett transparent), markierter Text war nicht sichtbar. Jetzt `#59005FB8`.
- Dunkel-Schema: `SelectionColor` `#4CC2FF59` korrigiert auf `#594CC2FF` (richtiger Akzent-Ton statt Grün-Stich).
- Rechtsklick-Menü hatte keinen Theme-Stil: im dunklen Modus heller Popup-Hintergrund mit hellem Text (unkontrastierend). `Theme/Controls.xaml` hat jetzt einen `ContextMenu`-Style mit `PopupBgBrush`, `PopupBorderBrush` und `TextBrush` (dunkel im Dark, hell in Light).

### Bekannte Einschränkungen
- Die KI braucht einen laufenden OpenAI-kompatiblen Server; ohne Server bleibt das Panel bei „Kein Server erreichbar“.
- Native Tool-Aufrufe hängen vom Modell ab; Modelle ohne Tool-Support bekommen das ```nova```-Textformat, kleine Modelle können damit daneben greifen.
- Kontextgröße wird nur übernommen, wenn der Server sie liefert; sonst wird 4096 angenommen.
- Vision wird nur angezeigt, nicht als Bildnachricht verwendet.
- Der Chat-Verlauf ist pro Fenster und wird nicht als Session gespeichert.
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- Nur `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` werden verknüpft; andere Endungen öffnet man über „Öffnen…“ oder die Kommandozeile.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
- Python-LSP hängt von einem verfügbaren Python-LSP-Server ab (`pyright-langserver` oder `python -m ruff server`). Ruff nutzt Pull-Diagnosen; ohne Server werden nur CLI-Syntax- und Format-Checks genutzt.
- Python-Formatierung hängt von lokal installierten Tools ab (`ruff format`, `black`, `autopep8`). Ohne diese Tools bleibt der Text unverändert oder zeigt eine Meldung.
- C#-Syntax-Check prüft primär Syntax; semantische Compilerfehler aus vollständigen Projekten werden nicht erkannt.
- YAML-Folding basiert auf Einrückung und kann bei stark gemischten YAML-Strukturen ungenau sein.

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Update-Prüfung über GitHub-Releases (stable Versionen ignorieren Prereleases)
- Dateiendungen: `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

## 0.2.0-beta
Beta mit Markdown, Python, C# und YAML.

### Neue Features
- Syntax-Highlighting für Markdown, Python, C# und YAML (Markdown/YAML über eigene XSHD-Definitionen im Assembly)
- Markdown-Live-Vorschau für `.md` und `.markdown` über MdXaml
- Python-Live-Syntax-Check über LSP-Pull-Diagnosen (`textDocument/diagnostic`) oder Python-CLI (`py_compile`/`ruff`/`flake8`); LSP-Server bevorzugt `pyright-langserver`, sonst `python -m ruff server`
- Python-Completion über AvalonEdit `CompletionWindow` bei `.` und `Strg+Leertaste`
- Python-Formatierung über `ruff format`, `black` oder `autopep8`
- C#-Syntax-Check mit Roslyn und Code-Formatierung mit Roslyn-Formatter (`Strg+Umschalt+F`)
- YAML-Syntax-Check mit YamlDotNet und Folding nach Einrückung über `IndentFoldingStrategy`
- Sprachen-Menü mit Markdown, Python, C# und YAML sowie „Code formatieren“ und „Python-Completion“
- Öffnen-Dialog mit Filtern für `.md`, `.py`, `.cs` und `.yaml`
- GitHub-Update-Prüfung beim Start und „Update prüfen“ im „Über NovaNote“-Dialog

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Dateiendungen: `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

### Bekannte Einschränkungen der Beta
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- Nur `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` werden verknüpft; andere Endungen öffnet man über „Öffnen…“ oder die Kommandozeile.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
- Python-LSP hängt von einem verfügbaren Python-LSP-Server ab (`pyright-langserver` oder `python -m ruff server`). Ruff nutzt Pull-Diagnosen; ohne Server werden nur CLI-Syntax- und Format-Checks genutzt.
- Python-Formatierung hängt von lokal installierten Tools ab (`ruff format`, `black`, `autopep8`). Ohne diese Tools bleibt der Text unverändert oder zeigt eine Meldung.
- C#-Syntax-Check prüft primär Syntax; semantische Compilerfehler aus vollständigen Projekten werden nicht erkannt.
- YAML-Folding basiert auf Einrückung und kann bei stark gemischten YAML-Strukturen ungenau sein.

### Behoben / Fixes
- Hell-Schema: `SelectionColor` war `#005FB859` (Alpha `00` = komplett transparent), markierter Text war nicht sichtbar. Jetzt `#59005FB8`.
- Dunkel-Schema: `SelectionColor` `#4CC2FF59` korrigiert auf `#594CC2FF` (richtiger Akzent-Ton statt Grün-Stich).
- Rechtsklick-Menü hatte keinen Theme-Stil: im dunklen Modus heller Popup-Hintergrund mit hellem Text (unkontrastierend). `Theme/Controls.xaml` hat jetzt einen `ContextMenu`-Style mit `PopupBgBrush`, `PopupBorderBrush` und `TextBrush` (dunkel im Dark, hell in Light).
- Kontextmenü bleibt offen und blendet unpassende Einträge aus.

## 0.1.0-beta
Erste öffentliche Beta-Version von NovaNote.

### Features
- WPF-Editor auf Basis von AvalonEdit
- Syntax-Highlighting für JSON, XML, HTML, CSS, JavaScript, C++
- Syntax-Check mit roter Wellenlinie und `F4` zum nächsten Fehler
- Code-Folding für JSON, XML, HTML, CSS, JavaScript und C++
- Mehrfach-Tabs, Tab-Detach, Drag & Drop
- Themes: hell, dunkel, system
- Encoding- und Zeilenenden-Umschaltung
- Suchen/Ersetzen, Goto, Zoom, Drucken, Seiteneinrichtung
- Session-Wiederherstellung unter `%APPDATA%\NovaNote\`

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Dateiendungen: `.txt`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

### Bekannte Einschränkungen der Beta
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
