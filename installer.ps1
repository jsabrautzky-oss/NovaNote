param(
  [string]$packVersion = "0.6.1"
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
  Get-ChildItem -LiteralPath $publishDir -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $releasesDir)) {
  New-Item -ItemType Directory -Path $releasesDir | Out-Null
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

$fullNupkg = Join-Path $releasesDir "$packId-$packVersion-full.nupkg"
$sha1 = (Get-FileHash -Algorithm SHA1 -LiteralPath $fullNupkg).Hash
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullNupkg).Hash
$size = (Get-Item -LiteralPath $fullNupkg).Length

$feedAssets = @(@{
  PackageId = $packId
  Version = $packVersion
  Type = "Full"
  FileName = "$packId-$packVersion-full.nupkg"
  SHA1 = $sha1
  SHA256 = $sha256
  Size = $size
})
$releaseAssets = @(
  @{ RelativeFileName = "$packId-win-Setup.exe"; Type = "Installer" },
  @{ RelativeFileName = "$packId-$packVersion-full.nupkg"; Type = "Full" },
  @{ RelativeFileName = "$packId-win-Portable.zip"; Type = "Portable" }
)

$deltaNupkg = Join-Path $releasesDir "$packId-$packVersion-delta.nupkg"
$deltaFileName = "$packId-$packVersion-delta.nupkg"
$releasesFile = Join-Path $releasesDir "RELEASES"

if (Test-Path -LiteralPath $deltaNupkg) {
  $deltaSha1 = (Get-FileHash -Algorithm SHA1 -LiteralPath $deltaNupkg).Hash
  $deltaSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $deltaNupkg).Hash
  $deltaSize = (Get-Item -LiteralPath $deltaNupkg).Length
  $deltaLine = "$deltaSha1 $deltaFileName $deltaSize"

  $feedAssets += @{
    PackageId = $packId
    Version = $packVersion
    Type = "Delta"
    FileName = $deltaFileName
    SHA1 = $deltaSha1
    SHA256 = $deltaSha256
    Size = $deltaSize
  }
  $releaseAssets += @{ RelativeFileName = $deltaFileName; Type = "Delta" }

  $existing = [System.IO.File]::ReadAllText($releasesFile, [System.Text.UTF8Encoding]::new($false))
  if ($existing -notmatch [regex]::Escape($deltaFileName)) {
    if (-not ($existing.EndsWith("`n") -or $existing.EndsWith("`r`n"))) { $existing += "`n" }
    [System.IO.File]::WriteAllText($releasesFile, $existing + $deltaLine + "`n", [System.Text.UTF8Encoding]::new($false))
  }
}

Set-Content -LiteralPath (Join-Path $releasesDir "releases.win.json") -Value (@{ Assets = $feedAssets } | ConvertTo-Json -Compress -Depth 5) -Encoding UTF8
Set-Content -LiteralPath (Join-Path $releasesDir "assets.win.json") -Value ($releaseAssets | ConvertTo-Json -Compress -Depth 5) -Encoding UTF8
