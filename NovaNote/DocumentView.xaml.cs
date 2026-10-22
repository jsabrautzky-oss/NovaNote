using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace NovaNote
{
    public enum DocAction
    {
        Undo,
        Redo,
        Cut,
        Copy,
        Paste,
        Delete,
        SelectAll,
        FormatJson,
        FormatCode,
        PythonCompletion,
        MarkdownRefresh,
        NextSyntaxError,
        SyntaxCheck,
        Folding,
        Find,
        FindNext,
        Replace,
        GoToLine,
        InsertDateTime,
        Save,
        SaveAs,
        Print,
        WordWrap,
        ZoomIn,
        ZoomOut,
        ZoomReset,
        Detach
    }

    public partial class DocumentView : UserControl, IDisposable
    {
        public string Path_ { get; private set; } = null;
        public bool IsDirty { get; private set; } = false;
        public NovaNoteEncoding Enc { get; private set; } = NovaNoteEncoding.Utf8;
        public LineEnding Eol { get; private set; } = LineEnding.CrLf;
        public int Zoom { get; private set; } = 100;
        public const int MinZoom = 10, MaxZoom = 1000;
        public bool WordWrap { get; private set; } = true;
        public DocLanguage Lang { get; private set; } = DocLanguage.None;
        public string StatusLanguage => DocLanguages.DisplayName(Lang);
        public string TabTitle
        {
            get
            {
                string name = Path_ == null ? "Unbenannt" : System.IO.Path.GetFileName(Path_);
                return name + (IsDirty ? "*" : "");
            }
        }

        bool _loading;
        double _baseFont = 15;
        FontFamily _baseFamily = new FontFamily("Consolas");
        FontStyle _baseStyle = FontStyles.Normal;
        FontWeight _baseWeight = FontWeights.Normal;

        ThemeHighlightingColorizer _colorizer;

        readonly SyntaxErrorRenderer _syntaxRenderer;
        readonly DispatcherTimer _syntaxTimer;
        CancellationTokenSource _syntaxCts;
        List<SyntaxError> _syntaxErrors = new List<SyntaxError>();
        string _syntaxStatus = "";
        bool _syntaxEnabled = SessionStore.Prefs.SyntaxCheck;

        readonly FoldingManager _foldingManager;
        readonly XmlFoldingStrategy _xmlFolding = new XmlFoldingStrategy();
        readonly IndentFoldingStrategy _indentFolding = new IndentFoldingStrategy();
        readonly DispatcherTimer _foldingTimer;
        CancellationTokenSource _foldingCts;
        bool _foldingEnabled = SessionStore.Prefs.Folding;

        PythonLanguageServerClient _pythonLsp;
        string _pythonUri;
        string _pythonRoot;
        string _pythonUnsavedUri;
        bool _pythonLspFailed;
        bool _pythonPullFailed;
        CompletionWindow _completionWindow;
        bool _disposed;
        int _menuSelStart, _menuSelLen;
        Window _menuWindow;
        MouseButtonEventHandler _menuOutsideHandler;

        public event EventHandler StateChanged;
        public event EventHandler DirtyChanged;
        public event Action<DocumentView, DocAction, bool> ContextAction;

        public DocumentView()
        {
            InitializeComponent();
            Editor.Options.EnableTextDragDrop = false;
            Editor.TextArea.SelectionForeground = null;
            Editor.TextArea.SelectionBorder = null;
            Editor.TextArea.SelectionCornerRadius = 2;
            _syntaxRenderer = new SyntaxErrorRenderer(() => AppBrush("SyntaxErrorBrush"));
            Editor.TextArea.TextView.BackgroundRenderers.Add(_syntaxRenderer);
            _syntaxTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _syntaxTimer.Tick += (s, e) =>
            {
                _syntaxTimer.Stop();
                SendPythonDidChange();
                RunSyntaxCheck();
                UpdateMarkdownPreview();
            };
            _foldingManager = FoldingManager.Install(Editor.TextArea);
            _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _foldingTimer.Tick += (s, e) => { _foldingTimer.Stop(); RunFolding(); };
            ApplyTheme();
            ApplyPreferences();
            UpdateHighlighting();
            DataObject.AddPastingHandler(Editor.TextArea, OnPaste);
            Editor.Document.TextChanged += Document_TextChanged;
            Editor.TextArea.TextEntered += Editor_TextEntered;
            Editor.TextArea.SelectionChanged += (s, e) => StateChanged?.Invoke(this, EventArgs.Empty);
            Editor.TextArea.Caret.PositionChanged += (s, e) => StateChanged?.Invoke(this, EventArgs.Empty);
            StateChanged += (s, e) => UpdateContextMenuState();
        }

        static Brush AppBrush(string key)
        {
            var res = Application.Current?.Resources;
            if (res != null && res.Contains(key)) return res[key] as Brush;
            return null;
        }

        void ApplyTheme()
        {
            DocHighlighting.InvalidateBrushes();
            Editor.Foreground = AppBrush("TextBrush") ?? Brushes.Black;
            Editor.LineNumbersForeground = AppBrush("SubtleTextBrush") ?? Brushes.Gray;
            var sel = AppBrush("SelectionBrush");
            if (sel != null) Editor.TextArea.SelectionBrush = sel;
            var caret = AppBrush("TextBrush");
            if (caret != null) Editor.TextArea.Caret.CaretBrush = caret;
            var marker = FrozenBrush(AppBrush("SubtleTextBrush"), Colors.Gray);
            var markerBg = FrozenBrush(AppBrush("EditorBgBrush"), Colors.White);
            foreach (var margin in Editor.TextArea.LeftMargins)
            {
                if (margin is FoldingMargin fm)
                {
                    FoldingMargin.SetFoldingMarkerBrush(fm, marker);
                    FoldingMargin.SetFoldingMarkerBackgroundBrush(fm, markerBg);
                }
            }
        }

        static Brush FrozenBrush(Brush source, Color fallback)
        {
            var color = (source as SolidColorBrush)?.Color ?? fallback;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public void ApplyPreferences()
        {
            Editor.ShowLineNumbers = SessionStore.Prefs.ShowLineNumbers;
            Editor.Options.ShowSpaces = SessionStore.Prefs.ShowWhitespace;
            Editor.Options.ShowTabs = SessionStore.Prefs.ShowWhitespace;
            Editor.Options.ShowEndOfLine = SessionStore.Prefs.ShowWhitespace;
            Editor.TextArea.TextView.Redraw();
        }

        // ---------- Plain-Text-Mapping ----------

        static string Norm(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

        TextDocument Doc => Editor.Document;

        public string PlainText() => Doc.Text;

        public string CurrentText => PlainText();

        public int LineCount => Doc.LineCount;

        int DocLength => Doc.TextLength;

        // ---------- Cursor/Selektion ----------

        int CaretIndex
        {
            get => Editor.CaretOffset;
            set
            {
                Editor.CaretOffset = Math.Clamp(value, 0, DocLength);
                Editor.TextArea.Caret.BringCaretToView();
            }
        }

        int SelStart => Editor.SelectionStart;
        int SelLength => Editor.SelectionLength;

        void Select(int index, int length)
        {
            int max = DocLength;
            int start = Math.Clamp(index, 0, max);
            int len = Math.Clamp(length, 0, max - start);
            Editor.Select(start, len);
        }

        // ---------- Format/Schrift ----------

        public void SetFont(FontFamily family, FontStyle style, FontWeight weight, double size)
        {
            _baseFamily = family; _baseStyle = style; _baseWeight = weight; _baseFont = size;
            Editor.FontFamily = family;
            Editor.FontStyle = style;
            Editor.FontWeight = weight;
            SetZoom(Zoom);
        }

        public void SetBaseFont(FontFamily family, FontStyle style, FontWeight weight, double size)
        {
            _baseFamily = family; _baseStyle = style; _baseWeight = weight; _baseFont = size;
        }

        public void SetZoom(int value)
        {
            Zoom = Math.Clamp(value, MinZoom, MaxZoom);
            Editor.FontSize = _baseFont * Zoom / 100.0;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetWordWrap(bool on)
        {
            WordWrap = on;
            Editor.WordWrap = on;
            Editor.HorizontalScrollBarVisibility = on ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }

        public void SetEncoding(NovaNoteEncoding enc)
        {
            Enc = enc;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetEol(LineEnding eol)
        {
            Eol = eol;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------- Status ----------

        public (string caret, string selection) GetStatus()
        {
            int ci = Editor.CaretOffset;
            var line = Doc.GetLineByOffset(ci);
            int col = ci - line.Offset + 1;
            int selLen = SelLength;
            return ($"Zeile {line.LineNumber}, Spalte {col}",
                    selLen > 0 ? $"Auswahl {selLen} Zeichen" : "");
        }

        public string StatusCaret => GetStatus().caret;
        public string StatusSelection => GetStatus().selection;

        // ---------- Dirty/Events ----------

        void SetDirty(bool dirty)
        {
            if (IsDirty == dirty) return;
            IsDirty = dirty;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }

        void Document_TextChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            SetDirty(true);
            StateChanged?.Invoke(this, EventArgs.Empty);
            ScheduleSyntaxCheck();
            ScheduleFolding();
            ScheduleMarkdownPreview();
        }

        void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                SetZoom(Zoom + (e.Delta > 0 ? 10 : -10));
                e.Handled = true;
            }
        }

        void SetText(string text)
        {
            _loading = true;
            Doc.BeginUpdate();
            try
            {
                Doc.Replace(0, DocLength, Norm(text ?? ""));
            }
            finally
            {
                Doc.EndUpdate();
                _loading = false;
            }
        }

        void ReplaceRange(int start, int length, string text)
        {
            Doc.BeginUpdate();
            try
            {
                Doc.Replace(Math.Clamp(start, 0, DocLength), Math.Clamp(length, 0, DocLength - start), Norm(text ?? ""));
            }
            finally
            {
                Doc.EndUpdate();
            }
        }

        public bool CanUndo => Doc.UndoStack.CanUndo;

        public bool CanRedo => Doc.UndoStack.CanRedo;

        public void DoUndo()
        {
            if (Doc.UndoStack.CanUndo) Doc.UndoStack.Undo();
            Editor.Focus();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DoRedo()
        {
            if (Doc.UndoStack.CanRedo) Doc.UndoStack.Redo();
            Editor.Focus();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearUndoStack() => Doc.UndoStack.ClearAll();

        void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                Editor.TextArea.PerformTextInput(Norm((string)e.DataObject.GetData(DataFormats.Text)));
                e.CancelCommand();
            }
        }

        public void DoCut() => Editor.Cut();
        public void DoCopy() => Editor.Copy();

        public void DoPaste()
        {
            if (Clipboard.ContainsText())
                Editor.TextArea.PerformTextInput(Norm(Clipboard.GetText()));
            Editor.Focus();
        }

        public void DoDelete()
        {
            Editor.Delete();
            Editor.Focus();
        }

        public void DoSelectAll()
        {
            Editor.SelectAll();
            Editor.Focus();
        }

        public void FocusEditor() => Editor.Focus();

        bool IsMarkdown => Lang == DocLanguage.Markdown || DocLanguages.FromExtension(Path_) == DocLanguage.Markdown;

        // ---------- Rechtsklick-Menü ----------

        void Editor_PreviewRightDown(object sender, MouseButtonEventArgs e)
        {
            _menuSelStart = Editor.SelectionStart;
            _menuSelLen = Editor.SelectionLength;
            UpdateContextMenuState();
            EditorMenuPopup.PlacementTarget = Editor.TextArea;
            EditorMenuPopup.Placement = PlacementMode.MousePoint;
            EditorMenuPopup.IsOpen = true;
            EditorMenuPopup.Focus();
            e.Handled = true;
        }

        void Menu_Opened(object sender, EventArgs e)
        {
            _menuOutsideHandler ??= Menu_OutsideMouseDown;
            _menuWindow = Window.GetWindow(this);
            _menuWindow?.AddHandler(Mouse.PreviewMouseDownEvent, _menuOutsideHandler, true);
        }

        void Menu_Closed(object sender, EventArgs e)
        {
            _menuWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, _menuOutsideHandler);
            _menuWindow = null;
        }

        void Menu_OutsideMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!EditorMenuPopup.IsOpen) return;
            if (IsInsidePopup(e.OriginalSource as DependencyObject)) return;
            EditorMenuPopup.IsOpen = false;
        }

        bool IsInsidePopup(DependencyObject source)
        {
            var child = EditorMenuPopup.Child;
            if (child == null || source == null) return false;
            for (DependencyObject cur = source; cur != null;
                 cur = cur is Visual ? VisualTreeHelper.GetParent(cur) : LogicalTreeHelper.GetParent(cur))
                if (ReferenceEquals(cur, child)) return true;
            return false;
        }

        void RestoreMenuSelection()
        {
            if (_menuSelLen > 0 && Editor.SelectionLength == 0)
            {
                Editor.SelectionStart = _menuSelStart;
                Editor.SelectionLength = _menuSelLen;
            }
        }

        void Ctx_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as MenuItem)?.Tag as string;
            DocAction action;
            if (string.IsNullOrEmpty(tag) || !Enum.TryParse(tag, out action)) return;

            if (action == DocAction.MarkdownRefresh)
            {
                UpdateMarkdownPreview();
                EditorMenuPopup.IsOpen = false;
                Editor.Focus();
                return;
            }

            if (action == DocAction.Cut || action == DocAction.Copy || action == DocAction.Delete)
                RestoreMenuSelection();

            bool value = action == DocAction.SyntaxCheck ? !_syntaxEnabled
                       : action == DocAction.Folding ? !_foldingEnabled
                       : action == DocAction.WordWrap ? !WordWrap
                       : false;
            ContextAction?.Invoke(this, action, value);
            EditorMenuPopup.IsOpen = false;
            Editor.Focus();
        }

        void UpdateContextMenuState()
        {
            if (CtxCopyItem == null) return;
            bool selection = Editor.SelectionLength > 0 || _menuSelLen > 0;
            bool clipboardText = false;
            try { clipboardText = Clipboard.ContainsText(); } catch { }

            bool undo = Show(CtxUndoItem, CanUndo);
            bool redo = Show(CtxRedoItem, CanRedo);
            bool cut = Show(CtxCutItem, selection);
            bool copy = Show(CtxCopyItem, selection);
            bool delete = Show(CtxDeleteItem, selection);
            bool paste = Show(CtxPasteItem, clipboardText);
            bool selectAll = Show(CtxSelectAllItem, Doc.TextLength > 0);

            bool json = Show(CtxFormatJsonItem, Lang == DocLanguage.Json);
            bool code = Show(CtxFormatCodeItem, FormatSupported);
            bool python = Show(CtxPythonCompletionItem, Lang == DocLanguage.Python && PythonLspReady);
            bool previewOpen = IsMarkdown && PreviewBorder != null && PreviewBorder.Visibility == Visibility.Visible;
            bool markdown = Show(CtxMarkdownPreviewItem, previewOpen);

            Show(CtxNextErrorItem, HasSyntaxErrors);
            CtxSyntaxCheckItem.Visibility = Visibility.Visible;
            CtxSyntaxCheckItem.IsChecked = _syntaxEnabled;
            CtxFoldingItem.Visibility = Visibility.Visible;
            CtxFoldingItem.IsChecked = _foldingEnabled;

            Show(CtxSaveItem, IsDirty);
            CtxWrapItem.Visibility = Visibility.Visible;
            CtxWrapItem.IsChecked = WordWrap;
            Show(CtxZoomResetItem, Zoom != 100);
            Show(CtxDetachItem, SessionStore.Prefs.MultiTab);

            bool gEdit = undo || redo;
            bool gClipboard = cut || copy || delete || paste || selectAll;
            bool gFormat = json || code || python || markdown;

            CtxSepEdit.Visibility = Sep(gEdit, gClipboard);
            CtxSepClipboard.Visibility = Sep(gClipboard, gFormat);
            CtxSepFormat.Visibility = Sep(gFormat, true);
            CtxSepSyntax.Visibility = Visibility.Visible;
            CtxSepSearch.Visibility = Visibility.Visible;
            CtxSepSave.Visibility = Visibility.Visible;
        }

        static bool Show(UIElement item, bool on)
        {
            item.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            return on;
        }

        static Visibility Sep(bool before, bool after)
            => before && after ? Visibility.Visible : Visibility.Collapsed;

        // ---------- Sprache/Highlight ----------

        public void SetLanguage(DocLanguage lang)
        {
            Lang = lang;
            StateChanged?.Invoke(this, EventArgs.Empty);
            UpdateHighlighting();
            UpdateMarkdownVisibility();
            ScheduleSyntaxCheck();
            ScheduleFolding();
            ScheduleMarkdownPreview();
        }

        public void DetectLanguage()
        {
            UpdateMarkdownVisibility();
            if (!SessionStore.Prefs.LanguageAuto)
            {
                ScheduleSyntaxCheck();
                ScheduleFolding();
                ScheduleMarkdownPreview();
                return;
            }
            var lang = DocLanguages.FromExtension(Path_);
            if (lang == DocLanguage.None) lang = DocLanguages.DetectContent(PlainText());
            Lang = lang;
            StateChanged?.Invoke(this, EventArgs.Empty);
            UpdateHighlighting();
            UpdateMarkdownVisibility();
            ScheduleSyntaxCheck();
            ScheduleFolding();
            ScheduleMarkdownPreview();
        }

        public void ReapplyHighlight()
        {
            ApplyTheme();
            UpdateHighlighting();
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }

        void ScheduleMarkdownPreview()
        {
            if (MarkdownViewer == null) return;
            if (Lang != DocLanguage.Markdown && DocLanguages.FromExtension(Path_) != DocLanguage.Markdown) return;
            if (PreviewBorder == null || PreviewBorder.Visibility == Visibility.Collapsed) return;
            _syntaxTimer.Stop();
            _syntaxTimer.Start();
        }

        void UpdateMarkdownVisibility()
        {
            if (MarkdownViewer == null) return;
            bool visible = Lang == DocLanguage.Markdown || DocLanguages.FromExtension(Path_) == DocLanguage.Markdown;
            MarkdownViewer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            PreviewBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            PreviewSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            LayoutGrid.ColumnDefinitions[1].Width = visible ? GridLength.Auto : new GridLength(0);
            LayoutGrid.ColumnDefinitions[2].Width = visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            LayoutGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            if (visible) UpdateMarkdownPreview();
            else MarkdownViewer.Markdown = "";
        }

        void UpdateMarkdownPreview()
        {
            if (MarkdownViewer == null) return;
            bool markdown = Lang == DocLanguage.Markdown || DocLanguages.FromExtension(Path_) == DocLanguage.Markdown;
            if (!markdown || PreviewBorder == null || PreviewBorder.Visibility == Visibility.Collapsed) return;
            try
            {
                MarkdownViewer.Markdown = Editor.Text;
            }
            catch
            {
            }
        }

        // ---------- Python LSP / IntelliSense ----------

        public bool PythonLspReady => _pythonLsp?.IsReady ?? false;
        public bool FormatSupported => Lang == DocLanguage.Python || Lang == DocLanguage.CSharp;

        async void Editor_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_loading || _disposed) return;
            if (Lang == DocLanguage.Python && _pythonLsp != null && _pythonLsp.IsReady &&
                _completionWindow == null && e.Text == ".")
                await ShowPythonCompletionAsync();
        }

        async void UpdatePythonLsp()
        {
            bool needed = Lang == DocLanguage.Python || DocLanguages.FromExtension(Path_) == DocLanguage.Python;
            if (!needed)
            {
                DisposePythonLsp();
                _pythonUri = null;
                _pythonRoot = null;
                _pythonLspFailed = false;
                return;
            }

            string uri = GetPythonDocumentUri();
            string root = GetPythonRootPath();
            if (_pythonLsp != null && _pythonUri == uri && _pythonRoot == root) return;
            if (_pythonLspFailed && _pythonUri == uri && _pythonRoot == root) return;

            DisposePythonLsp();
            _pythonUri = uri;
            _pythonRoot = root;
            _pythonLspFailed = false;
            _pythonPullFailed = false;

            var client = new PythonLanguageServerClient(uri, GetPythonRootUri(), root);
            client.DiagnosticsReady += OnPythonDiagnostics;
            _pythonLsp = client;
            await EnsurePythonLspAsync(client);
        }

        async Task EnsurePythonLspAsync(PythonLanguageServerClient client)
        {
            bool ok;
            try
            {
                ok = await client.StartAsync(Doc.Text);
            }
            catch
            {
                ok = false;
            }
            if (!ok)
            {
                if (ReferenceEquals(_pythonLsp, client))
                {
                    _pythonLspFailed = true;
                    DisposePythonLsp();
                }
            }
            Dispatcher.Invoke(new Action(() => StateChanged?.Invoke(this, EventArgs.Empty)));
            if (!ok) Dispatcher.Invoke(new Action(() => ScheduleSyntaxCheck()));
        }

        void DisposePythonLsp()
        {
            _pythonPullFailed = false;
            if (_pythonLsp == null) return;
            _pythonLsp.DiagnosticsReady -= OnPythonDiagnostics;
            _pythonLsp.Dispose();
            _pythonLsp = null;
        }

        void SendPythonDidChange()
        {
            if (_pythonLsp != null && (Lang == DocLanguage.Python || DocLanguages.FromExtension(Path_) == DocLanguage.Python))
                _pythonLsp.DidChange(Doc.Text);
        }

        async void PullPythonDiagnostics()
        {
            if (_disposed || _pythonLsp == null || !_pythonLsp.IsReady || _pythonPullFailed) return;
            try
            {
                await _pythonLsp.RequestDiagnosticsAsync(_pythonLsp.DocumentVersion);
            }
            catch
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    _pythonPullFailed = true;
                    if (_syntaxEnabled) ScheduleSyntaxCheck();
                }));
            }
        }

        void OnPythonDiagnostics(List<LspDiagnostic> diagnostics)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed || Lang != DocLanguage.Python || !_syntaxEnabled) return;
                _pythonPullFailed = false;
                string text = Doc.Text;
                var errors = new List<SyntaxError>();
                var all = diagnostics ?? new List<LspDiagnostic>();
                foreach (var d in all)
                {
                    if (d.Severity == 1)
                    {
                        errors.Add(MakePythonError(text, d));
                        if (errors.Count >= SyntaxChecker.MaxErrors) break;
                    }
                }
                string status = errors.Count > 0
                    ? BuildSyntaxStatus(errors)
                    : all.Count > 0 ? $"Python: {all.Count} Diagnose(n)" : "Python: OK";
                SetSyntaxResult(errors, status);
            }));
        }

        SyntaxError MakePythonError(string text, LspDiagnostic d)
        {
            int start = OffsetFromLspPosition(text, d.StartLine, d.StartChar);
            int end = OffsetFromLspPosition(text, d.EndLine, d.EndChar);
            if (end <= start) end = Math.Min(text.Length, start + 1);
            int len = Math.Min(Math.Max(end - start, 1), 60);
            return new SyntaxError { Offset = start, Length = len, Message = d.Message };
        }

        int OffsetFromLspPosition(string text, int line0, int char0)
        {
            int offset = 0;
            int currentLine = 0;
            while (currentLine < line0 && offset < text.Length)
            {
                int nl = text.IndexOf('\n', offset);
                if (nl < 0) return text.Length;
                offset = nl + 1;
                currentLine++;
            }
            if (char0 < 0) char0 = 0;
            return Math.Clamp(offset + char0, 0, text.Length);
        }

        (int Line, int Character) LspPosition(int offset)
        {
            int len = DocLength;
            int ci = Math.Clamp(offset, 0, len);
            var line = Doc.GetLineByOffset(ci);
            return (line.LineNumber - 1, ci - line.Offset);
        }

        public async Task ShowPythonCompletionAsync()
        {
            if (_disposed || _pythonLsp == null || !_pythonLsp.IsReady) return;
            if (_completionWindow != null) return;

            var pos = LspPosition(Editor.CaretOffset);
            var window = new CompletionWindow(Editor.TextArea);
            _completionWindow = window;
            window.Closed += (s, e) => { if (ReferenceEquals(_completionWindow, window)) _completionWindow = null; };
            window.Show();

            try
            {
                var result = await _pythonLsp.RequestCompletion(pos.Line, pos.Character);
                var items = BuildCompletionItems(result, Editor.TextArea.Caret.Offset);
                Dispatcher.Invoke(new Action(() =>
                {
                    if (!window.IsVisible) return;
                    foreach (var item in items)
                    {
                        window.CompletionList?.CompletionData?.Add(item);
                    }
                    if (items.Count == 0) window.Close();
                }));
            }
            catch
            {
                Dispatcher.Invoke(new Action(() => window.Close()));
            }
        }

        public async Task<string> FormatCodeAsync()
        {
            switch (Lang)
            {
                case DocLanguage.Python:
                    return await FormatPythonAsync();
                case DocLanguage.CSharp:
                    return await FormatCSharpAsync();
                default:
                    return null;
            }
        }

        async Task<string> FormatPythonAsync()
        {
            string text = Doc.Text;
            int caret = Editor.CaretOffset;
            var result = await PythonFormatter.FormatAsync(text);
            if (result.Error != null) return result.Error;
            string formatted = Norm(result.Text ?? "");
            if (formatted == text) return null;
            ReplaceFormatted(formatted, caret);
            return null;
        }

        Task<string> FormatCSharpAsync()
        {
            string text = Doc.Text;
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();
            using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
            var formatted = Formatter.Format(root, workspace, workspace.Options, CancellationToken.None);
            string result = Norm(formatted.ToFullString());
            if (result == text) return Task.FromResult<string>(null);
            ReplaceFormatted(result, Editor.CaretOffset);
            return Task.FromResult<string>(null);
        }

        void ReplaceFormatted(string text, int caretOffset)
        {
            Dispatcher.Invoke(new Action(() => ReplaceFormattedCore(text, caretOffset)));
        }

        void ReplaceFormattedCore(string text, int caretOffset)
        {
            int max = DocLength;
            int ci = Math.Clamp(caretOffset, 0, max);
            var oldLine = Doc.GetLineByOffset(ci);
            int caretLine = oldLine.LineNumber;
            int caretCol = ci - oldLine.Offset + 1;

            _loading = true;
            Doc.BeginUpdate();
            try
            {
                Doc.Replace(0, DocLength, Norm(text ?? ""));
            }
            finally
            {
                Doc.EndUpdate();
                _loading = false;
            }

            Doc.UndoStack.ClearRedoStack();

            int targetLine = Math.Clamp(caretLine, 1, Doc.LineCount);
            var newLine = Doc.GetLineByNumber(targetLine);
            int offset = newLine.Offset + (caretCol - 1);
            if (offset > newLine.EndOffset) offset = newLine.EndOffset;
            CaretIndex = offset;

            SetDirty(true);
            StateChanged?.Invoke(this, EventArgs.Empty);
            UpdateHighlighting();
            ScheduleSyntaxCheck();
            ScheduleFolding();
            ScheduleMarkdownPreview();
            SendPythonDidChange();
        }

        static string ToFileUri(string path)
        {
            string full = System.IO.Path.GetFullPath(path);
            if (System.IO.Directory.Exists(full)) full += "/";
            return new Uri(full, UriKind.Absolute).ToString();
        }

        string GetPythonDocumentUri()
        {
            if (Path_ == null)
            {
                _pythonUnsavedUri ??= ToFileUri(System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"novanote-{Guid.NewGuid():N}.py"));
                return _pythonUnsavedUri;
            }
            return ToFileUri(System.IO.Path.GetFullPath(Path_));
        }

        string GetPythonRootPath()
        {
            if (Path_ == null) return System.IO.Path.GetTempPath();
            string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path_));
            return string.IsNullOrEmpty(dir) ? System.IO.Path.GetTempPath() : dir;
        }

        string GetPythonRootUri()
        {
            return ToFileUri(GetPythonRootPath());
        }

        static List<ICompletionData> BuildCompletionItems(JsonElement result, int startOffset)
        {
            var completions = new List<ICompletionData>();
            JsonElement array;
            if (result.ValueKind == JsonValueKind.Array)
            {
                array = result;
            }
            else if (result.ValueKind == JsonValueKind.Object &&
                     result.TryGetProperty("items", out var itemsElement) &&
                     itemsElement.ValueKind == JsonValueKind.Array)
            {
                array = itemsElement;
            }
            else return completions;

            foreach (var item in array.EnumerateArray())
            {
                string label = JsonString(item, "label");
                if (string.IsNullOrEmpty(label)) continue;

                string insert = JsonString(item, "insertText");
                if (string.IsNullOrEmpty(insert) && item.TryGetProperty("textEdit", out var edit))
                    insert = JsonString(edit, "insertText");
                if (string.IsNullOrEmpty(insert)) insert = label;

                string detail = JsonString(item, "detail");
                string documentation = JsonString(item, "documentation");
                string description = detail;
                if (!string.IsNullOrEmpty(detail) && !string.IsNullOrEmpty(documentation))
                    description = detail + " — " + documentation;
                else if (string.IsNullOrEmpty(description))
                    description = documentation;

                var completion = new LspCompletion(label, insert, description);
                completions.Add(completion);
                if (completions.Count >= SyntaxChecker.MaxErrors) break;
            }
            return completions;
        }

        static string JsonString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString();
                if (value.ValueKind == JsonValueKind.Object &&
                    value.TryGetProperty("value", out var inner) &&
                    inner.ValueKind == JsonValueKind.String)
                    return inner.GetString();
            }
            return null;
        }

        // ---------- Syntax-Check ----------

        public IReadOnlyList<SyntaxError> SyntaxErrors => _syntaxErrors;
        public string SyntaxStatus => _syntaxStatus;
        public bool HasSyntaxErrors => _syntaxErrors.Count > 0;
        public bool SyntaxCheckEnabled => _syntaxEnabled;

        public void SetSyntaxCheck(bool on)
        {
            _syntaxEnabled = on;
            if (on) ScheduleSyntaxCheck();
            else SetSyntaxResult(null, "Syntax-Check aus");
            UpdateContextMenuState();
        }

        void ScheduleSyntaxCheck()
        {
            bool pythonLsp = Lang == DocLanguage.Python && _pythonLsp != null;
            if (!_syntaxEnabled && !pythonLsp)
            {
                _syntaxTimer.Stop();
                if (_syntaxErrors.Count > 0 || !string.IsNullOrEmpty(_syntaxStatus))
                    SetSyntaxResult(null, "Syntax-Check aus");
                return;
            }
            _syntaxTimer.Stop();
            _syntaxTimer.Start();
        }

        void RunSyntaxCheck()
        {
            if (!_syntaxEnabled)
            {
                SetSyntaxResult(null, "Syntax-Check aus");
                return;
            }
            if (Lang == DocLanguage.None)
            {
                SetSyntaxResult(null, "");
                return;
            }
            if (Lang == DocLanguage.Python)
            {
                if (_pythonLsp != null && _pythonLsp.IsReady && !_pythonPullFailed)
                {
                    SetSyntaxResult(null, "Python-LSP: Diagnose läuft");
                    PullPythonDiagnostics();
                    return;
                }

                string pythonText = Doc.Text;
                _syntaxCts?.Cancel();
                var pythonCts = new CancellationTokenSource();
                _syntaxCts = pythonCts;
                Task.Run(async () =>
                {
                    if (pythonCts.IsCancellationRequested) return;
                    var result = await PythonSyntaxChecker.ValidateAsync(pythonText, pythonCts.Token);
                    if (pythonCts.IsCancellationRequested || result == null) return;
#pragma warning disable CS4014
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!ReferenceEquals(_syntaxCts, pythonCts)) return;
                        SetSyntaxResult(result.Errors, result.Status ?? BuildSyntaxStatus(result.Errors));
                    }));
