# Changelog

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
- Editor-Tabs werden beim Schließen, Detach und Beenden sauber disposed

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
