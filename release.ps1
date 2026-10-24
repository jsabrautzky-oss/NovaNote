param(
  [string]$version = "0.6.1",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "",
  [string]$title = "",
  [string]$notesVersion = "",
  [string]$changelogPath = "",
  [string]$deltaFrom = "",
  [switch]$prerelease,
  [switch]$skipPack,
  [switch]$dryRun
)

$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releasesDir = Join-Path $repoDir "Releases"
if (-not $tag) { $tag = "v$version" }
if (-not $title) { $title = "NovaNote $version" }
if (-not $notesVersion) { $notesVersion = $version }
if (-not $changelogPath) { $changelogPath = Join-Path $repoDir "CHANGELOG.md" }

if (-not $skipPack) {
  & (Join-Path $repoDir "installer.ps1") -packVersion $version
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($gh) { $gh.Source } else { "C:\Program Files\GitHub CLI\gh.exe" }

$lines = [System.IO.File]::ReadAllLines($changelogPath, [System.Text.UTF8Encoding]::new($false))
$headingIndex = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match ("^##\s+" + [regex]::Escape($notesVersion) + "\s*$")) {
    $headingIndex = $i
    break
  }
}
if ($headingIndex -lt 0) {
  throw "Kein CHANGELOG-Abschnitt für '$notesVersion' in $changelogPath"
}

$section = @()
$nextHeading = ""
for ($i = $headingIndex + 1; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match "^##\s+") {
    $nextHeading = ($lines[$i] -replace "^##\s+", "").Trim()
    break
  }
  $section += $lines[$i]
}

$summary = ""
$body = @()
foreach ($line in $section) {
  $t = $line.TrimEnd()
  if ($t.StartsWith("<!--")) { continue }
  if (-not $summary) {
    if ($t -eq "") { continue }
    $summary = ($t -replace "^Kurz:\s*", "")
    continue
  }
  $body += $t
}
if (-not $summary) { $summary = "NovaNote $version" }
while ($body.Count -gt 0 -and $body[$body.Count - 1] -eq "") {
  $body = $body[0..($body.Count - 2)]
}

if (-not $deltaFrom -and $nextHeading -and $nextHeading -notmatch "(?i)unver") {
  $deltaFrom = $nextHeading
}

$footer = @(
  "",
  "Leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung inkl. Bild-Chat (Vision), kein Cloud-Zwang.",
  "",
  "Hinweis: Die KI ist optional und laeuft nur gegen einen lokalen OpenAI-kompatiblen Server (LM Studio, Ollama, llama-server)."
)
if ($deltaFrom) { $footer += @("", "Delta-Paket ab $deltaFrom.") }

$notes = (@($summary) + $body + $footer) -join "`n"

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

if ($dryRun) {
  Write-Output "Notes geschrieben: $notesFile"
  Write-Output "Titel: $title | Tag: $tag | Delta ab: $deltaFrom"
  return
}

$releaseArgs = @("release", "create", $tag, "--repo", $repo, "--title", $title, "--notes-file", $notesFile)
if ($prerelease) { $releaseArgs += "--prerelease" }
$releaseArgs += $assets

& $ghPath @releaseArgs
