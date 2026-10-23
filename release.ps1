param(
  [string]$version = "0.5.2",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.5.2",
  [string]$title = "NovaNote 0.5.2",
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
  'NovaNote 0.5.2 - Das KI-Chat-Panel rendert Antworten jetzt in WebView2 (HTML/JS) statt als nackter Text. Leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung, kein Cloud-Zwang.',
  '',
  'Neu:',
  '- Chat als WebView2-Ansicht: Markdown mit Ueberschriften, Listen, Tabellen und Syntax-Highlighting, Antworten werden live gestreamt (SSE, gebatcht ca. 70 ms)',
  '- Tool-Calls als aufklappbare Karten mit Status (laufend, wartet auf Bestaetigung, abgeschlossen, Fehler, verworfen), formatierten Arguments und Ergebnis',
  '- Bestaetigen/Verwerfen direkt im Tool-Card; Codeblocks haben "Kopieren" und "In Editor einfuegen" (am Cursor, eine Undo-Gruppe)',
  '- Chat-Frontend liegt lokal im Programm (Assets\Chat\ mit vendored marked/highlight); kein CDN, kein Internet needed',
  '- Chat-Farben folgen dem Theme (hell/dunkel); DevTools, Kontextmenue und Zoom im Chat sind deaktiviert',
  '- KI-Panel startet ohne gespeicherte Breite mit halber Fensterbreite (min. 180 px); nach dem Zerren am Trenner bleibt die Breite gespeichert',
  '- Ohne WebView2-Runtime fallbackt der Chat auf die einfache Textausgabe',
  '',
  'Enthalten:',
  '- Optionales KI-Panel (Strg+Alt+K) mit Tool-Calls: get_document, search_document, goto_line, select_lines, replace_all_text, replace_lines, insert_at_line, syntax_check, format_code',
  '- Anbieter-Verwaltung (LM Studio, Ollama, OpenAI-kompatibel, eigene Adresse) mit Modell-Auto-Scan; API-Key verschluesselt (DPAPI)',
  '- Velopack-Installer (NovaNote-win-Setup.exe), Portable-Zip, Delta ab 0.5.1',
  '- Microsoft.Web.WebView2 und WebView2Loader.dll im Paket',
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
