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
