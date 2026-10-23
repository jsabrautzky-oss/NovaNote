param(
  [string]$version = "0.6.0",
  [string]$repo = "jsabrautzky-oss/NovaNote",
  [string]$tag = "v0.6.0",
  [string]$title = "NovaNote 0.6.0",
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
  'NovaNote 0.6.0 - Der KI-Chat kann Bilder (Vision) und die Statusleiste ist per Klick bedienbar. Leichter Windows-Texteditor (AvalonEdit, Themes, Mehrfach-Tabs, Syntax-Check) - optional mit lokaler KI-Anbindung, kein Cloud-Zwang.',
  '',
  'Neu:',
  '- Bildanhänge im Chat: +-Button (Mehrfachauswahl), Drag & Drop auf das Eingabefeld, Strg+V für Screenshots aus der Zwischenablage',
  '- Vision-Erkennung ueber Server-Metadaten (vision, supports_vision, capabilities/modalities, projector, vision_* Zahlen) plus Modellnamen-Heuristik (vision, -vl, llava, pixtral, gemma-3, minicpm-v, moondream, gpt-4o, claude-3, gemini, glm-4v, internvl, phi-vision)',
  '- "Vision erzwingen" pro Modell in den KI-Einstellungen; Status zeigt "Vision Ja (manuell)"',
  '- OpenAI-kompatibles Payload: Bilder als data:image/...;base64 im content-Array (LM Studio, Ollama, llama-server); ohne Bilder bleibt content ein String',
  '- Thumbnail-Chips vor dem Senden (48x48 mit Dateiname und x), Bilder in den Chat-Bubbles, Klick öffnet Lightbox (Escape schließt)',
  '- Grosse Bilder werden auf 2048 px skaliert und als JPEG kodiert, max. 6 Bilder pro Nachricht; Bilder aus älteren Nachrichten werden entfernt',
  '- Ohne Vision ist der +-Button deaktiviert (Tooltip) und Drag & Drop blockiert',
  '- Statusleiste per Klick: Sprache (Menü, manuelle Auswahl deaktiviert "Automatisch erkennen"), Syntax-Check an/aus, Encoding, Zeilenende, Schriftgröße mit Schieberegler (40-300 %)',
  '- Kontext-Menüs, Menü-Trenner und Slider sind vollständig gethemt (Dark Mode: dunkler Popup #2B2B2B, heller Text #FFFFFF)',
  '- Sprache "Keine" heißt jetzt "Text"',
  '- Beim Schließen wird jeder ungespeicherte Tab einzeln abgefragt (Speichern?/Verwerfen/Abbrechen), der Tab bekommt vorher den Fokus; die Sammel-Abfrage "Alle Tabs schließen" ist entfernt',
  '',
  'Enthalten:',
  '- Chat als WebView2-Ansicht (Markdown, Streaming, Tool-Cards mit Bestaetigen/Verwerfen, Codeblock "Kopieren"/"In Editor einfuegen")',
  '- Optionales KI-Panel (Strg+Alt+K) mit Tool-Calls: get_document, search_document, goto_line, select_lines, replace_all_text, replace_lines, insert_at_line, syntax_check, format_code',
  '- Anbieter-Verwaltung (LM Studio, Ollama, OpenAI-kompatibel, eigene Adresse) mit Modell-Auto-Scan; API-Key verschluesselt (DPAPI)',
  '- Velopack-Installer (NovaNote-win-Setup.exe), Portable-Zip, Delta ab 0.5.2',
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
