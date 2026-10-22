# NovaNote — interne Architektur-Landkarte

Repo: https://github.com/jsabrautzky-oss/NovaNote  
Aktuelle Version: 0.5.0  

Diese Datei ist nur für interne Coding-/KI-Agenten gedacht und nicht für öffentliche Nutzerdoku. Sie soll Scannen des ganzen Codes vermeiden. Alles Wichtige ist hier zusammengefasst: Komponenten, Abhängigkeiten, öffentliche APIs, Fluss, Fallstricke.

## Zweck
WPF-Editor „NovaNote“ für Windows (.NET 9) mit AvalonEdit-Editor, Syntax-Highlighting, Syntax-Check mit roter Wellenlinie, Code-Folding, Markdown-Live-Vorschau für `.md/.markdown`, Python-LSP/Live-Check/Completion/Formatierung, C#-Roslyn-Check/Formatierung, YAML-Check/Indent-Folding, Mehrfach-Tabs, Session-Wiederherstellung, Theme (hell/dunkel/system), Drucken, Suchen/Ersetzen, Goto, Encoding/EOL-Umschaltung, Drag & Drop, Tab-Detach, Velopack-Installer und verknüpfte Dateiendungen `.txt/.md/.markdown/.json/.xml/.html` (ProgID `NovaNote.Editor`, nur HKCU). Optional lokaler KI-Assistent im rechten Chat-Panel gegen OpenAI-kompatible Server (LM Studio/Ollama/`llama-server`) mit Tool-Calls auf den Editor; ohne Server keine KI.

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
powershell .\release.ps1 -version 0.5.0 -repo jsabrautzky-oss/NovaNote -tag v0.5.0 -title "NovaNote 0.5.0" -prerelease $false
```

Wichtige Velopack-Parameter:
- `packId`: `NovaNote`
- `mainExe`: `NovaNote.exe`
- `packVersion`: `0.5.0`
- `icon`: `NovaNote\novanote.ico`
- `runtime`: `win-x64`

Kein Testframework im Repo. Frühere Verifikation lief über temporären WPF-Harness:
`C:\Users\juerg\AppData\Local\Temp\opencode\syntaxTest` (referenziert `NovaNote/NovaNote.csproj`; prüft Syntax-Check, Folding, Theme-Brushes, Renderer-Geometrien, Markdown-Preview-Visibility/live update sowie Python/C#/YAML-Extensions, Syntax-Checker, Highlight-Definitionen, C#-/YAML-UI-Checks und YAML-Indent-Folding). Harness ist nicht Teil des Projekts.

## NuGet-Pakete
| Paket | Zweck | Genutzte API |
|---|---|---|
| `AvalonEdit` 6.3.1.120 | Editor, Highlighting, Folding, Rendering | `TextEditor`, `TextArea`, `TextView`, `TextDocument`, `BackgroundGeometryBuilder`, `IBackgroundRenderer`, `FoldingManager`, `XmlFoldingStrategy`, `HighlightingColorizer`, `UndoStack` |
| `Acornima` 1.9.0 | JS-Parsing für Syntax-Check | `Parser(ParserOptions{Tolerant,ErrorHandler}).ParseScript(string)`, `ParseErrorCollector`, `ParseError.Index/LineNumber/Column/Description`, `ParseErrorException` |
| `HtmlAgilityPack` 1.13.1 | HTML-Parsing für Syntax-Check | `HtmlDocument.LoadHtml(string)`, `HtmlDocument.ParseErrors`, `HtmlParseErrorCode`, `HtmlDocument.MaxDepthLevel` (static!) |
| `System.Text.Encoding.CodePages` 9.0.11 | ANSI/DOS-Encoding | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in `Codec.cs` |
| `MdXaml` 1.27.0 | Markdown-Live-Vorschau | `md:MarkdownScrollViewer`, `Markdown`-Property im `DocumentView` |
| `Microsoft.CodeAnalysis.CSharp` 5.9.0 | C#-Syntax-Check | `CSharpSyntaxTree.ParseText(text).GetDiagnostics()`, `DiagnosticSeverity.Error`, `Location.GetLineSpan()` |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` 5.9.0 | C#-Formatierung | `Formatter.Format(root, workspace, workspace.Options, CancellationToken.None)`, `Microsoft.CodeAnalysis.AdhocWorkspace` |
| `YamlDotNet` 18.1.0 | YAML-Syntax-Check | `DeserializerBuilder().Build().Deserialize(new StringReader(text))`, `YamlDotNet.Core.YamlException`, `YamlException.Start.Line/Column` (`long` → `int`) |
| `Velopack` 1.2.161 | Installer, Updates, FirstRun/AfterUpdate/BeforeUninstall-Hooks | `VelopackApp.Build().SetAppUserModelId(...).SetArgs(...).Run()` |

