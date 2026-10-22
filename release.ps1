param(
  [string]$version = "0.5.0",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.5.0",
  [string]$title = "NovaNote 0.5.0",
  [bool]$prerelease = $false
)

$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releasesDir = Join-Path $repoDir "Releases"

& (Join-Path $repoDir "installer.ps1") -packVersion $version

$gh = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($gh) { $gh.Source } else { "C:\Program Files\GitHub CLI\gh.exe" }

$notes = @(
  'NovaNote 0.5.0 - leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung, kein Cloud-Zwang.',
  '',
  'Neu:',
  '- Optionales KI-Panel rechts im Fenster (Strg+Alt+K): Chat mit Dokumentkontext, Multi-Turn, Abbrechen',
  '- Editor-Werkzeuge per Tool-Call: get_document, search_document, goto_line, select_lines, replace_all_text, replace_lines, insert_at_line, syntax_check, format_code',
  '- Editor-Aktionen erst nach "Übernehmen" oder automatisch; Änderungen bleiben eine Undo-Group',
  '- Anbieter-Verwaltung (LM Studio, Ollama, OpenAI-kompatibel, eigene Adresse) mit Modell-Auto-Scan: Kontext, Vision, Tools',
  '- API-Key verschlüsselt (DPAPI); Codeuebernahme aus Antwort-Codeblocken; ```nova```-Fallback fuer Modelle ohne Tools',
  '- Panel-Breite wird nach dem Zerren am Trenner gespeichert',
  '- Markdown, Python, C# und YAML wie in 0.2.0; dynamisches Rechtsklick-Menue; Tab-Views werden disposed',
  '',
  'Fixes:',
  '- ComboBoxen zeigen die Auswahl im geschlossenen Feld',
  '- KI-Einstellungsdialog: keine ueberlappenden Zeilen mehr',
  '',
  'Enthalten:',
  '- Velopack-Installer (NovaNote-win-Setup.exe)',
  '- Portable-Zip (NovaNote-win-Portable.zip)',
  '- Dateiendungen .txt/.md/.markdown/.json/.xml/.html ueber HKCU-ProgID NovaNote.Editor',
  '',
  'Hinweis: Die KI ist optional und laeuft nur gegen einen lokalen OpenAI-kompatiblen Server (LM Studio, Ollama, llama-server).'
) -join "`n"

$assets = @(
  (Join-Path $releasesDir "NovaNote-win-Setup.exe"),
  (Join-Path $releasesDir "NovaNote-win-Portable.zip"),
  (Join-Path $releasesDir "NovaNote-$version-full.nupkg"),
  (Join-Path $releasesDir "RELEASES"),
  (Join-Path $releasesDir "releases.win.json"),
  (Join-Path $releasesDir "assets.win.json")
)

$delta = Join-Path $releasesDir "NovaNote-$version-delta.nupkg"
if (Test-Path $delta) { $assets += $delta }

$releaseArgs = @("release", "create", $tag, "--repo", $repo, "--title", $title, "--notes", $notes)
if ($prerelease) { $releaseArgs += "--prerelease" }
$releaseArgs += $assets

& $ghPath @releaseArgs
