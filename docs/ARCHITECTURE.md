# NovaNote — interne Architektur-Landkarte

Repo: https://github.com/jsabrautzky-oss/NovaNote  
Aktuelle Beta: 0.1.0-beta  

Diese Datei ist nur für interne Coding-/KI-Agenten gedacht und nicht für öffentliche Nutzerdoku. Sie soll Scannen des ganzen Codes vermeiden. Alles Wichtige ist hier zusammengefasst: Komponenten, Abhängigkeiten, öffentliche APIs, Fluss, Fallstricke.

## Zweck
WPF-Editor „NovaNote“ für Windows (.NET 9) mit AvalonEdit-Editor, Syntax-Highlighting, Syntax-Check mit roter Wellenlinie, Code-Folding, Mehrfach-Tabs, Session-Wiederherstellung, Theme (hell/dunkel/system), Drucken, Suchen/Ersetzen, Goto, Encoding/EOL-Umschaltung, Drag & Drop, Tab-Detach, Velopack-Installer und Dateiendungen `.txt/.json/.xml/.html`.

## Build / Installer
```powershell
dotnet build NovaNote/NovaNote.csproj -c Release --nologo
dotnet publish NovaNote/NovaNote.csproj -c Release -r win-x64 --self-contained true -o "$env:TEMP\NovaNote-publish" --nologo
```

Velopack-Installer lokal erzeugen:
```powershell
powershell .\installer.ps1
```

Das Script erzeugt:
- `publish/` als self-contained `win-x64`-Ablage
- `Releases/NovaNote-win-Setup.exe` als Benutzer-Installer
- Updates/Portable-Pakete im `Releases`-Ordner

GitHub-Release erzeugen und hochladen (nach `gh auth login` oder mit `GH_TOKEN`):
```powershell
powershell .\release.ps1 -version 0.1.0-beta -repo jsabrautzky-oss/NovaNote -tag v0.1.0-beta -title "NovaNote 0.1.0-beta" -prerelease $true
```

Wichtige Velopack-Parameter:
- `packId`: `NovaNote`
- `mainExe`: `NovaNote.exe`
- `packVersion`: `1.0.0`
- `icon`: `NovaNote\novanote.ico`
- `runtime`: `win-x64`

Kein Testframework im Repo. Frühere Verifikation lief über temporären WPF-Harness:
`C:\Users\juerg\AppData\Local\Temp\opencode\syntaxTest` (referenziert `NovaNote/NovaNote.csproj`; prüft Syntax-Check, Folding, Theme-Brushes, Renderer-Geometrien). Harness ist nicht Teil des Projekts.

## NuGet-Pakete
| Paket | Zweck | Genutzte API |
|---|---|---|
| `AvalonEdit` 6.3.1.120 | Editor, Highlighting, Folding, Rendering | `TextEditor`, `TextArea`, `TextView`, `TextDocument`, `BackgroundGeometryBuilder`, `IBackgroundRenderer`, `FoldingManager`, `XmlFoldingStrategy`, `HighlightingColorizer`, `UndoStack` |
| `Acornima` 1.9.0 | JS-Parsing für Syntax-Check | `Parser(ParserOptions{Tolerant,ErrorHandler}).ParseScript(string)`, `ParseErrorCollector`, `ParseError.Index/LineNumber/Column/Description`, `ParseErrorException` |
| `HtmlAgilityPack` 1.13.1 | HTML-Parsing für Syntax-Check | `HtmlDocument.LoadHtml(string)`, `HtmlDocument.ParseErrors`, `HtmlParseErrorCode`, `HtmlDocument.MaxDepthLevel` (static!) |
| `System.Text.Encoding.CodePages` 9.0.11 | ANSI/DOS-Encoding | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in `Codec.cs` |
| `Velopack` 1.2.161 | Installer, Updates, FirstRun/AfterUpdate/BeforeUninstall-Hooks | `VelopackApp.Build().SetAppUserModelId(...).SetArgs(...).Run()` |

JSON/XML-Check nutzen BCL: `System.Text.Json`, `System.Xml.XmlReader`.