JSON/XML-Check nutzen BCL: `System.Text.Json`, `System.Xml.XmlReader`. Python-LSP ist als eigener JSON-RPC stdio-Client (`PythonLanguageServerClient.cs`) implementiert; das NuGet-Paket `AvalonEdit.Lsp` ist nicht verfügbar.

## Dateiübersicht und Abhängigkeiten
| Datei | Rolle | Wichtige Zusammenhänge |
|---|---|---|
| `NovaNote.csproj` | `net9.0-windows`, `UseWPF`, `WinExe`, `StartupObject=NovaNote.Program`, Packages, embedded `Highlighting/Json.xshd`, `Highlighting/Markdown.xshd` und `Highlighting/Yaml.xshd` | Assembly/RootNamespace `NovaNote` |
| `Program.cs` | echter Einstiegspunkt, Velopack zuerst, dann WPF `App.Run()` | setzt `App.StartupArgs`, ruft `VelopackApp.Build().SetAppUserModelId(FileAssociationHelper.ProgId).SetArgs(args).Run()` |
| `App.xaml` / `App.xaml.cs` | merged Dicts `Theme/Light.xaml` + `Theme/Controls.xaml`, Argumente `/?`, `/N`, `/S`, Dateipfad, Session-Windows | nutzt `App.StartupArgs`, ruft `SessionStore.LoadPrefs()`, `AiStore.Load()`, `ThemeService.SetMode()`, erstellt `MainWindow` |
| `MainWindow.xaml(.cs)` | Fenster, Tab-UI, Menü, Statusbar, RoutedCommands+KeyBindings, Dialogsteuerung, Session speichern, KI-Panel | besitzt `ObservableCollection<TabModel> Tabs`, `ActiveView`; verwendet `DocumentView`, Dialoge, `ThemeService`, `SessionStore`, `Codec`, `AiStore`/`LocalAiClient`/`AiTools`; `RootGrid` hat drei Spalten (Editor, `AiSplitter`, `AiPanel`), `ApplyAiPanel(open)` setzt `Prefs.AiPanelOpen` und `Prefs.AiPanelWidth`, `AiSplitter_DragCompleted` speichert die Breite; KI-Region: `RefreshAiPanel`, `AiDetectAsync`, `AiSendAsync`/`AiRunAsync` (Agent-Loop max 6), Pending-Call/Code, `Commands.AiToggle` = `Strg+Alt+K` |
| `DocumentView.xaml(.cs)` | zentrale Editor-Komponente (UserControl mit `ae:TextEditor x:Name="Editor"` und `md:MarkdownScrollViewer x:Name="MarkdownViewer"`) | AvalonEdit-Dokument, Highlighting, Markdown-Preview, Python-LSP/Completion/Diagnostics, C#-/YAML-Format/Folding, Syntax-Check, Undo, Encoding/EOL, Search, Print, Session-Restore, Dispose von Timern/LSP, Rechtsklick-Menü `EditorMenuPopup`; Events `StateChanged`, `DirtyChanged`, `ContextAction(DocumentView, DocAction, bool)`; KI-API: `CurrentText`, `GetLines`, `FindAll`, `ReplaceAllText`, `ReplaceLines`, `InsertLine`, `SelectLines`, `GoToLineQuiet`, `SelectedText`, `SyntaxHasErrors`, `FormatSupported` (kein `ReplaceText` für KI — verwirft Undo) |
| `FileAssociationHelper.cs` | HKCU-Dateizuordnungen, ProgID, Velopack-Fluent-Adapter | ProgID `NovaNote.Editor`, Description `NovaNote Editor`, Endungen `.txt/.md/.markdown/.py/.pyw/.cs/.yaml/.yml/.json/.xml/.html`; `WithFirstRun/WithAfterUpdate/WithBeforeUninstall` |
| `Highlight.cs` | `DocLanguage` (None/Json/Xml/Html/Css/JavaScript/Cpp/Markdown/Python/CSharp/Yaml), `DocLanguages.FromExtension/DetectContent/DisplayName` | wird von DocumentView/MainWindow/Syntax/Folding verwendet; `.md/.markdown` → Markdown, `.py/.pyw` → Python, `.cs/.csx` → CSharp, `.yaml/.yml` → Yaml; Enum nur am Ende anhängen, damit Session-Language-Ints kompatibel bleiben |
| `Codec.cs` | Encoding/EOL: `NovaNoteEncoding`, `LineEnding`, Detect/Decode/Encode/NormalizeToLf/ApplyEol | registriert CodePages-Provider; von DocumentView/Statusbar/session genutzt |
| `Highlighting/DocHighlighting.cs` | Theme-Farb-Colorizer und XSHD-Definitionen | `ThemeHighlightingColorizer`, `DefinitionFor(DocLanguage)`, `InvalidateBrushes()`, `BrushForKey(string)`; JSON über embedded `Json.xshd`; Markdown über embedded `Markdown.xshd`; Python/C# über AvalonEdit-Standarddefinitionen `Python`/`C#`; YAML über AvalonEdit-Standard `YAML` oder fallback embedded `Yaml.xshd` mit eigener Definition `NovaNote YAML`; XML/HTML/CSS/JS/C++ über AvalonEdit-XSHD |
| `Highlighting/Json.xshd` | Embedded JSON-Highlighting | LogicalName `NovaNote.Highlighting.Json.xshd` |
| `Highlighting/Markdown.xshd` | Embedded Markdown-Highlighting | LogicalName `NovaNote.Highlighting.Markdown.xshd`; AvalonEdit-Regexes nutzen `\x23/\x3E/\x60`, damit `#` nicht als Regex-Kommentar behandelt wird |
| `Highlighting/Yaml.xshd` | Embedded YAML-Highlighting-Fallback | LogicalName `NovaNote.Highlighting.Yaml.xshd`; wird nur genutzt, wenn AvalonEdit keine `YAML`-Standarddefinition liefert; Regexes vermeiden `#` als Regex-Kommentar durch `\\x23` |
| `SyntaxCheck.cs` | Fehlerliste für Syntax-Check | `SyntaxError {Offset,Length,Message}`, `SyntaxChecker.Validate(lang,text)`, `ShouldCheck(lang,path)`; nutzt Json/Xml/HAP/Acornima/CSS/C++-Scanner, C#-Roslyn und YAML-YamlDotNet; Markdown wird übersprungen; Python wird in `DocumentView` separat über LSP/CLI geprüft, nicht durch `SyntaxChecker.Validate` |
| `SyntaxErrorRenderer.cs` | rote Wellenlinie als `IBackgroundRenderer` auf `KnownLayer.Selection` | `BackgroundGeometryBuilder.GetRectsForSegment`, Brush-Resolver → `AppBrush("SyntaxErrorBrush")` |
| `Folding.cs` | `FoldingScanner.CreateBrackets` für JSON/CSS/JS/C++/C# | XML/HTML nutzen AvalonEdit `XmlFoldingStrategy` in DocumentView; YAML nutzt `IndentFoldingStrategy`; C# nutzt `lineComments` im Bracket-Scanner |
| `PythonToolRunner.cs` | Python-/CLI-/LSP-Erkennung | cached async detection für `python`, `py_compile`/`ruff`/`flake8`, `ruff format`/`black`/`autopep8`, `pyright-langserver --stdio` oder `python -m ruff server`; lokale Umgebung hat `Python 3.12.9` und `ruff 0.14.10`, nicht `pyright`/`black`/`autopep8` |
| `PythonSyntaxChecker.cs` | Python-CLI-Live-Check | temp `.py`, `python -m py_compile` oder `python -m ruff check`/`python -m flake8`, parse Zeile/Column zu `SyntaxError` |
| `PythonFormatter.cs` | Python-CLI-Formatierung | temp `.py`, `python -m ruff format`, sonst `black --fast`/`autopep8 --in-place`, Ergebnis oder Fehlermeldung |
| `PythonLanguageServerClient.cs` | eigener Python-LSP-Client | stdio JSON-RPC mit `Content-Length`, `initialize/initialized/didOpen/didChange/completion/hover/publishDiagnostics/textDocument/diagnostic`; `didOpen.text` und `didChange.version` gehören in `textDocument`; Params-Key muss `params` sein; Dispose killt Prozess |
| `IndentFoldingStrategy.cs` | YAML-Folding | `UpdateFoldings(manager, document)`, `CreateFoldings(document, maxFoldings)` mit `NewFolding { StartOffset, EndOffset, Name }`, sortiert nach StartOffset; nutzt `document.GetText(offset,length)` |
| `Theme/Light.xaml`, `Theme/Dark.xaml` | Theme-Farb-Brushes | u. a. `EditorBgBrush`, `TextBrush`, `SubtleTextBrush`, `SelectionBrush`, `Syntax*Brush`, `SyntaxErrorBrush` |
| `Theme/Controls.xaml` | Control-Styles für WPF-Controls | global gemerged; `ToggleBtn`, `CbItem`, ComboBox-Template bindet Auswahl im geschlossenen Feld über `SelectedItem` + `ItemTemplate`, `PasswordBox`-Style |
| `Theme/ThemeService.cs` | hell/dunkel/system, DWM-Titelleiste, `ThemeChanged` | ersetzt gemergte Theme-Dictionary in `Application.Current.Resources.MergedDictionaries` |
| `UpdateService.cs` | GitHub Update-Check und Setup-Download | `Program.Main` läuft zuerst; App ruft `UpdateService.StartStartupCheck(window)`; Manual über `AboutDialog`/`MainWindow`; `DownloadSetupAsync` schließt alle Streams vor `LaunchSetup` (sonst Datei-Lock); Setup wird silent mit `App.StartupArgs` gestartet |
| `SessionStore.cs` | `AppPrefs`, `SessionState`, JSON in `%APPDATA%\NovaNote\einstellungen.json` und `sitzung.json` | Prefs: RestoreSession, MultiTab, LanguageAuto, ShowLineNumbers, ShowWhitespace, SyntaxCheck, Folding, Theme, UpdateCheck, DismissedUpdateVersion, AiPanelOpen, AiPanelWidth (180–720, aus GridSplitter), AiAutoApply |
| `Dialogs/*.xaml(.cs)` | Suche/Ersetzen, Goto, Schriftwahl, About, PageSetup, KI-Einstellungen | `SearchDialog`/`GoToDialog` haben `MainWindow`-Referenz und rufen Such/Goto-API; `AiSettingsDialog` editiert Anbieter/Adresse/API-Key und Modell-Scan und ruft `AiStore.ReplaceState` |
| `AiStore.cs` | KI-Anbieter/Modelle in `%APPDATA%\NovaNote\ki.json` | `AiKind`, `AiProvider` (API-Key DPAPI via `ProtectedData`, `[JsonIgnore] auf ApiKey/DisplayName/ActiveModel`), `AiModel`, `AiState`, `Load/Save`, `MergeScanned`, `NewId`, `SetActiveProvider`, Presets LM Studio/Ollama |
| `LocalAiClient.cs` | HTTP-Client für OpenAI-kompatible Server | `ScanAsync` (`/v1/models`, `/api/v1/models`, `/api/tags`+`/api/show`), `ChatAsync` mit `tools`/`tool_choice`, Retry ohne Tools bei Ablehnung, `ParseFallbackToolCalls` (```nova```), `ExtractCodeBlock`, `NormalizeBase`/`OpenAiBase`/`RootBase`; Kontext-Keys `contextWindow`/`context_length`/`n_ctx`, Vision via `multimodal`/`vision` |
| `AiTools.cs` | 9 Tool-Schemas + Dispatcher auf `DocumentView` | `Execute(name, argsJson, view)`, `IsEditTool`, `ShortArgs`; Edits erzeugen eine Undo-Group |
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
    - Markdown-Preview: nutzt den 400-ms-`_syntaxTimer`; nach Tick wird `MarkdownViewer.Markdown = Editor.Text` gesetzt, wenn `Lang == Markdown` oder Pfad `.md/.markdown` ist.
    - Python-LSP: der Tick sendet zuerst `SendPythonDidChange()`, dann `RunSyntaxCheck()`. Wenn der LSP ready ist, zieht `PullPythonDiagnostics()` `textDocument/diagnostic`; bei Pull-Failure nutzt `RunSyntaxCheck` CLI `PythonSyntaxChecker` als Fallback. `UpdatePythonLsp()` startet `pyright-langserver` oder `python -m ruff server`; Diagnostics aus `publishDiagnostics` und Pull werden zu `SyntaxError` mit `Severity == 1` gemappt.
    - YAML-Folding: `RunFolding` nutzt `_indentFolding.UpdateFoldings(_foldingManager, Doc)` für `DocLanguage.Yaml`.
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
- Eigenschaften: `Path_`, `IsDirty`, `Enc`, `Eol`, `Zoom`, `WordWrap`, `Lang`, `StatusLanguage`, `TabTitle`, `SyntaxErrors`, `SyntaxStatus`, `HasSyntaxErrors`, `SyntaxCheckEnabled`, `FoldingEnabled`, `CanUndo/CanRedo`, `PythonLspReady`, `FormatSupported`
- Text/Status: `PlainText()`, `CurrentText`, `LineCount`, `GetStatus()`, `StatusCaret`, `StatusSelection`, `GetScrollState()`, `RestoreScrollState()`
- Bearbeiten/Navigation: `SetFont`, `SetBaseFont`, `SetZoom`, `SetWordWrap`, `SetEncoding`, `SetEol`, `SetLanguage`, `DetectLanguage`, `ReapplyHighlight`, `DoUndo/DoRedo/ClearUndoStack`, `DoCut/Copy/Paste/Delete/SelectAll`, `FocusEditor`, `GoToLine`, `InsertDateTime`
- Syntax/Folding: `SetSyntaxCheck(bool)`, `GoToNextSyntaxError()`, `SetFolding(bool)`
- Dateien: `OpenFile(string)`, `PromptDirty()`, `SavePath()`, `Save()`, `SaveAsCore()`, `WriteFile(string)`, `ReplaceText(string)`, `FormatJson(out string)`, `FormatCodeAsync()`, `ShowPythonCompletionAsync()`, `Dispose()`
- Suche/Drucken/Session: `DoSearch`, `ReplaceCurrent`, `ReplaceAll`, `Print(...)`, `RestoreSession(...)`, `TransferOut()`
- Events: `StateChanged`, `DirtyChanged`, `ContextAction(view, DocAction, bool value)` (value = neuer Zustand für Toggle-Aktionen)