#pragma warning restore CS4014
                }, pythonCts.Token);
                return;
            }
            if (!SyntaxChecker.ShouldCheck(Lang, Path_))
            {
                if (Lang == DocLanguage.Markdown) SetSyntaxResult(null, "Markdown-Preview");
                else SetSyntaxResult(null, "kein Syntax-Check für TypeScript");
                return;
            }
            string text = Doc.Text;
            if (text.Length > SyntaxChecker.MaxCheckedChars)
            {
                SetSyntaxResult(null, "Dokument zu groß – nicht geprüft");
                return;
            }

            _syntaxCts?.Cancel();
            var cts = new CancellationTokenSource();
            _syntaxCts = cts;
            var lang = Lang;
            Task.Run(() =>
            {
                if (cts.IsCancellationRequested) return;
                List<SyntaxError> errors;
                try
                {
                    errors = SyntaxChecker.Validate(lang, text);
                }
                catch
                {
                    return;
                }
                if (cts.IsCancellationRequested) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ReferenceEquals(_syntaxCts, cts)) return;
                    SetSyntaxResult(errors, BuildSyntaxStatus(errors));
                }));
            }, cts.Token);
        }

        void SetSyntaxResult(List<SyntaxError> errors, string status)
        {
            _syntaxErrors = errors ?? new List<SyntaxError>();
            _syntaxRenderer.SetErrors(_syntaxErrors);
            _syntaxStatus = status;
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        string BuildSyntaxStatus(List<SyntaxError> errors)
        {
            if (errors == null || errors.Count == 0) return "Syntax: OK";
            int line = LineOfOffset(errors[0].Offset);
            string extra = errors.Count > 1 ? $" (+{errors.Count - 1})" : "";
            return $"Syntax-Fehler Zeile {line}: {errors[0].Message}{extra}";
        }

        int LineOfOffset(int offset)
        {
            int len = DocLength;
            if (len == 0) return 1;
            return Doc.GetLineByOffset(Math.Clamp(offset, 0, len)).LineNumber;
        }

        public bool GoToNextSyntaxError()
        {
            if (_syntaxErrors.Count == 0) return false;
            int caret = Editor.CaretOffset;
            int idx = -1;
            for (int i = 0; i < _syntaxErrors.Count; i++)
            {
                if (_syntaxErrors[i].Offset > caret) { idx = i; break; }
            }
            if (idx < 0) idx = 0;
            var err = _syntaxErrors[idx];
            int len = DocLength;
            int start = Math.Clamp(err.Offset, 0, len);
            int end = Math.Clamp(err.Offset + err.Length, start, len);
            Select(start, end - start);
            ScrollToIndex(start);
            Editor.Focus();
            _syntaxStatus = $"Syntax-Fehler Zeile {LineOfOffset(start)}: {err.Message}";
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        // ---------- Code-Folding ----------

        public bool FoldingEnabled => _foldingEnabled;

        public void SetFolding(bool on)
        {
            _foldingEnabled = on;
            if (on) ScheduleFolding();
            else ClearFoldings();
            UpdateContextMenuState();
        }

        void ScheduleFolding()
        {
            if (!_foldingEnabled)
            {
                ClearFoldings();
                return;
            }
            _foldingTimer.Stop();
            _foldingTimer.Start();
        }

        void ClearFoldings()
        {
            _foldingCts?.Cancel();
            _foldingManager.UpdateFoldings(System.Linq.Enumerable.Empty<NewFolding>(), -1);
        }

        void RunFolding()
        {
            if (!_foldingEnabled || !FoldingScanner.ShouldFold(Lang))
            {
                ClearFoldings();
                return;
            }
            string text = Doc.Text;
            if (text.Length > FoldingScanner.MaxFoldedChars)
            {
                ClearFoldings();
                return;
            }
            if (Lang == DocLanguage.Xml || Lang == DocLanguage.Html)
            {
                _xmlFolding.UpdateFoldings(_foldingManager, Doc);
                return;
            }
            if (Lang == DocLanguage.Yaml)
            {
                _indentFolding.UpdateFoldings(_foldingManager, Doc);
                return;
            }

            _foldingCts?.Cancel();
            var cts = new CancellationTokenSource();
            _foldingCts = cts;
            var lang = Lang;
            Task.Run(() =>
            {
                if (cts.IsCancellationRequested) return;
                List<NewFolding> foldings;
                int firstError;
                try
                {
                    foldings = FoldingScanner.CreateBrackets(text, lang, out firstError);
                }
                catch
                {
                    return;
                }
                if (cts.IsCancellationRequested) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ReferenceEquals(_foldingCts, cts)) return;
                    _foldingManager.UpdateFoldings(foldings, firstError);
                }));
            }, cts.Token);
        }

        void UpdateHighlighting()
        {
            var textView = Editor.TextArea.TextView;
            if (_colorizer != null)
            {
                textView.LineTransformers.Remove(_colorizer);
                _colorizer = null;
            }
            var def = DocHighlighting.DefinitionFor(Lang);
            if (def != null)
            {
                _colorizer = new ThemeHighlightingColorizer(def);
                textView.LineTransformers.Add(_colorizer);
            }
            textView.Redraw();
            UpdatePythonLsp();
        }

        public bool FormatJson(out string error)
        {
            error = null;
            string text = PlainText();
            try
            {
                using var doc = JsonDocument.Parse(text,
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                    doc.WriteTo(writer);
                string result = Norm(Encoding.UTF8.GetString(ms.ToArray()));
                int caret = Editor.CaretOffset;
                _loading = true;
                Doc.BeginUpdate();
                try
                {
                    Doc.Replace(0, DocLength, result);
                }
                finally
                {
                    Doc.EndUpdate();
                    _loading = false;
                }
                Doc.UndoStack.ClearRedoStack();
                CaretIndex = caret;
                SetDirty(true);
                StateChanged?.Invoke(this, EventArgs.Empty);
                ScheduleSyntaxCheck();
                ScheduleFolding();
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ---------- Datei ----------

        public void OpenFile(string path)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"Die Datei \"{path}\" konnte nicht geöffnet werden.\n\n{ex.Message}",
                    "NovaNote", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var enc = Codec.Detect(data, out int skip);
            string text = Codec.Decode(data, enc, skip);
            Eol = Codec.DetectEol(text);
            Enc = enc;
            SetText(Codec.NormalizeToLf(text));
            ClearUndoStack();
            Path_ = path;
            Editor.CaretOffset = 0;
            DetectLanguage();
            var extLang = DocLanguages.FromExtension(path);
            if (extLang == DocLanguage.Markdown || extLang == DocLanguage.Python ||
                extLang == DocLanguage.CSharp || extLang == DocLanguage.Yaml)
            {
                if (Lang != extLang) SetLanguage(extLang);
            }
            UpdateMarkdownVisibility();
            SetDirty(false);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool PromptDirty()
        {
            if (!IsDirty) return true;
            string name = Path_ == null ? "Unbenannt" : System.IO.Path.GetFileName(Path_);
            var owner = Window.GetWindow(this);
            var r = owner != null
                ? MessageBox.Show(owner,
                    $"Änderungen an \"{name}\" sollen gespeichert werden?",
                    "NovaNote – Speichern?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
                : MessageBox.Show(
                    $"Änderungen an \"{name}\" sollen gespeichert werden?",
                    "NovaNote – Speichern?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return false;
            if (r == MessageBoxResult.Yes) return SavePath();
            return true;
        }

        public bool SavePath()
        {
            if (Path_ == null) return SaveAsCore();
            return WriteFile(Path_);
        }

        public bool Save()
        {
            if (Path_ == null) return SaveAsCore();
            return WriteFile(Path_);
        }

        public bool SaveAsCore()
        {
            string ext = CurrentSaveExtension();
            var dlg = new SaveFileDialog
            {
                Filter = "Textdokumente (*.txt)|*.txt|Markdown (*.md)|*.md|Python (*.py)|*.py|C# (*.cs)|*.cs|YAML (*.yaml)|*.yaml;*.yml|Alle Dateien (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = ext,
                AddExtension = true,
                FileName = Path_ == null ? $"Unbenannt.{ext}" : System.IO.Path.GetFileName(Path_)
            };
            if (Path_ != null) dlg.InitialDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path_));
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return false;
            Path_ = dlg.FileName;
            DetectLanguage();
            StateChanged?.Invoke(this, EventArgs.Empty);
            return WriteFile(Path_);
        }

        string CurrentSaveExtension()
        {
            if (Path_ != null)
            {
                string ext = System.IO.Path.GetExtension(Path_);
                if (ext.Length > 1) return ext.Substring(1);
            }
            switch (Lang)
            {
                case DocLanguage.Markdown: return "md";
                case DocLanguage.Python: return "py";
                case DocLanguage.CSharp: return "cs";
                case DocLanguage.Yaml: return "yaml";
                default: return "txt";
            }
        }

        public bool WriteFile(string path)
        {
            try
            {
                string text = Codec.ApplyEol(PlainText(), Eol);
                File.WriteAllBytes(path, Codec.Encode(text, Enc));
                Doc.UndoStack.MarkAsOriginalFile();
                SetDirty(false);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"Die Datei konnte nicht gespeichert werden.\n\n{ex.Message}",
                    "NovaNote", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        public void ReplaceText(string text)
        {
            SetText(text);
            ClearUndoStack();
            Editor.CaretOffset = 0;
            SetDirty(false);
            DetectLanguage();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InsertDateTime()
        {
            Editor.TextArea.PerformTextInput(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            Editor.Focus();
        }

        public void GoToLine(int n)
        {
            n = Math.Clamp(n, 1, Math.Max(1, LineCount));
            int offset = Doc.GetOffset(n, 1);
            Editor.CaretOffset = offset;
            Editor.ScrollTo(n, 1);
            Editor.Focus();
        }

        void ScrollToIndex(int index)
        {
            var line = Doc.GetLineByOffset(Math.Clamp(index, 0, DocLength));
            Editor.ScrollTo(line.LineNumber, index - line.Offset + 1);
        }

        (int idx, int len) FindMatch(string t, string term, int from, bool matchCase, bool whole, bool backward)
        {
            if (string.IsNullOrEmpty(term) || t.Length == 0) return (-1, 0);
            from = Math.Clamp(from, 0, t.Length);

            var opts = RegexOptions.ExplicitCapture;
            if (!matchCase) opts |= RegexOptions.IgnoreCase;
            string pattern = whole ? "(?<!\\w)" + Regex.Escape(term) + "(?!\\w)" : Regex.Escape(term);
            var rx = new Regex(pattern, opts);

            if (!backward)
            {
                var m = rx.Match(t, from);
                if (!m.Success) m = rx.Match(t, 0);
                return m.Success ? (m.Index, m.Length) : (-1, 0);
            }
            else
            {
                Match last = null;
                foreach (Match m in rx.Matches(t))
                {
                    if (m.Index < from) last = m;
                    else break;
                }
                if (last == null)
                {
                    foreach (Match m in rx.Matches(t)) last = m;
                }
                return last != null ? (last.Index, last.Length) : (-1, 0);
            }
        }

        public bool DoSearch(string term, bool matchCase, bool whole, bool backward)
        {
            int from = backward ? Math.Max(CaretIndex - 1, 0) : CaretIndex;
            var (idx, len) = FindMatch(PlainText(), term, from, matchCase, whole, backward);
            if (idx < 0)
            {
                MessageBox.Show(Window.GetWindow(this), $"\"{term}\" wurde nicht gefunden.", "Suchen",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            Select(idx, len);
            ScrollToIndex(idx);
            Editor.Focus();
            return true;
        }

        public void ReplaceCurrent(string term, string repl, bool matchCase, bool whole)
        {
            int s = SelStart, l = SelLength;
            if (l > 0 && !string.IsNullOrEmpty(term) && l == term.Length &&
                string.Compare(Doc.GetText(s, l), term,
                    matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == 0)
            {
                ReplaceRange(s, l, repl);
                Editor.CaretOffset = Math.Clamp(s + repl.Length, 0, DocLength);
            }
            DoSearch(term, matchCase, whole, false);
        }

        public int ReplaceAll(string term, string repl, bool matchCase, bool whole)
        {
            if (string.IsNullOrEmpty(term)) return 0;
            var opts = RegexOptions.ExplicitCapture;
            if (!matchCase) opts |= RegexOptions.IgnoreCase;
            string pattern = whole ? "(?<!\\w)" + Regex.Escape(term) + "(?!\\w)" : Regex.Escape(term);
            var rx = new Regex(pattern, opts);
            string text = PlainText();
            var matches = rx.Matches(text);
            int count = matches.Count;
            if (count == 0) return 0;
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in matches)
            {
                sb.Append(text, pos, m.Index - pos);
                sb.Append(repl);
                pos = m.Index + m.Length;
            }
            sb.Append(text, pos, text.Length - pos);
            string result = sb.ToString();
            _loading = true;
            Doc.BeginUpdate();
            try
            {
                Doc.Replace(0, DocLength, result);
            }
            finally
            {
                Doc.EndUpdate();
                _loading = false;
            }
            Doc.UndoStack.ClearRedoStack();
            CaretIndex = result.Length;
            SetDirty(true);
            StateChanged?.Invoke(this, EventArgs.Empty);
            ScheduleSyntaxCheck();
            ScheduleFolding();
            return count;
        }

        public void Print(double marginL, double marginT, double marginR, double marginB, Window owner, out string printerName)
        {
            printerName = null;
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;
            printerName = pd.PrintQueue.FullName;

            double paperW = 8.5 * 96, paperH = 11 * 96;
            var media = pd.PrintTicket?.PageMediaSize;
            if (media?.Width != null) paperW = media.Width.Value / 25.4 * 96;
            if (media?.Height != null) paperH = media.Height.Value / 25.4 * 96;

            var doc = new FlowDocument
            {
                PageWidth = paperW,
                PageHeight = paperH,
                PagePadding = new Thickness(marginL * 96, marginT * 96, marginR * 96, marginB * 96),
                FontFamily = _baseFamily,
                FontSize = _baseFont * 0.75,
                FontWeight = _baseWeight,
                FontStyle = _baseStyle,
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                ColumnWidth = double.PositiveInfinity
            };
            var para = new Paragraph(new Run(PlainText())) { Margin = new Thickness(0) };
            doc.Blocks.Add(para);
            pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "NovaNote");
            Editor.Focus();
        }

        public (double scrollX, double scrollY, int caret) GetScrollState()
            => (Editor.HorizontalOffset, Editor.VerticalOffset, Editor.CaretOffset);

        public void RestoreScrollState(double scrollX, double scrollY, int caret)
        {
            Editor.CaretOffset = Math.Clamp(caret, 0, DocLength);
            Editor.ScrollToHorizontalOffset(scrollX);
            Editor.ScrollToVerticalOffset(scrollY);
        }

        public void RestoreSession(string path, string text, NovaNoteEncoding enc, LineEnding eol,
                                   int zoom, bool wrap, double scrollX, double scrollY, int caret, bool dirty,
                                   DocLanguage language)
        {
            SetWordWrap(wrap);
            SetText(text ?? "");
            ClearUndoStack();
            Path_ = path;
            Enc = enc;
            Eol = eol;
            Lang = language;
            var restoreExt = DocLanguages.FromExtension(path);
            if (Lang == DocLanguage.None &&
                (restoreExt == DocLanguage.Markdown || restoreExt == DocLanguage.Python ||
                 restoreExt == DocLanguage.CSharp || restoreExt == DocLanguage.Yaml))
                Lang = restoreExt;
            if (Lang == DocLanguage.None && SessionStore.Prefs.LanguageAuto)
                Lang = DocLanguages.DetectContent(text ?? "");
            UpdateHighlighting();
            UpdateMarkdownVisibility();
            SetZoom(zoom);
            IsDirty = dirty;
            ScheduleSyntaxCheck();
            ScheduleFolding();
            ScheduleMarkdownPreview();
            Dispatcher.BeginInvoke(new Action(() => RestoreScrollState(scrollX, scrollY, caret)),
                System.Windows.Threading.DispatcherPriority.Background);
            StateChanged?.Invoke(this, EventArgs.Empty);
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------- Drag & Drop / Transfer ----------

        void View_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var w = Window.GetWindow(this) as MainWindow;
                if (w != null) w.OpenInNewTab(files[0]);
                e.Handled = true;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _completionWindow?.Close();
            _completionWindow = null;
            EditorMenuPopup.IsOpen = false;
            DisposePythonLsp();
            _syntaxTimer.Stop();
            _foldingTimer.Stop();
            _syntaxCts?.Cancel();
            _foldingCts?.Cancel();
        }

        sealed class LspCompletion : ICompletionData
        {
            private readonly string _text;
            private readonly string _displayText;
            private readonly string _description;

            public LspCompletion(string displayText, string text, string description)
            {
                _displayText = displayText ?? "";
                _text = text ?? "";
                _description = description ?? "";
            }

            public string Text => _text;
            public object Content => _displayText;
            public object Description => _description;
            public ImageSource Image => null;
            public double Priority => 0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, _text);
                textArea.Caret.Offset = completionSegment.Offset + _text.Length;
            }
        }

        public void TransferOut()
        {
            switch (Parent)
            {
                case ContentControl cc:
                    if (cc.Content == this) cc.Content = null;
                    break;
                case Decorator dec:
                    if (dec.Child == this) dec.Child = null;
                    break;
                case Panel p:
                    p.Children.Remove(this);
                    break;
            }
        }
    }
}