## Dateiübersicht und Abhängigkeiten
| Datei | Rolle | Wichtige Zusammenhänge |
|---|---|---|
| `NovaNote.csproj` | `net9.0-windows`, `UseWPF`, `WinExe`, `StartupObject=NovaNote.Program`, Packages, embedded `Highlighting/Json.xshd` | Assembly/RootNamespace `NovaNote` |
| `Program.cs` | echter Einstiegspunkt, Velopack zuerst, dann WPF `App.Run()` | setzt `App.StartupArgs`, ruft `VelopackApp.Build().SetAppUserModelId(FileAssociationHelper.ProgId).SetArgs(args).Run()` |
| `App.xaml` / `App.xaml.cs` | merged Dicts `Theme/Light.xaml` + `Theme/Controls.xaml`, Argumente `/?`, `/N`, `/S`, Dateipfad, Session-Windows | nutzt `App.StartupArgs`, ruft `SessionStore.LoadPrefs()`, `ThemeService.SetMode()`, erstellt `MainWindow` |
| `MainWindow.xaml(.cs)` | Fenster, Tab-UI, Menü, Statusbar, RoutedCommands+KeyBindings, Dialogsteuerung, Session speichern | besitzt `ObservableCollection<TabModel> Tabs`, `ActiveView`; verwendet `DocumentView`, Dialoge, `ThemeService`, `SessionStore`, `Codec` |
| `DocumentView.xaml(.cs)` | zentrale Editor-Komponente (UserControl mit `ae:TextEditor x:Name="Editor"`) | AvalonEdit-Dokument, Highlighting, Syntax-Check, Folding, Undo, Encoding/EOL, Search, Print, Session-Restore; Events `StateChanged`, `DirtyChanged` |
| `FileAssociationHelper.cs` | HKCU-Dateizuordnungen, ProgID, Velopack-Fluent-Adapter | ProgID `NovaNote.Editor`, Description `NovaNote Editor`, Endungen `.txt/.json/.xml/.html`; `WithFirstRun/WithAfterUpdate/WithBeforeUninstall` |
| `Highlight.cs` | `DocLanguage` (None/Json/Xml/Html/Css/JavaScript/Cpp), `DocLanguages.FromExtension/DetectContent/DisplayName` | wird von DocumentView/MainWindow/Syntax/Folding verwendet |
| `Codec.cs` | Encoding/EOL: `NovaNoteEncoding`, `LineEnding`, Detect/Decode/Encode/NormalizeToLf/ApplyEol | registriert CodePages-Provider; von DocumentView/Statusbar/session genutzt |
| `Highlighting/DocHighlighting.cs` | Theme-Farb-Colorizer und XSHD-Definitionen | `ThemeHighlightingColorizer`, `DefinitionFor(DocLanguage)`, `InvalidateBrushes()`, `BrushForKey(string)`; JSON über embedded `Json.xshd`; XML/HTML/CSS/JS/C++ über AvalonEdit-XSHD |
| `Highlighting/Json.xshd` | Embedded JSON-Highlighting | LogicalName `NovaNote.Highlighting.Json.xshd` |
| `SyntaxCheck.cs` | Fehlerliste für Syntax-Check | `SyntaxError {Offset,Length,Message}`, `SyntaxChecker.Validate(lang,text)`, `ShouldCheck(lang,path)`; nutzt Json/Xml/HAP/Acornima/CSS/C++-Scanner |
| `SyntaxErrorRenderer.cs` | rote Wellenlinie als `IBackgroundRenderer` auf `KnownLayer.Selection` | `BackgroundGeometryBuilder.GetRectsForSegment`, Brush-Resolver → `AppBrush("SyntaxErrorBrush")` |
| `Folding.cs` | `FoldingScanner.CreateBrackets` für JSON/CSS/JS/C++ | XML/HTML nutzen AvalonEdit `XmlFoldingStrategy` in DocumentView |
| `Theme/Light.xaml`, `Theme/Dark.xaml` | Theme-Farb-Brushes | u. a. `EditorBgBrush`, `TextBrush`, `SubtleTextBrush`, `SelectionBrush`, `Syntax*Brush`, `SyntaxErrorBrush` |
| `Theme/Controls.xaml` | Control-Styles für WPF-Controls | global gemerged |
| `Theme/ThemeService.cs` | hell/dunkel/system, DWM-Titelleiste, `ThemeChanged` | ersetzt gemergte Theme-Dictionary in `Application.Current.Resources.MergedDictionaries` |
| `UpdateService.cs` | GitHub Update-Check und Setup-Download | `Program.Main` läuft zuerst; App ruft `UpdateService.StartStartupCheck(window)`; Manual über `AboutDialog`/`MainWindow`; Setup wird silent mit `App.StartupArgs` gestartet |
| `SessionStore.cs` | `AppPrefs`, `SessionState`, JSON in `%APPDATA%\NovaNote\einstellungen.json` und `sitzung.json` | Prefs: RestoreSession, MultiTab, LanguageAuto, ShowLineNumbers, ShowWhitespace, SyntaxCheck, Folding, Theme, UpdateCheck, DismissedUpdateVersion |
| `Dialogs/*.xaml(.cs)` | Suche/Ersetzen, Goto, Schriftwahl, About, PageSetup | `SearchDialog`/`GoToDialog` haben `MainWindow`-Referenz und rufen Such/Goto-API |
| `app.manifest`, `novanote.ico` | DPI/Manifest, Icon | Icon für EXE, Installer und Explorer-DefaultIcon |
| `installer.ps1` | Publish + `vpk pack` zu Velopack-Installer | erzeugt `publish/` und `Releases/` |
| `Releases/` | Velopack-Pakete, nicht Build-Input | enthält z. B. `NovaNote-win-Setup.exe` |
| `publish/` | Publish-Ablage für Velopack, nicht Build-Input | wird von `installer.ps1` erzeugt |

