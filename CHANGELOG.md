# Changelog

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
