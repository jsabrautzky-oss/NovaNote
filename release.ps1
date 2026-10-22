param(
  [string]$version = "0.5.1",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.5.1",
  [string]$title = "NovaNote 0.5.1",
  [bool]$prerelease = $false,
  [bool]$skipPack = $false
)

$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releasesDir = Join-Path $repoDir "Releases"

if (-not $skipPack) {
  & (Join-Path $repoDir "installer.ps1") -packVersion $version
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($gh) { $gh.Source } else { "C:\Program Files\GitHub CLI\gh.exe" }

$notes = @(
  'NovaNote 0.5.1 - Bugfix-Release fuer den Update-Setup-Start. Leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung, kein Cloud-Zwang.',
  '',
  'Fixes:',
  '- "Update pruefen -> Setup herunterladen" startet das Setup jetzt: der Download hielt die Datei beim Start offen ("von einem anderen Prozess verwendet")',
  '- KI schickt den Dokumenttext nicht mehr automatisch; sie liest bei Bedarf selbst per get_document/search_document (spart Tokens)',
  '- ComboBoxen zeigen die Auswahl im geschlossenen Feld; KI-Einstellungsdialog ohne ueberlappende Zeilen',
  '',
  'Enthalten:',
  '- Optionales KI-Panel (Strg+Alt+K) mit Tool-Calls: get_document, search_document, goto_line, select_lines, replace_all_text, replace_lines, insert_at_line, syntax_check, format_code',
  '- Anbieter-Verwaltung (LM Studio, Ollama, OpenAI-kompatibel, eigene Adresse) mit Modell-Auto-Scan; API-Key verschluesselt (DPAPI)',
  '- Velopack-Installer (NovaNote-win-Setup.exe), Portable-Zip, Delta ab 0.5.0',
  '- Markdown, Python, C# und YAML; Dateiendungen .txt/.md/.markdown/.json/.xml/.html ueber HKCU-ProgID NovaNote.Editor',
  '',
  'Hinweis: Die KI ist optional und laeuft nur gegen einen lokalen OpenAI-kompatiblen Server (LM Studio, Ollama, llama-server).'
) -join "`n"

$notesFile = Join-Path $releasesDir "release-notes.txt"
[System.IO.File]::WriteAllText($notesFile, $notes, [System.Text.UTF8Encoding]::new($false))

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

$releaseArgs = @("release", "create", $tag, "--repo", $repo, "--title", $title, "--notes-file", $notesFile)
if ($prerelease) { $releaseArgs += "--prerelease" }
$releaseArgs += $assets

& $ghPath @releaseArgs