## Architektur-Fluss
1. `Program.Main` → `App.StartupArgs` → `VelopackApp.Build().Run()` → `App.InitializeComponent()` → `App.Run()`.
2. `App.OnStartup` → `SessionStore.LoadPrefs()` → `ThemeService.SetMode(Prefs.Theme)` → MainWindow(s) mit Session.
3. `MainWindow` erzeugt pro Tab `DocumentView` über `NewView()` und hält `TabModel { View, Title, Width }`.
4. `DocumentView` kapselt AvalonEdit: öffentliche API bewusst stabil (kein `RichTextBox` mehr; alles über `Editor.CaretOffset`, `Editor.SelectionStart/Length`, `Editor.Select`, `Editor.Document`, `Editor.ScrollTo`, `Editor.TextArea`).
5. `Document.TextChanged` setzt Dirty und startet Debounce-Timer:
   - Syntax-Check: 400 ms, Text-Snapshot, `Task.Run`, Rückkehr über `Dispatcher`, Guard `ReferenceEquals(_syntaxCts, cts)`.
   - Folding: 350 ms; JSON/CSS/JS/C++ Hintergrund-Scan; XML/HTML synchron `XmlFoldingStrategy`.
6. `MainWindow.UpdateStatusNow()` liest `v.GetStatus()`, `v.SyntaxStatus`, `v.Enc/Eol/Zoom/StatusLanguage` und aktualisiert `StatusSyntax` mit `SyntaxErrorBrush`.
7. Session wird beim Schließen geserialized (`SessionStore.SaveSession`), Preferences bei Menü-Toggles (`SavePrefs`).

## Update-Fluss
- `UpdateService.CheckAsync` prüft GitHub Releases und sucht das Asset `NovaNote-win-Setup.exe`.
- Automatischer Check beim Start: nur wenn installiert, nicht portable/dev, und `SessionStore.Prefs.UpdateCheck` aktiv ist.
- Manual check: Button in AboutDialog und Menü „Nach Update prüfen“; funktioniert auch dev/portable als Download-Test.
- Bei Update: Prompt, dirty Tabs speichern, Session speichern, Setup nach `%TEMP%` herunterladen, `Setup.exe --silent -- [StartupArgs]` starten, MainWindow-Closing mit `_closingForUpdate` unterdrücken, `Application.Shutdown(0)`.
- GitHub-Fehler werden silent geloggt (`%APPDATA%\NovaNote\update.log`); manual check zeigt Fehler als MessageBox.

## Velopack- und Dateiassozierung-Fluss
- `Program.Main` führt `VelopackApp` vor der WPF-App aus, damit Update-/Installer-Specialargs korrekt behandelt werden.
- `SetAppUserModelId(FileAssociationHelper.ProgId)` nutzt `NovaNote.Editor` für Shortcuts/Taskbar-AUMID.
- FirstRun und AfterUpdate registrieren HKCU-Dateizuordnungen für `.txt/.json/.xml/.html`.
- BeforeUninstall entfernt die eigene ProgID und Endungen, wenn sie vorher auf `NovaNote.Editor` zeigten.
- `RegisterAssociations()` schreibt nur `HKEY_CURRENT_USER\Software\Classes` und ruft `SHChangeNotify(SHCNE_ASSOCCHANGED)`.

