param(
  [string]$version = "0.1.0-beta",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.1.0-beta",
  [string]$title = "NovaNote 0.1.0-beta",
  [bool]$prerelease = $true
)

$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releasesDir = Join-Path $repoDir "Releases"

& (Join-Path $repoDir "installer.ps1") -packVersion $version

$gh = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($gh) { $gh.Source } else { "C:\Program Files\GitHub CLI\gh.exe" }

$notes = @(
  'Erster Beta-Release von NovaNote.',
  '',
  'Enthalten:',
  '- Velopack-Installer (NovaNote-win-Setup.exe)',
  '- Portable-Zip (NovaNote-win-Portable.zip)',
  '- AvalonEdit-Editor mit Syntax-Highlighting, Syntax-Check und Code-Folding',
  '- Themes hell/dunkel/system, Mehrfach-Tabs, Session-Wiederherstellung',
  '- Dateiendungen .txt/.json/.xml/.html ueber HKCU-ProgID NovaNote.Editor',
  '',
  'Hinweis: Beta/Prerelease. Velopack-Updates prufen standardmaessig stabile Releases.'
) -join "`n"

$assets = @(
  (Join-Path $releasesDir "NovaNote-win-Setup.exe"),
  (Join-Path $releasesDir "NovaNote-win-Portable.zip"),
  (Join-Path $releasesDir "NovaNote-$version-full.nupkg"),
  (Join-Path $releasesDir "RELEASES"),
  (Join-Path $releasesDir "releases.win.json"),
  (Join-Path $releasesDir "assets.win.json")
)

$releaseArgs = @("release", "create", $tag, "--repo", $repo, "--title", $title, "--notes", $notes)
if ($prerelease) { $releaseArgs += "--prerelease" }
$releaseArgs += $assets

& $ghPath @releaseArgs
