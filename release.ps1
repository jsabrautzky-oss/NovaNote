param(
  [string]$version = "0.6.1",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.6.1",
  [string]$title = "NovaNote 0.6.1",
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
  'NovaNote 0.6.1 - Bugfix-Release: Tab-Schließen wie im Browser und „nicht gespeichert" bleibt erhalten. Leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung inkl. Bild-Chat (Vision), kein Cloud-Zwang.',
  '',
  'Behoben:',
  '- Schließt man den letzten Tab, wird das Fenster geschlossen (wie Chrome) statt einen neuen leeren Tab zu erzeugen; gilt auch für „Alle Tabs schließen" und Strg+W',
  '- „Nein" im Speichern-Dialog behält den „nicht gespeichert"-Status: die Session merkt Dirty, der wiederhergestellte Tab bleibt mit Punkt markiert und wird beim nächsten Schließen erneut gefragt',
  '- „Nein" wird pro Schließvorgang nur einmal gefragt (kein Doppel-Dialog bei „Alle Tabs schließen" + Fenster-Schließen); neue Änderungen setzen die Markierung zurück',
  '- SaveSessionNow() übersieht Fenster ohne Tabs und löscht die Session, wenn kein Fenster mehr Tabs hat',
  '',
  'Enthalten (seit 0.6.0):',
  '- Bildanhänge im Chat: +-Button, Drag & Drop, Strg+V für Screenshots; Vision-Erkennung ueber Metadaten plus Namens-Heuristik, „Vision erzwingen" pro Modell',
  '- Bilder als data:image-Array im OpenAI-kompatiblen Payload (LM Studio, Ollama, llama-server); Thumbnails in den Chat-Bubbles mit Lightbox (Escape schließt)',
  '- Chat als WebView2-Ansicht (Markdown, Streaming, Tool-Cards mit Bestaetigen/Verwerfen, Codeblock "Kopieren"/"In Editor einfuegen")',
  '- Statusleiste per Klick: Sprache, Syntax-Check an/aus, Encoding, Zeilenende, Schriftgröße mit Schieberegler (40-300 %); Menü/Slider gethemt (Dark Mode)',
  '- Beim Schließen wird jeder ungespeicherte Tab einzeln abgefragt, der Tab bekommt vorher den Fokus',
  '- Optionales KI-Panel (Strg+Alt+K) mit Tool-Calls; Anbieter-Verwaltung mit Modell-Auto-Scan, API-Key per DPAPI',
  '- Velopack-Installer (NovaNote-win-Setup.exe), Portable-Zip, Delta ab 0.6.0',
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