## Öffentliche API (Kurzfassung)
### `DocumentView`
- Eigenschaften: `Path_`, `IsDirty`, `Enc`, `Eol`, `Zoom`, `WordWrap`, `Lang`, `StatusLanguage`, `TabTitle`, `SyntaxErrors`, `SyntaxStatus`, `HasSyntaxErrors`, `SyntaxCheckEnabled`, `FoldingEnabled`, `CanUndo/CanRedo`
- Text/Status: `PlainText()`, `CurrentText`, `LineCount`, `GetStatus()`, `StatusCaret`, `StatusSelection`, `GetScrollState()`, `RestoreScrollState()`
- Bearbeiten/Navigation: `SetFont`, `SetBaseFont`, `SetZoom`, `SetWordWrap`, `SetEncoding`, `SetEol`, `SetLanguage`, `DetectLanguage`, `ReapplyHighlight`, `DoUndo/DoRedo/ClearUndoStack`, `DoCut/Copy/Paste/Delete/SelectAll`, `FocusEditor`, `GoToLine`, `InsertDateTime`
- Syntax/Folding: `SetSyntaxCheck(bool)`, `GoToNextSyntaxError()`, `SetFolding(bool)`
- Dateien: `OpenFile(string)`, `PromptDirty()`, `SavePath()`, `Save()`, `SaveAsCore()`, `WriteFile(string)`, `ReplaceText(string)`, `FormatJson(out string)`
- Suche/Drucken/Session: `DoSearch`, `ReplaceCurrent`, `ReplaceAll`, `Print(...)`, `RestoreSession(...)`, `TransferOut()`

### `MainWindow`
- `Tabs`, `Active`, `ActiveView`, `Detached`
- statische globale Zustände: `BaseFamily/BaseStyle/BaseWeight/BaseSize`, `LastPrinterName`, `MargL/T/R/B`, `ClosedHistory`
- Tab/Datei: `NewView()`, `FindTab(view)`, `OpenInNewTab(path)`, `CloseTab(tab)`
- Suche/Dialoge: `DoSearch`, `ReplaceCurrent`, `ReplaceAll`, `ShowSearch(bool)`, `ShowGoTo()`, `GoToLine(int)`, `SaveSearchArgs`, `SetPrintMargins`
- `Commands` mit Bindings: Ctrl+T/O/S/Shift+S/P/F/F3/Shift+F3/Ctrl+H/Ctrl+G/F5/Zoom/Shift+W/Ctrl+Shift+T/Ctrl+Tab/Ctrl+Shift+Tab/Ctrl+Shift+D/Ctrl+Shift+W/`Ctrl+Shift+J` JSON-Format/`F4` nächster Syntaxfehler.

### `SessionStore`
- `AppPrefs` (siehe oben), `LoadPrefs()`, `SavePrefs()`, `HasSession`, `LoadSession()`, `SaveSession(SessionState)`, `ClearSession()`
- Session enthält Windows mit Tabs: Path, Text, Enc, Eol, Zoom, Wrap, ScrollX/Y, Caret, Dirty, Language.
- Session-/Prefs-Ordner: `%APPDATA%\NovaNote\`.

### `ThemeService`
- `SetMode("hell"|"dunkel"|"system")`, `Apply()`, `IsDark`, `ThemeChanged`, `AttachWindow(Window)`, `ApplyTitleBar(Window)`
- Ersetszt Theme-Dictionary durch `pack://application:,,,/Theme/Dark.xaml` oder `Light.xaml` (nur gültig, wenn das Theme im Entry-Assembly liegt).

### `DocHighlighting`
- `DefinitionFor(DocLanguage)`, `ThemeHighlightingColorizer`, `BrushForKey`, `BrushForColorName`, `InvalidateBrushes`
- Theme-Brush-Keys: `TextBrush`, `SyntaxStringBrush`, `SyntaxNumberBrush`, `SyntaxKeywordBrush`, `SyntaxKeyBrush`, `SyntaxPunctBrush`, `SyntaxTagBrush`, `SyntaxAttrBrush`, `SyntaxCommentBrush`, `SyntaxPreprocBrush`, `SyntaxErrorBrush`