### Rechtsklick-Menü (`DocumentView.EditorMenuPopup`)
- Eigenes `Popup x:Name="EditorMenuPopup"` im Grid mit `Border` + `StackPanel` aus `MenuItem`s (kein `ContextMenu`): WPF öffnet einen `ContextMenu` nur über seinen internen Popup-Mechanismus, ein eigener Template-Style öffnet dort nichts zuverlässig.
- `Editor_PreviewRightDown` merkt die Selection (`_menuSelStart/_menuSelLen`), weil AvalonEdit per Rechtsklick die Selection aufhebt; `RestoreMenuSelection()` wird vor Cut/Copy/Delete aufgerufen.
- Öffnen im Preview-Handler: `PlacementTarget = Editor.TextArea`, `Placement = MousePoint`, `IsOpen = true`, `Focus()`. `StaysOpen="True"` ist nötig, sonst schließt der Popup schon beim Loslassen der rechten Maustaste. Geschlossen wird über `Menu_Opened`/`Menu_Closed`: im geöffneten Zustand wird am Fenster `Mouse.PreviewMouseDownEvent` (`handledEventsToo=true`) gehört; `Menu_OutsideMouseDown` schließt nur, wenn `e.OriginalSource` nicht im Popup-Child liegt (`IsInsidePopup` läuft Visual-/Logical-Tree aufwärts). `Ctx_Click` schließt nach der Aktion und gibt den Fokus an `Editor` zurück; Escape schließt via Popup-Fokus.
- Submenüs sind zu flachen Items (`Vergrößern`/`Verkleinern`/`Zoom zurücksetzen`) aufgelöst, weil `MenuItem.Role` im Markup read-only ist und ein Submenü in einem `StackPanel` kein `SubmenuHeader` wird.
- Deaktivierte Einträge werden nicht grau angezeigt, sondern ausgeblendet: `UpdateContextMenuState()` setzt `Visibility` über `Show(item, bool)`; die benannten Separatoren (`CtxSepEdit`, `CtxSepClipboard`, `CtxSepFormat`, `CtxSepSyntax`, `CtxSepSearch`, `CtxSepSave`) werden mit `Sep(beforeVisible, afterVisible)` ebenfalls ausgeblendet, wenn eine angrenzende Gruppe leer ist.
- `UpdateContextMenuState()` läuft bei jedem `StateChanged` (Text/Caret/Selection/Language/Zoom/Wrap/Syntax) sowie beim Öffnen: Copy/Cut/Delete nur mit Selection, Paste nur mit `Clipboard.ContainsText()` (try/catch), Undo/Redo nach UndoStack, SelectAll nur bei Inhalt, Save nur bei dirty, Zoom-Reset nur bei Zoom != 100, `CtxFormatJsonItem` nur JSON, `CtxFormatCodeItem` nur Python/C#, `CtxPythonCompletionItem` nur Python (enabled nur wenn LSP ready), `CtxMarkdownPreviewItem` nur bei offener Markdown-Vorschau, `CtxNextErrorItem` nur bei Syntaxfehlern, Checkable Items spiegeln Syntax-Check/Folding/Zeilenumbruch, Detach nur im Multi-Tab-Modus.
- Menüitems nutzen `Tag` (Name von `DocAction`) und einen gemeinsamen `Ctx_Click`; MainWindow `View_ContextAction` aktiviert zuerst den Tab und ruft die bestehenden Funktionen auf (JsonFormat, FormatCode, PythonCompletion, ShowSearch, ShowGoTo, Print, DetachActive, `SetSyntaxCheckAll`/`SetFoldingAll`, ZoomBy).

