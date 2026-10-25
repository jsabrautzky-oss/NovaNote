# NovaNote – Build & Tests

## Build
- `dotnet build NovaNote/NovaNote.csproj -c Release --nologo`
- Windows PowerShell: kein `&&`, verkettete Befehle mit `;` und `if ($?) { ... }`.

## UI-Harness
- Der Test-Harness liegt außerhalb des Repos: `C:\Users\juerg\AppData\Local\Temp\opencode\chatTest\Program.cs`
- Build: `dotnet build chatTest/chatTest.csproj -c Release --nologo` (Working Directory `C:\Users\juerg\AppData\Local\Temp\opencode`)
- Modi: `chatTest.exe ui`, `chat`, `stream`, `vision`; ohne Argument alle Modi; Erfolg = `ALL OK`
- Der Harness setzt `NOVANOTE_DIR` auf `%TEMP%\novanote-test-data`, damit echte `ki.json`, `einstellungen.json` und `sitzung.json` nicht angefasst werden.
- Im `ui`-Modus wird `Prefs.AskSaveOnClose` auf `false` gesetzt, damit keine modalen Dialoge den Test blockieren.

## Dialoge beim Testen
- Wenn beim Testen oder beim Schließen des Programms ein Dialog wie „NovaNote – Speichern?“ erscheint: **mit „Nein“ bestätigen** (Änderungen verwerfen).
- Nie mit „Ja“ speichern und nie mit „Abbrechen“ abbrechen – „Abbrechen“ und „Ja“ lassen das Programm offen bzw. schreiben Testdaten in echte Dateien.
- Bei „Alle Tabs schließen“ gilt dasselbe: jeder aufgetauchte Speichern-Dialog wird mit „Nein“ beantwortet.

## KI-Wissenswiki (hilfe.md)
- `NovaNote/Assets/Help/hilfe.md` ist das eingebettete Nachschlagewerk, das die KI über das Werkzeug `get_help` liest (Loader `NovaNote/AiHelp.cs`, als `EmbeddedResource` in der `csproj`).
- Nach **jeder** Änderung an Menüs, Tastenkürzeln, Editor-Funktionen (Folding, Umbruch, Nummern, Syntax-Check …), Einstellungen (`einstellungen.json`-Schlüssel) oder Dialogen `hilfe.md` mitpflegen, sonst antwortet die KI veraltet.
- Der Platzhalter `%VERSION%` bleibt stehen; `AiHelp.Version()` setzt beim Laden die Assembly-Version ein.
- Der `ui`-Modus des Harness prüft `get_help` (Inhaltsverzeichnis, Folding-Abschnitt, Tastenkürzel, nova-Fallback).

## Historie (CHANGELOG.md)
- `CHANGELOG.md` ist die einzige Historie des Projekts: nach **jeder** abgeschlossenen Änderung sofort eine Bullet im Abschnitt `## Unreleased` eintragen – eine Bullet pro Änderung, ein Zeile, kurz.
- Präfix je Bullet: `FEAT`, `FIX`, `CHORE` oder `TEST` plus Bereich, z. B. `FEAT ki: …`, `FIX save: …`, `TEST harness: …`.
- Unter jeder Versionsüberschrift steht eine Zeile `Kurz: …` (einzeilige Summary). `release.ps1` liest den Abschnitt der Version und baut daraus die GitHub-Release-Notes – die `Kurz:`-Zeile wird die erste Notes-Zeile.
- `## Unreleased` bleibt immer oben; beim Release wird der Abschnitt zu `## x.y.z` umbenannt und die `Kurz:`-Zeile auf die endgültige Summary geprüft.
- Harness-Änderungen liegen außerhalb des Repos (`%TEMP%\opencode\chatTest`) und werden nur als `TEST harness: …` in `CHANGELOG.md` festgehalten.

## Release
1. `NovaNote/NovaNote.csproj`: `VersionPrefix`, `Version`, `AssemblyVersion`, `FileVersion` auf die neue Version setzen.
2. `CHANGELOG.md`: `## Unreleased` → `## x.y.z`, `Kurz:`-Zeile als Summary, `### Paket`-Bullet (Setup, Portable-Zip, Full-Paket, Delta von der Vorgängerversion).
3. `README.md`: „Aktuelle Version“ und GitHub-Release-Link aktualisieren, neuen Abschnitt `## Neu in x.y.z` mit den Kurzpunkten anlegen (neueste oben).
4. Commit im Stil des Repos, z. B. `Ship 0.6.2 with …`; nur intendierte Dateien stagen, keine Secrets.
5. `installer.ps1 -packVersion x.y.z` (publish + `vpk pack`, erzeugt `Releases/` inkl. Delta aus dem vorherigen Stand).
6. `git tag vx.y.z` und pushen (`git push; git push origin vx.y.z`).
7. `release.ps1 -version x.y.z -tag vx.y.z` – die Release-Notes kommen automatisch aus dem `CHANGELOG.md`-Abschnitt (`-notesVersion`, `-changelogPath`, `-deltaFrom` als Overrides; `-skipPack` wenn schon gepackt; `-prerelease` für Beta; `-dryRun` erzeugt nur die Notes-Datei ohne GitHub-Release).