### `SyntaxChecker`
- `MaxErrors=100`, `MaxCheckedChars=2_000_000`
- `ShouldCheck(lang, path)`: `None` false; JS false bei `.ts/.mts/.cts`
- `Validate(lang, text)` → `List<SyntaxError>` mit Dokument-Offset (LF-normalisiert)

### `FoldingScanner`
- `MaxFoldedChars=1_000_000`, `MaxFoldings=500`
- `ShouldFold`: Json/Xml/Html/Css/Cpp/JavaScript
- `CreateBrackets(text, lang, out firstErrorOffset)`: nur mehrzeilige Blöcke, `{...}` und in JSON auch `[...]`; ignoriert Strings, `//`/`/* */` je nach Sprache, JS-Template-Literals.

## Testharness-Muster (wenn KI UI verifizieren will)
- Temporäres WPF-Projekt mit `ProjectReference` auf `NovaNote/NovaNote.csproj`, `Application` erzeugen, Theme als `pack://application:,,,/NovaNote;component/Theme/Light.xaml` mergen.
- Dispatcher-Pump mit `DispatcherFrame` + `DispatcherTimer`; für Foldings/Syntax statusabhängig warten, nicht nur „non-empty“.
- Wellenlinie verifizieren durch `renderer.Draw(TextView, DrawingContext)` in `DrawingVisual` und Auswertung der `GeometryDrawing.Pen.Brush`-Farbe, nicht RenderTargetBitmap.