### `MainWindow`
- `Tabs`, `Active`, `ActiveView`, `Detached`
- statische globale Zustände: `BaseFamily/BaseStyle/BaseWeight/BaseSize`, `LastPrinterName`, `MargL/T/R/B`, `ClosedHistory`
- Tab/Datei: `NewView()`, `FindTab(view)`, `OpenInNewTab(path)`, `CloseTab(tab)`
- Suche/Dialoge: `DoSearch`, `ReplaceCurrent`, `ReplaceAll`, `ShowSearch(bool)`, `ShowGoTo()`, `GoToLine(int)`, `SaveSearchArgs`, `SetPrintMargins`
- `Commands` mit Bindings: Ctrl+T/O/S/Shift+S/P/F/F3/Shift+F3/Ctrl+H/Ctrl+G/F5/Zoom/Shift+W/Ctrl+Shift+T/Ctrl+Tab/Ctrl+Shift+Tab/Ctrl+Shift+D/Ctrl+Shift+W/`Ctrl+Shift+J` JSON-Format/`Ctrl+Shift+F` Code-Format (Python/C#)/`Ctrl+Space` Python-Completion/`F4` nächster Syntaxfehler/`Ctrl+Alt+K` KI-Panel.
- Menü „Sprachen“ enthält Markdown, Python, C#, YAML sowie `CodeFormatItem` (Python/C#) und `PythonCompletionItem` (nur wenn Python-LSP ready).
- Menü „KI“ enthält `AiPanelItem` (checkable), `AiAutoApplyItem` (checkable), `AiScanItem`, `AiSettingsItem`; Panel-Header nutzt `AiProviderBox`/`AiModelBox` (ComboBoxen), `AiStatus`, `AiLog`, `AiInput`, `AiSendButton`/`AiCancelButton`/`AiApplyButton`/`AiDiscardButton`/`AiClearButton`.

### KI
- `AiStore`: `Load()`, `Save()`, `State` (`Providers`, `ActiveProviderId`), `ActiveProvider`, `SetActiveProvider(id)`, `MergeScanned(provider, models)`, `NewId(prefix)`, `KindLabel/KindFromLabel`, `ReplaceState(list, activeId)`; `AiProvider.ApiKey` (de)kodiert DPAPI in `ApiKeyEnc`; `AiModel.DisplayName/Context/Vision/Tools` für UI
- `LocalAiClient`: `ScanAsync(provider, ct)`, `ChatAsync(provider, model, messages, tools, ct)` → `AiReply { Content, ToolCalls, ToolsAccepted, Error }`; `ExtractCodeBlock`, `ParseFallbackToolCalls`; Base-URL-Normalisierung; HTTP-Retry ohne `tools` wenn der Server Tools ablehnt
- `AiTools`: `Execute(string name, string argsJson, DocumentView view)` → String-Ergebnis; `IsEditTool(name)`; 9 Tools; Edit-Tools rufen `ReplaceAllText`/`ReplaceLines`/`InsertLine` (eine Undo-Group)
- `MainWindow.BuildAiSystemPrompt(model)`: nur Datei-/Sprach-/Zeileninfo (+ Auswahl), **kein Dokumenttext** — die KI liest bei Bedarf selbst über `get_document`/`search_document`; das spart Tokens. `TrimAiTranscript` hält max 12 Nicht-System-Nachrichten
- `AiSettingsDialog`: `AiStore.ReplaceState` bei OK, `SessionStore.Prefs.AiAutoApply`; `DialogWindow`-Style hat `SizeToContent=WidthAndHeight`

### `SessionStore`
- `AppPrefs` (siehe oben), `LoadPrefs()`, `SavePrefs()`, `HasSession`, `LoadSession()`, `SaveSession(SessionState)`, `ClearSession()`
- Session enthält Windows mit Tabs: Path, Text, Enc, Eol, Zoom, Wrap, ScrollX/Y, Caret, Dirty, Language.
- Session-/Prefs-Ordner: `%APPDATA%\NovaNote\`.

### `ThemeService`
- `SetMode("hell"|"dunkel"|"system")`, `Apply()`, `IsDark`, `ThemeChanged`, `AttachWindow(Window)`, `ApplyTitleBar(Window)`
- Ersetszt Theme-Dictionary durch `pack://application:,,,/Theme/Dark.xaml` oder `Light.xaml` (nur gültig, wenn das Theme im Entry-Assembly liegt).

### `DocHighlighting`
- `DefinitionFor(DocLanguage)`, `ThemeHighlightingColorizer`, `BrushForKey`, `BrushForColorName`, `InvalidateBrushes`; Markdown über `EnsureMarkdownRegistered()` aus `Markdown.xshd`, Python/C# über AvalonEdit-Standard `Python`/`C#`, YAML über Standard `YAML` oder `EnsureYamlCustomRegistered()` aus `Yaml.xshd`
- Theme-Brush-Keys: `TextBrush`, `SyntaxStringBrush`, `SyntaxNumberBrush`, `SyntaxKeywordBrush`, `SyntaxKeyBrush`, `SyntaxPunctBrush`, `SyntaxTagBrush`, `SyntaxAttrBrush`, `SyntaxCommentBrush`, `SyntaxPreprocBrush`, `SyntaxErrorBrush`

### `SyntaxChecker`
- `MaxErrors=100`, `MaxCheckedChars=2_000_000`
- `ShouldCheck(lang, path)`: `None` false; `Markdown` false; `Python` false; JS false bei `.ts/.mts/.cts`
- `Validate(lang, text)` → `List<SyntaxError>` mit Dokument-Offset (LF-normalisiert); C# nutzt Roslyn `Location.GetLineSpan()` (line/character 0-based), YAML nutzt `YamlException.Start.Line/Column` (long, 1-based?) → `MakeLine`

### `FoldingScanner`
- `MaxFoldedChars=1_000_000`, `MaxFoldings=500`
- `ShouldFold`: Json/Xml/Html/Css/Cpp/JavaScript/CSharp/Yaml; YAML wird aber in `DocumentView.RunFolding` über `IndentFoldingStrategy` behandelt und nicht über `CreateBrackets` gescannt
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
23. **Python-LSP/CLI und Dispatcher**: Python-LSP startet async; wenn der LSP nicht ready ist, nutzt `RunSyntaxCheck` den Python-CLI-Fallback. `ReplaceFormatted` und Python-Completion marshalieren UI-Arbeit über `Dispatcher.Invoke`, damit Formatierung/Completion auch ohne UI-SynchronizationContext (temp-Harness) keine Thread-Zugriffsfehler erzeugt. UI-Handler wie `MainWindow.FormatCode()` marshalieren MessageBox/UpdateStatus nach `await` ebenfalls über Dispatcher.
24. **Farbwerte sind AARRGGBB**: `Color`-Keys in `Theme/*.xaml` nutzen 8-stellig `#AARRGGBB`. `#005FB859` (Light) war Alpha `00` → Selection unsichtbar; richtig ist `#59005FB8` (Light) und `#594CC2FF` (Dark). Beim Ändern von Selection-/Popup-Farben Alpha zuerst prüfen.
25. **Popup-Stil**: Das Rechtsklick-Menü ist ein eigenes `Popup` mit `Border Background="{DynamicResource PopupBgBrush}"`; `Theme/Controls.xaml` hat implicit Styles für `Menu`/`MenuItem` (Hover = `PopupHoverBrush`). Kein eigener `ContextMenu`-Template-Style: ein solches Template öffnet den WPF-Popup nicht (Popup bleibt `IsOpen=false`) und würde zusätzlich die TextBox-Kontextmenüs in Dialogen zerstören. Farb-Alpha ist 8-stellig als `#AARRGGBB` zu lesen.
26. **ComboBox ist kein ContentControl**: Der eigene ComboBox-Style muss die Auswahl im geschlossenen Feld über `Content={Binding SelectedItem, RelativeSource TemplatedParent}` + `ContentTemplate={TemplateBinding ItemTemplate}` anzeigen; ein nackter `ContentPresenter` bleibt leer. Der `CbItem`-Accent-Hintergrund darf nur im Popup erscheinen.
27. **KI-Editorzugriff**: Nur die dokumentierte `DocumentView`-KI-API nutzen (`ReplaceAllText`, `ReplaceLines`, `InsertLine`, `GetLines`, `FindAll`, `SelectLines`, `GoToLineQuiet`); `ReplaceText` verwirft den Undo-Stack und ist für KI-Aktionen ungeeignet.
28. **KI ist optional**: Ohne Server laufen keine Anfragen (`AiDetectAsync` zeigt nur „Kein Server erreichbar“). `AiStore.Save()` schreibt `%APPDATA%\NovaNote\ki.json` (DPAPI-Key, nur aktueller Benutzer); Harness-Tests dürfen echte `ki.json`/`einstellungen.json` nicht überschreiben, ohne sie vorher zu sichern.
29. **`llama-server` meldet Tools nicht**: `/v1/models` liefert oft kein Tool-Support-Flag; `MainWindow.AiRunAsync` setzt `model.SupportsTools = true` und speichert, nachdem ein Tool-Aufruf akzeptiert wurde. Kontext steht dort als `contextWindow`/`context_length`, Vision als `multimodal`/`modalities`.
30. **Update-Setup-Lock**: `UpdateService.DownloadSetupAsync` muss alle Streams schließen, **bevor** `LaunchSetup` läuft. Ein `using var file` im selben Block wie `Process.Start` hält die temp-Setup-Datei offen und der Start schlägt fehl („von einem anderen Prozess verwendet”).

## Kurzregeln für KI
- Nicht den ganzen Code scannen; diese README als Landkarte nutzen.
- Änderungen an Editor/Syntax/Folding primär in `DocumentView.xaml.cs`, Scanner in `SyntaxCheck.cs`/`Folding.cs`, UI in `MainWindow.xaml(.cs)`, Farben in `Theme/*.xaml`, Installer in `installer.ps1`/`Program.cs`.
- Nach Änderungen: `dotnet build` + optional temp-Harness + Publish-Smoke.
- Deutsch im UI, deutsche Fehlermeldungen/Statustexte beibehalten.
