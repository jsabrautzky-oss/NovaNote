# Changelog
<!-- Konvention: eine Bullet = eine Änderung, ein Zeile, kurz. Präfix FEAT | FIX | CHORE | TEST (+ Bereich, z. B. ki/save/ui/harness).
     Die Zeile "Kurz: …" unter jeder Überschrift ist die einzeilige Summary; release.ps1 baut daraus die GitHub-Release-Notes. -->

## 0.7.1
Kurz: Das Update läuft jetzt in-app mit Fortschrittsbalken und startet den Editor danach automatisch neu.

- FEAT update: installierte Versionen updaten über Velopack (`GithubSource` + `UpdateManager` über das `RELEASES`-Asset, Delta-Updates möglich) statt über den Setup-Download
- FEAT update: neues Dialog „NovaNote – Update“ zeigt Download und Installation mit Fortschrittsbalken und Prozent (vorher nur ein Wait-Cursor)
- FIX update: NovaNote startet nach dem Update wieder automatisch — `WaitExitThenApplyUpdates(restart:true)` wartet auf den sauberen Exit, installiert und öffnet den Editor mit der neuen Version
- FEAT update: der Neustart behält die Start-Argumente (Datei, `/S`) bei, ohne `/N` und `/?`
- FEAT update: „Update prüfen“ in Portable-/Build-Versionen lädt das Setup und öffnet es mit UI, NovaNote bleibt dabei offen
- FIX ui: eine übergebene Datei wird nach dem Neustart nicht doppelt geöffnet, wenn der Tab schon aus der Session wiederhergestellt ist (der bestehende Tab wird aktiviert)
- TEST harness: Der UI-Harness prüft `ActivateExistingTab` (case-insensitiv, kein Doppel-Tab) und das Update-Fortschrittsdialog (Balken und Prozent, Schließen bis `AllowClose()` blockiert)
- CHORE update: Das `RELEASES`-Feed enthält jetzt die Delta-Zeile (`installer.ps1`) — ohne sie schrieb Velopack nur Full-Einträge und jeder Update lud das Full-Paket (~91 MB) statt des Deltas (~0,3 MB)

### Paket
- Velopack-Installer `NovaNote-win-Setup.exe`, Portable-Zip `NovaNote-win-Portable.zip`, Full-Paket `NovaNote-0.7.1-full.nupkg`, Delta von 0.7.0 (`NovaNote-0.7.1-delta.nupkg`)

## 0.7.0
Kurz: Die KI kennt die Dokument-Art und kann sie umstellen; der Speicher-Dialog legt die passende Endung fest; die „KI denkt…“-Zeile blendet dezent ein; der KI-Chat bekommt Eingabe-Historie und Favoriten; die Kontextgröße des Modells wird erkannt, berücksichtigt und als Füllbalken angezeigt.