## Fallstricke
1. **Velopack-Main**: `VelopackApp.Build().Run()` sollte im echten `Main` ganz oben laufen. `App.OnStartup` ist dafür nicht ideal und erzeugte früher einen vpk-Warnhinweis. Jetzt steht die Velopack-Logik in `Program.cs`.
2. **StartupArgs**: WPF-OnStartup erhält nicht zuverlässig die `Main`-Argumente; die App nutzt deshalb `App.StartupArgs`, gesetzt von `Program.Main`.
3. **HKCU vs. UserChoice**: Windows kann `UserChoice` nicht von einer Anwendung gesetzt werden. Die App schreibt nur HKCU-`Software\Classes` und ProgIDs; für Doppelklick-Default kann Windows eine Benutzerauswahl bevorzugen.
4. **AvalonEdit-Offsets und LF**: `DocumentView` arbeitet intern mit LF-normalisiertem Text. Offsets aus Syntax/Folding sind Document-Offsets, keine Datei-Byteoffsets. Bei UTF-8-JSON ist `JsonException.BytePositionInLine` eine **Byte-Position**, muss zu Zeichenposition zurückgerechnet werden; `JsonException.LineNumber` kann `0` sein.
5. **JSON-Fehlerposition**: `JsonException.LineNumber`/`BytePositionInLine` sind nullable (`long?`) und zeigen oft auf das Ende eines Tokens; `TokenStart()` läuft daher zum Tokenanfang zurück.
6. **XML**: `XmlException.LineNumber` ist 1-based, `LinePosition` wurde empirisch 1-based behandelt (`-1`). Ungültige Dateien brechen Lesevorgang mit erster Exception → nur ein Fehler.
7. **HTML/HAP**: `HtmlDocument.MaxDepthLevel` ist **static**, nicht Instanz-Property. `LoadHtml("<div><span>x</div>")` erzeugt oft **keine** ParseErrors (auto-close). Zuverläßliche Fehler: EOF-unkclosed (`TagNotClosed`), `TagNotOpened`. `EndTagNotRequired`/`CharsetMismatch` werden bewusst ignoriert. `StreamPosition` kann als absoluter Offset verwendet werden, aber Falltests prüfen.
8. **Acornima**: API heißt `ParseScript(...)`, nicht `ParseProgram(...)`. `Tolerant=true` mit `ParseErrorCollector` sammelt nicht alle Fehler robust — der erste Fehler kann als `ParseErrorException` geworfen werden. Erwartung: mindestens ein Fehler, nicht viele.
9. **Folding-Strategien**: `XmlFoldingStrategy` läuft synchron auf dem UI-Thread; Klammer-Scanner laufen im Hintergrund und werden per Dispatcher angewendet. `FoldingManager.UpdateFoldings` erwartet sortierte `NewFolding`-Liste nach `StartOffset`.
10. **FrozenBrush-Pflicht**: `FoldingMargin` friest Brushes intern ein (`MakeFrozenPen`). DynamicResource-Brushes (z. B. `SubtleTextBrush`) können nicht eingefroren werden. In `DocumentView.ApplyTheme()` wird deshalb ein kopierter, eingefrorener `SolidColorBrush` gesetzt. Für eigene `Pen`-Erzeugung analog Vorsicht.
11. **Renderer-Layer**: Wellenlinien werden auf `KnownLayer.Selection` gezeichnet; nach Errors/Theme muss `Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection)` aufgerufen werden.
12. **Debounce/Races**: Syntax und Folding nutzen Timer + `CancellationTokenSource` + `ReferenceEquals(_cts, cts)` auf UI-Rückkehr. Alter Zustand kann sonst nach Dokumentwechsel kurz sichtbar sein. Tests müssen gezielt auf neuen Status/Foldings warten.
13. **TypeScript**: wird nur angezeigt, aber nicht geprüft (Acornima versteht TS-Typen nicht). Folding kann JS-Klammern zeigen, aber kein TS-Check.
14. **Theme-Pack-URI**: `ThemeService.Apply()` nutzt `/Theme/Dark.xaml` ohne Assembly-Komponente. Das funktioniert im echten App-Assembly, nicht in einem Referenz-Harness. Harness muss `/NovaNote;component/Theme/...` verwenden oder Theme manuell swappen.
15. **RenderTargetBitmap**: im Harness waren typische `PixelFormat`-Varianten nicht nutzbar; besser DrawingVisual/Geometrie prüfen statt Pixelbitmap.
16. **`_loading`-Guard**: `SetText/OpenFile/RestoreSession/ReplaceAll/FormatJson` setzen `_loading`, dadurch wird `Document.TextChanged` suppressed. Nach solchen Aktionen müssen `ScheduleSyntaxCheck()`/`ScheduleFolding()` explizit aufgerufen werden.
17. **Große Dateien**: Syntax-Check stoppt > 2 MB chars, Folding > 1 MB chars / 500 Foldings. Nicht ohne Grund entfernen; UI bleibt sonst langsam.
18. **Drucken**: `DocumentView.Print()` baut manuell `FlowDocument`; kein AvalonEdit-Print. `PrintDialog`-Aufruf ist modal und owner-abhängig.
19. **Encoding**: `Codec.Encode/Decode` behandelt UTF-8/UTF-8-BOM/ANSI/DOS/etc. `Codec.NormalizeToLf` und `ApplyEol` sind die Grenze zwischen UI-Text (LF) und Datei-EOL.
20. **Session/Prefs**: Dateien liegen in `%APPDATA%\NovaNote\` (`einstellungen.json`, `sitzung.json`). KI sollte nicht testen, wenn sie echte Nutzerdaten zerstören will; Harness darf `LoadPrefs()` nutzen, aber keine `SavePrefs()`-Seiteffekte ohne Bedacht.
21. **Public-API-Stabilität**: Dialoge/MainWindow erwarten die dokumentierte `DocumentView`-API. Neue interne Editor-API (z. B. direkte AvalonEdit-Zugriffe) nur wo nötig; `Editor` ist in XAML internal, Harness findet TextArea/TextView/FoldingManager über VisualTree oder `TextArea.GetService(typeof(FoldingManager))`.
22. **UTF-8-Encoding der Quelltexte**: Deutsch im UI/Kommentaren ist UTF-8. Ältere PowerShell-Reparaturen konnten UTF-8 als CP1252 lesen und doppelkodieren. Quelltext-Bearbeitungen in PowerShell nur mit expliziter UTF-8-E/A-Routine machen.

## Kurzregeln für KI
- Nicht den ganzen Code scannen; diese README als Landkarte nutzen.
- Änderungen an Editor/Syntax/Folding primär in `DocumentView.xaml.cs`, Scanner in `SyntaxCheck.cs`/`Folding.cs`, UI in `MainWindow.xaml(.cs)`, Farben in `Theme/*.xaml`, Installer in `installer.ps1`/`Program.cs`.
- Nach Änderungen: `dotnet build` + optional temp-Harness + Publish-Smoke.
- Deutsch im UI, deutsche Fehlermeldungen/Statustexte beibehalten.
