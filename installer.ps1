param(
  [string]$packVersion = "0.2.0-beta"
)

$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoDir "NovaNote\NovaNote.csproj"
$icon = Join-Path $repoDir "NovaNote\novanote.ico"
$publishDir = Join-Path $repoDir "publish"
$releasesDir = Join-Path $repoDir "Releases"

$packId = "NovaNote"
$runtime = "win-x64"
$mainExe = "NovaNote.exe"

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
  dotnet tool install --global vpk --version 1.2.161
}

if (Test-Path -LiteralPath $publishDir) {
  Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $releasesDir) {
  Remove-Item -LiteralPath $releasesDir -Recurse -Force
}

dotnet publish $project `
  --configuration Release `
  --runtime $runtime `
  --self-contained true `
  --output $publishDir `
  --nologo `
  /p:Version=$packVersion

vpk pack `
  --packId $packId `
  --packVersion $packVersion `
  --packDir $publishDir `
  --runtime $runtime `
  --packTitle "NovaNote" `
  --packAuthors "NovaNote" `
  --mainExe $mainExe `
  --icon $icon `
  --outputDir $releasesDir