### Neue Features
- FEAT ki: `get_language`/`set_language` — die KI liest die aktuelle Dokument-Art und stellt sie um (auch Aliases wie `md`, `py`, `cs`, `c#`, `js`, `yml`, `c++`)
- FEAT ki: Der ```nova```-Text-Fallback für Modelle ohne native Tools erlaubt zusätzlich `get_language` und `set_language`
- FEAT save: `Speichern`/`Speichern unter` legt Name und Endung nach der Dokument-Art fest — ein neues HTML-Dokument wird als `Unbenannt.html` vorgeschlagen, JSON als `Unbenannt.json`, CSS als `Unbenannt.css`, JavaScript als `Unbenannt.js`, C++ als `Unbenannt.cpp`, XML als `Unbenannt.xml`; vorher gab es für alle diese Arten nur `Unbenannt.txt`
- FEAT save: Der Filter des Speichern-Dialogs listet alle Dokument-Arten (Text, Markdown, JSON, XML, HTML, CSS, JavaScript, C++, Python, C#, YAML, Alle Dateien) und wählt die aktuelle Art voraus; der Öffnen-Dialog listet denselben Filter, „Textdokumente“ bleibt vorausgewählt
- FEAT chat: Dezent-Denkfeld am Chat-Ende — 1px-Rand mit rotierendem Farbverlauf (Cyan/Violet/Grün, theme-aware) um „KI denkt“, dazu drei pulsierende Punkte; blendet sanft ein (0.55 s) und nach dem Denken per Fade-Out wieder aus (0.6 s bis `hidden`)
- CHORE chat: Der Three.js-Canvas-Effekt ist entfernt — `chat-wave.js` und `three.module.min.js` sind nicht mehr im Paket, der Chat braucht kein WebGL mehr
- FEAT ki: Das Eingabefeld ist eine Zeile höher und schmaler; rechts daneben stehen drei Icon-Buttons untereinander (Stern, Liste, Zahnrad)
- FEAT ki: Favoriten-Popup am Stern-Button — jeder Favorit übernimmt per Klick den Text in das Eingabefeld
- FEAT ki: Historie-Popup am Liste-Button — die letzten 10 Eingaben; der Stern macht eine Eingabe zum Favoriten (gelb gefüllt, zweiter Klick entfernt ihn), Klick auf den Text übernimmt ihn, „Verlauf leeren“ löscht nur die Eingaben
- FEAT ki: Favoriten-Dialog am Zahnrad-Button — Favoriten ansehen, bearbeiten, neu anlegen und löschen; persistiert in `ki-eingaben.json` und `ki-favoriten.json`
- FEAT ki: Der Favoriten-Dialog öffnet mittig über dem KI-Panel statt beliebig auf dem Bildschirm
- CHORE ki: Favoriten- und Historie-Popup nutzen die volle Breite des KI-Panels bis zum rechten Rand
- FEAT ki: Kontextgrenze pro Modell — Tiktoken zählt die Prompt-Tokens exakt (o200k für `gpt-4o`/o-series, cl100k für andere OpenAI-kompatible Modelle), LM Studio/Ollama nutzen eine Heuristik und kalibrieren sie an den vom Server gemeldeten `usage`-Tokens
- FEAT ki: Der Scan erkennt die Kontextgröße jetzt auch bei Ollama (`ollama.context_length` in `model_info`); ohne Erkenntung gilt 4096 als „Annahme“ in der Statuszeile
- FEAT ki: „KI-Einstellungen…“ hat ein Kontext-Feld pro Modell mit „manuell“-Schalter — manuell gesetzte Werte bleiben auch nach einem Scan stehen
- FEAT chat: Kontext-Statusleiste unten im KI-Panel — Füllbalken über Prompt-Tokens und Limit (Accent, Warn, Rot), Prozentzahl der Auslastung statt Tokenizer-Name, Tooltip mit Antwort-Reserve, Limit-Herkunft und Zählquelle

### Geändert
- CHORE ki: Der Systemprompt nennt die aktuelle Dokument-Art und weist auf `set_language` vor dem Schreiben eines Markdown-/Code-Dokuments hin
- CHORE ki: `set_language` schaltet „Automatisch erkennen“ (`Prefs.LanguageAuto`) aus und aktualisiert Statusleiste und Sprach-Menü — wie die manuelle Sprachwahl
- FIX chat: Die „KI denkt…“-Zeile lag durch `#log { height: 100% }` unter dem View und war unsichtbar — `body` ist jetzt Flex-Column mit `#log { flex: 1 }`, das Denkfeld bleibt unten im Viewport sichtbar
- FIX ki: Eine unbekannte Sprache wird als Tool-Fehler gezeigt („Unbekannte Sprache: …“) statt als normaler Tool-Text
- TEST harness: Der UI-Harness prüft die beiden Sprach-Tools, den Dialog-Filter (Label und Index je Art) und die Speicher-Endung
- TEST harness: Der Chat-Frontend-Test prüft das Denkfeld — class `on` nach `setBusy(true)`, drei Punkte, Viewport und Fade-Out (`hidden` erst nach der Transition)
- TEST harness: Der Chat-Frontend-Test speichert einen `CapturePreviewAsync`-PNG als visuellen Beleg des busy-Zustands
- TEST harness: Der UI-Harness prüft die drei Icon-Buttons neben der Eingabe, die Historie nach `AiSendAsync`, den Stern-Toggle und „Verlauf leeren" (Favoriten bleiben) sowie den Favoriten-Dialog
- FIX ki: Ist der Kontext voll, kürzt der Chat automatisch die ältesten Nachrichten (ein Tool-Aufruf und seine Ergebnisse bleiben zusammen), kürzt eine zu lange Nachricht mit Marker und meldet die Kürzung als Info-Zeile im Chat
- FIX ki: Der Request sendet `max_tokens` (OpenAI-kompatibel) und für Ollama zusätzlich `options.num_ctx`; meldet der Server einen Kontextfehler, wird einmal aggressiv gekürzt und erneut versucht statt mehrfach fehlerhaft
- TEST harness: Neuer `context`-Modus prüft Token-Schätzung, Budget-Kürzung, Override-Persistenz, Ollama-Kontext-Erkennung sowie `usage`, `max_tokens` und `num_ctx`
- TEST harness: Der UI-Harness prüft die Kontext-Statusleiste (Zeile 6, Balken-Limit, Prozent-Chip), die automatische Kürzung im gesendeten Prompt und das Kontext-Feld im KI-Einstellungsdialog
- CHORE chat: Der Chip rechts in der Kontextleiste zeigt die Prozentzahl der Auslastung statt „exakt (cl100k)“ oder „geschätzt“ — die Zählquelle steht nur noch im Tooltip

### Paket
- Velopack-Installer `NovaNote-win-Setup.exe`, Portable-Zip `NovaNote-win-Portable.zip`, Full-Paket `NovaNote-0.7.0-full.nupkg`, Delta von 0.6.1 (`NovaNote-0.7.0-delta.nupkg`)

## 0.6.1
Kurz: Tab-Schließen verhält sich wie im Browser, und „nicht gespeichert“ bleibt erhalten.

- Schließt man den letzten Tab, wird das Fenster geschlossen (wie Chrome) — statt einen neuen leeren Tab zu erstellen; „Alle Tabs schließen“ und `Strg+W` auf dem letzten Tab machen dasselbe
- Bugfix: „Nein“ im Speichern-Dialog behält den „nicht gespeichert“-Status des Tabs — die Session speichert `Dirty` und der wiederhergestellte Tab bleibt mit Punkt markiert und wird beim nächsten Schließen erneut gefragt (vorher ging der Status beim Schließen verloren)
- Eine abantwortete „Nein“-Abfrage wird pro Tab nur einmal gestellt (`_promptAnswered`), damit „Alle Tabs schließen“ + Fenster-Schließen nicht doppelt fragt; neue Änderungen setzen die Markierung zurück
- `SaveSessionNow()` ignoriert Fenster ohne Tabs und löscht die Session, wenn kein Fenster mehr Tabs hat

### Paket
- Velopack-Installer `NovaNote-win-Setup.exe`, Portable-Zip `NovaNote-win-Portable.zip`, Full-Paket `NovaNote-0.6.1-full.nupkg`, Delta von 0.6.0 (`NovaNote-0.6.1-delta.nupkg`)

## 0.6.0
Kurz: Der KI-Chat kann Bilder (Vision) — nur wenn das aktive Modell Bilder versteht; Statusleiste per Klick, Menüs und Regler gethemt, jeder ungespeicherte Tab wird einzeln mit Fokus abgefragt.

### Neue Features
- Bildanhänge im Chat: `+`-Button mit `OpenFileDialog` (Mehrfachauswahl), Drag & Drop von Bilddateien auf das Eingabefeld und `Strg+V` für Screenshots aus der Zwischenablage
- Vorschau-Leiste über der Eingabe: Thumbnail-Chips (48×48) mit Dateiname und `✕`, geleert nach dem Senden
- Vision-Erkennung: Server-Metadaten (`vision`/`supports_vision`/`multimodal`, `capabilities`/`modalities`, `projector`, `vision_*`-Zahlen in `metadata`/`model_info`) plus Modellnamen-Heuristik (`vision`, `-vl`, `llava`, `pixtral`, `minicpm-v`, `moondream`, `gemma-3`/`gemma3`, `gpt-4o`, `claude-3`, `gemini`, `glm-4v`, `internvl`, `phi-vision`)
- Manueller Override: „Vision erzwingen“ im KI-Einstellungsdialog pro Modell (`VisionOverride`); Statuszeile zeigt „Vision Ja (manuell)“
- OpenAI-kompatibles Payload: User-Nachrichten mit Bildern senden `content` als Array (`{"type":"text"}` + `{"type":"image_url","image_url":{"url":"data:image/…;base64,…"}}`; ohne Bilder bleibt `content` ein String) — identisch für LM Studio, Ollama und llama-server
- Bilder in den Chat-Bubbles: gerenderte Thumbnails (`max-height: 180px`, abgerundet), Klick öffnet eine Lightbox (`Escape` schließt sie)
- `ChatImages.cs`: Base64-Data-URI-Erzeugung, Kantenlänge > 2048 px wird auf 2048 px skaliert und als JPEG kodiert, > 6 MB wird neu kodiert, maximal 6 Bilder pro Nachricht

### Geändert
- Ohne Vision: `+`-Button deaktiviert mit Tooltip „Das aktuelle Modell unterstützt keine Bilder (Vision)“, `AiInput.AllowDrop` false und Drag & Drop blockiert (kurzer Hinweis im Chat)
- Die Sammel-Abfrage „Alle Tabs schließen“ beim Fenster-Schließen ist entfernt; stattdessen fragt `MainWindow_Closing` jeden ungespeicherten Tab einzeln ab (`PromptDirtyFocused` aktiviert den Tab vorher, damit der Dialog den richtigen Tab zeigt). `Prefs.AskSaveOnClose` ist wieder aktiviert (per Tab „Speichern?/Verwerfen/Abbrechen“); ausgeschaltet verwirft „Beenden“ Änderungen ohne Nachfrage
- Die Sprache „Keine“ heißt jetzt „Text“ und wird wie alle anderen Sprachen in der Statusleiste angezeigt (`DocLanguages.DisplayName(None)` = „Text“)
- Klick auf die Sprache in der Statusleiste öffnet ein Menü mit allen Sprachen und „Automatisch erkennen“ (Context-Menü über `StatusLanguage`, öffnet nach oben); eine manuelle Sprachwahl schaltet `Prefs.LanguageAuto` aus, die Statuszeile zeigt dann „JSON“ statt „JSON · Automatik“
- Kontext-Menüs (Sprachmenü, Rechtsklick-Menü) sind jetzt vollständig gethemt: eigener `Style` für `ContextMenu` mit `PopupBgBrush`/`PopupBorderBrush`/`TextBrush` und abgerundetem Popup mit Schatten — im Dark Mode ist der Hintergrund dunkel (#2B2B2B) und der Text hell (#FFFFFF)
- Die ganze Statusleiste ist per Klick bedienbar: `StatusSyntax` schaltet Syntax-Check an/aus, `StatusEncoding` und `StatusEol` öffnen Menüs (Encoding/Zeilenende), `StatusZoom` öffnet einen gethemten Schieberegler (`ZoomPopup` mit `ZoomSlider`, 40–300 %, Wert als Text daneben); `Theme/Controls.xaml` hat dazu einen dunklen `Slider`-Style (Accent-Thumb, `InputBgBrush`-Track)
- Systemprompt erwähnt angehängte Bilder; Bilder aus älteren Nachrichten werden im Verlauf entfernt, damit jeder Request nur die aktuellen Bilder mitschickt
- CSP des Chat-Frontends lässt `img-src 'self' data:`; der Sanitizer entfernt weiterhin Modell-HTML, angehängte Bilder werden per JS gesetzt

### Paket
- Velopack-Installer `NovaNote-win-Setup.exe`, Portable-Zip `NovaNote-win-Portable.zip`, Full-Paket `NovaNote-0.6.0-full.nupkg`, Delta von 0.5.2 (`NovaNote-0.6.0-delta.nupkg`)

## 0.5.2
Kurz: Das KI-Chat-Panel rendert Antworten in WebView2 (Markdown, Streaming, Tool-Cards mit Bestätigen/Verwerfen) statt als nackter Text.

### Neue Features
- Chat-Panel nutzt `Microsoft.Web.WebView2` mit lokalem Frontend `NovaNote/Assets/Chat/` (`chat.html`, `chat.css`, `chat.js`, vendored `marked.min.js` und `highlight.min.js`); geladen über `SetVirtualHostNameToFolderMapping("novanote.chat", …)`, offline ohne CDN
- Markdown-Rendering (Überschriften, Listen, Tabellen, Inline-Code) und Syntax-Highlighting für Codeblöcke im Theme-look (hell/dunkel via `setTheme` bei Theme-Wechsel)
- Jeder Codeblock hat „Kopieren“ (Zwischenablage) und „In Editor einfügen“ (`insertIntoEditor` → `DocumentView.InsertAtCaret`, eine Undo-Group, ersetzt bestehende Auswahl)
- Tool-Calls als aufklappbare `<details>`-Karten mit Status ⏳ Läuft / 🟡 Warten auf Bestätigung / ✅ Abgeschlossen / ❌ Fehler / 🚫 Verworfen, formatierten Arguments und Ergebnis; Bestätigen/Verwerfen sitzt inline im Card
- Gestreamte Antworten: `LocalAiClient.ChatStreamAsync` liest SSE-Deltas (`stream:true`) und liefert `appendChunk` gebatcht (~70 ms) an das Frontend; Auto-Scroll klebt unten, „Nach unten“-Button bei manuellem Scroll
- Chat-Events aus C#: `addMessage`, `appendChunk`, `addToolCall`, `updateToolResult`, `updateToolStatus`, `setBusy`, `setTheme`, `clearChat`; der Verlauf wird nach dem Laden des Frontends replayt
- DevTools, Standard-Kontextmenü, Statusbar und Zoom im Chat deaktiviert; fremde Navigation wird geblockt, HTML aus Modellantworten wird vor dem Rendern bereinigt (script/on*-Attribute/`javascript:`)
- Fallback: ohne WebView2-Runtime bleibt die bisherige TextBox-Ausgabe sichtbar und Edit-Tools laufen ohne Bestätigungs-Card

### Geändert
- `AiApplyButton`/`AiDiscardButton` im Panel entfernt (Bestätigung inline im Tool-Card, Code über den Einfügen-Button)
- `AiPanelWidth` ist jetzt optional: ohne gespeicherten Wert startet das Panel mit halber Fensterbreite (mindestens 180 px, nach Fenster-Größenänderung neu berechnet); ein per GridSplitter gespeicherter Wert wird wie bisher übernommen (180–720 px)
- Scan-, Fehler- und Infozeilen erscheinen als Info-Zeilen im Chat statt als Textblock
- Chat-Daten liegen in `%APPDATA%\NovaNote\WebView2` (geteilte WebView2-Umgebung pro Fenster)

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Delta-Paket von 0.5.1 (`NovaNote-0.5.2-delta.nupkg`)
- `Microsoft.Web.WebView2` und `WebView2Loader.dll` sind im Paket enthalten; das Chat-Frontend liegt in `Assets\Chat\`

## 0.5.1
Kurz: Bugfix — das Update-Setup ließ sich nicht starten („Datei wird von einem anderen Prozess verwendet“), weil der Download die Datei beim Start offen hielt.

### Behoben / Fixes
- `UpdateService.DownloadSetupAsync` schließt alle Download-Streams, bevor `LaunchSetup` das Setup startet; der Update-Dialog „Update prüfen → Herunterladen“ funktioniert wieder
- README enthält unten die Credits „Erstellt mit lokaler KI“

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Delta-Paket von 0.5.0 (klein, nur geänderte Dateien)

## 0.5.0
Kurz: Erste Version mit der optionalen lokalen KI-Anbindung — Editor ohne Cloud-Zwang, KI nur gegen den eigenen Server.

### Neue Features
- Lokales KI-Panel rechts im Fenster mit GridSplitter, Toggle über „KI → KI-Panel“ oder `Strg+Alt+K`; die Panel-Breite wird nach dem Zerren am Trenner in den Einstellungen gespeichert (`AiPanelWidth`, 180–720 px)
- KI ist optional und standardmäßig geschlossen: ohne Server laufen keine Anfragen, der Editor bleibt unverändert
- KI-Menügruppe: Panel-Umschaltung, „Aktionen automatisch anwenden“, „Modelle scannen“, „KI-Einstellungen…“
- Anbieter-Verwaltung in `%APPDATA%\NovaNote\ki.json`: LM Studio, Ollama, OpenAI-kompatibel oder eigene Anbieter mit Basis-URL und API-Key
- API-Key mit DPAPI (aktueller Benutzer) verschlüsselt gespeichert, im Dialog maskiert („Anzeigen“ schaltet um)
- Modell-Auto-Scan: `/v1/models` plus LM Studio `/api/v1/models` und Ollama `/api/tags` + `/api/show`; erfasst Name, Modell-ID, Kontextgröße, Vision und Tool-Support; manuelle Modelle bleiben erhalten
- Scan liest auch `contextWindow`/`context_length`/`n_ctx` und Vision-Marker wie `multimodal`, damit `llama-server` (auch im Netzwerk) korrekt erkannt wird
- Das „Tools“-Flag wird automatisch auf Ja gesetzt, wenn der Server einen Tool-Aufruf akzeptiert hat (llama-server meldet das nicht in `/v1/models`)
- Aktives Modell pro Anbieter per ComboBox im Panel; Statuszeile zeigt Anbieter, Modell, Kontext, Vision und Tools
- Chat mit Multi-Turn-Verlauf; das Dokument wird nicht automatisch als Kontext geschickt — die KI liest bei Bedarf selbst über `get_document`/`search_document` (spart Tokens)
- Agent-Loop mit bis zu 6 Tool-Runden und Abbrechen; Werkzeuge: `get_document`, `search_document`, `goto_line`, `select_lines`, `replace_all_text`, `replace_lines`, `insert_at_line`, `syntax_check`, `format_code`
- Editor-Aktionen brauchen Bestätigung per „Übernehmen“ (abschaltbar über „Aktionen automatisch anwenden“); Änderungen bleiben eine Undo-Group
- Codeblöcke aus der KI-Antwort werden per „Code übernehmen“ in den Editor geschrieben; Modelle ohne Tool-Support nutzen ein ```nova```-Textformat
- Neue Theme-Stile für ToggleButton, ComboBox und PasswordBox im Windows-11-Look
- Rechtsklick-Menü im Editor mit dynamischen Aktionen: Cut/Copy/Delete nur mit Auswahl, Paste nur mit Zwischenablage, Undo/Redo nur wenn verfügbar, „JSON formatieren“ nur im JSON-Modus, „Code formatieren“ für Python/C#, „Python-Completion“ nur bei bereititem LSP, „Markdown-Vorschau aktualisieren“ nur bei offener Vorschau, sowie Zoom, Zeilenumbruch, Syntax-Check, Folding und nächster Syntaxfehler
- Editor-Tabs werden beim Schließen, Detach und Beenden sauber disposed

### Behoben / Fixes
- Update-Setup: der Download hielt die Datei bis zum Start offen (`using var file` im selben Block), wodurch `Process.Start` mit „von einem anderen Prozess verwendet“ fehlschlug. Streams werden jetzt vor dem Start geschlossen.
- ComboBoxen zeigen das ausgewählte Element im geschlossenen Feld (vorher leer, weil der eigene Style keinen Inhalt gebunden hatte)
- KI-Einstellungsdialog: „Neu“/„Löschen“ und „Editor-Aktionen automatisch anwenden“ bekommen eigene Zeilen und überlappen nicht mehr; der Dialog passt sich per SizeToContent an den Inhalt an
- Hell-Schema: `SelectionColor` war `#005FB859` (Alpha `00` = komplett transparent), markierter Text war nicht sichtbar. Jetzt `#59005FB8`.
- Dunkel-Schema: `SelectionColor` `#4CC2FF59` korrigiert auf `#594CC2FF` (richtiger Akzent-Ton statt Grün-Stich).
- Rechtsklick-Menü hatte keinen Theme-Stil: im dunklen Modus heller Popup-Hintergrund mit hellem Text (unkontrastierend). `Theme/Controls.xaml` hat jetzt einen `ContextMenu`-Style mit `PopupBgBrush`, `PopupBorderBrush` und `TextBrush` (dunkel im Dark, hell in Light).

### Bekannte Einschränkungen
- Die KI braucht einen laufenden OpenAI-kompatiblen Server; ohne Server bleibt das Panel bei „Kein Server erreichbar“.
- Native Tool-Aufrufe hängen vom Modell ab; Modelle ohne Tool-Support bekommen das ```nova```-Textformat, kleine Modelle können damit daneben greifen.
- Kontextgröße wird nur übernommen, wenn der Server sie liefert; sonst wird 4096 angenommen.
- Vision wird nur angezeigt, nicht als Bildnachricht verwendet.
- Der Chat-Verlauf ist pro Fenster und wird nicht als Session gespeichert.
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- Nur `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` werden verknüpft; andere Endungen öffnet man über „Öffnen…“ oder die Kommandozeile.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
- Python-LSP hängt von einem verfügbaren Python-LSP-Server ab (`pyright-langserver` oder `python -m ruff server`). Ruff nutzt Pull-Diagnosen; ohne Server werden nur CLI-Syntax- und Format-Checks genutzt.
- Python-Formatierung hängt von lokal installierten Tools ab (`ruff format`, `black`, `autopep8`). Ohne diese Tools bleibt der Text unverändert oder zeigt eine Meldung.
- C#-Syntax-Check prüft primär Syntax; semantische Compilerfehler aus vollständigen Projekten werden nicht erkannt.
- YAML-Folding basiert auf Einrückung und kann bei stark gemischten YAML-Strukturen ungenau sein.

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Update-Prüfung über GitHub-Releases (stable Versionen ignorieren Prereleases)
- Dateiendungen: `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

## 0.2.0-beta
Kurz: Beta mit Markdown, Python, C# und YAML (Highlighting, Syntax-Check, Formatierung, Folding).

### Neue Features
- Syntax-Highlighting für Markdown, Python, C# und YAML (Markdown/YAML über eigene XSHD-Definitionen im Assembly)
- Markdown-Live-Vorschau für `.md` und `.markdown` über MdXaml
- Python-Live-Syntax-Check über LSP-Pull-Diagnosen (`textDocument/diagnostic`) oder Python-CLI (`py_compile`/`ruff`/`flake8`); LSP-Server bevorzugt `pyright-langserver`, sonst `python -m ruff server`
- Python-Completion über AvalonEdit `CompletionWindow` bei `.` und `Strg+Leertaste`
- Python-Formatierung über `ruff format`, `black` oder `autopep8`
- C#-Syntax-Check mit Roslyn und Code-Formatierung mit Roslyn-Formatter (`Strg+Umschalt+F`)
- YAML-Syntax-Check mit YamlDotNet und Folding nach Einrückung über `IndentFoldingStrategy`
- Sprachen-Menü mit Markdown, Python, C# und YAML sowie „Code formatieren“ und „Python-Completion“
- Öffnen-Dialog mit Filtern für `.md`, `.py`, `.cs` und `.yaml`
- GitHub-Update-Prüfung beim Start und „Update prüfen“ im „Über NovaNote“-Dialog

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Dateiendungen: `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

### Bekannte Einschränkungen der Beta
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- Nur `.txt`, `.md`, `.markdown`, `.json`, `.xml`, `.html` werden verknüpft; andere Endungen öffnet man über „Öffnen…“ oder die Kommandozeile.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
- Python-LSP hängt von einem verfügbaren Python-LSP-Server ab (`pyright-langserver` oder `python -m ruff server`). Ruff nutzt Pull-Diagnosen; ohne Server werden nur CLI-Syntax- und Format-Checks genutzt.
- Python-Formatierung hängt von lokal installierten Tools ab (`ruff format`, `black`, `autopep8`). Ohne diese Tools bleibt der Text unverändert oder zeigt eine Meldung.
- C#-Syntax-Check prüft primär Syntax; semantische Compilerfehler aus vollständigen Projekten werden nicht erkannt.
- YAML-Folding basiert auf Einrückung und kann bei stark gemischten YAML-Strukturen ungenau sein.

### Behoben / Fixes
- Hell-Schema: `SelectionColor` war `#005FB859` (Alpha `00` = komplett transparent), markierter Text war nicht sichtbar. Jetzt `#59005FB8`.
- Dunkel-Schema: `SelectionColor` `#4CC2FF59` korrigiert auf `#594CC2FF` (richtiger Akzent-Ton statt Grün-Stich).
- Rechtsklick-Menü hatte keinen Theme-Stil: im dunklen Modus heller Popup-Hintergrund mit hellem Text (unkontrastierend). `Theme/Controls.xaml` hat jetzt einen `ContextMenu`-Style mit `PopupBgBrush`, `PopupBorderBrush` und `TextBrush` (dunkel im Dark, hell in Light).
- Kontextmenü bleibt offen und blendet unpassende Einträge aus.

## 0.1.0-beta
Kurz: Erste öffentliche Beta — WPF-Editor auf AvalonEdit mit Highlighting, Syntax-Check, Folding, Mehrfach-Tabs und Themes.

### Features
- WPF-Editor auf Basis von AvalonEdit
- Syntax-Highlighting für JSON, XML, HTML, CSS, JavaScript, C++
- Syntax-Check mit roter Wellenlinie und `F4` zum nächsten Fehler
- Code-Folding für JSON, XML, HTML, CSS, JavaScript und C++
- Mehrfach-Tabs, Tab-Detach, Drag & Drop
- Themes: hell, dunkel, system
- Encoding- und Zeilenenden-Umschaltung
- Suchen/Ersetzen, Goto, Zoom, Drucken, Seiteneinrichtung
- Session-Wiederherstellung unter `%APPDATA%\NovaNote\`

### Installation / Releases
- Velopack-Installer: `NovaNote-win-Setup.exe`
- Portable-Zip: `NovaNote-win-Portable.zip`
- Dateiendungen: `.txt`, `.json`, `.xml`, `.html` über ProgID `NovaNote.Editor`

### Bekannte Einschränkungen der Beta
- Dateizuordnung schreibt nur HKCU; Windows-`UserChoice` wird nicht gesetzt.
- JavaScript-Syntax-Check verwendet Acornima tolerante Fehler; nicht alle Fehler werden zuverlässig gefunden.
- TypeScript wird angezeigt, aber nicht geprüft.
