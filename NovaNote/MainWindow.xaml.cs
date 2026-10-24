using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace NovaNote
{
    public partial class MainWindow : Window
    {
        public const string ViewDataFormat = "NovaNoteView";

        public static FontFamily BaseFamily = new FontFamily("Consolas");
        public static FontStyle BaseStyle = FontStyles.Normal;
        public static FontWeight BaseWeight = FontWeights.Normal;
        public static double BaseSize = 15;
        public static string LastPrinterName = null;
        public static double MargL = 1, MargT = 1, MargR = 1, MargB = 1;
        public static List<ClosedDoc> ClosedHistory = new List<ClosedDoc>();

        public ObservableCollection<TabModel> Tabs = new ObservableCollection<TabModel>();
        TabModel _active;
        bool _initializing = true;
        bool _restored;
        public bool Detached { get; set; }

        string _lastTerm = "", _lastRepl = "";
        bool _lastMatchCase, _lastWhole;
        bool _sessionDirty;
        SearchDialog _searchDlg;
        GoToDialog _gotoDlg;
        DispatcherTimer _autosave;
        DispatcherTimer _status;
        readonly ContextMenu _langMenu = new ContextMenu();
        readonly List<MenuItem> _langMenuItems = new List<MenuItem>();
        MenuItem _langAutoMenuItem;
        readonly ContextMenu _encMenu = new ContextMenu();
        readonly List<MenuItem> _encMenuItems = new List<MenuItem>();
        readonly ContextMenu _eolMenu = new ContextMenu();
        readonly List<MenuItem> _eolMenuItems = new List<MenuItem>();
        Point? _dragStart;
        TabModel _dragTab;
        bool _closingForUpdate;

        bool _aiApplying;
        bool _aiSyncing;
        bool _aiBusy;
        CancellationTokenSource _aiCts;
        readonly List<AiMessage> _aiMessages = new List<AiMessage>();
        AiToolCall _aiPendingCall;
        string _aiPendingChatId;
        int _aiPendingRound;
        AiSettingsDialog _aiDlg;
        FavoritesDialog _favDlg;
        readonly ObservableCollection<ChatImage> _attachments = new ObservableCollection<ChatImage>();
        DateTime _lastDropHint;
        int _tokenShown;
        string _tokenMode = "geschätzt";
        bool _contextRetried;

        static CoreWebView2Environment _chatEnv;
        static readonly JsonSerializerOptions ChatJson = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        bool _chatReady;
        bool _chatInitializing;
        bool _chatUnavailable;
        int _chatSeq;
        readonly List<string> _chatPending = new List<string>();
        readonly List<ChatEntry> _chatTranscript = new List<ChatEntry>();
        ChatEntry _chatStreamEntry;
        bool _chatStreamShown;
        readonly StringBuilder _chatStreamBuf = new StringBuilder();
        readonly object _chatStreamLock = new object();
        DispatcherTimer _chatFlush;

        public TabModel Active => _active;
        public DocumentView ActiveView => _active?.View;

        public MainWindow() : this(true, 0, null)
        {
        }

        public MainWindow(bool restoreSession, int windowIndex, SessionState session)
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            ThemeService.ThemeChanged += OnThemeChanged;
            KiHistory.Load();
            Closed += (s, e) => ThemeService.ThemeChanged -= OnThemeChanged;
            SetupBindings();
            BuildStatusLanguageMenu();
            BuildStatusEncodingMenus();
            TabStrip.ItemsSource = Tabs;
            TabStrip.SizeChanged += (s, e) => UpdateTabWidths();
            SizeChanged += (s, e) => SyncDefaultAiWidth();

            if (restoreSession && SessionStore.Prefs.RestoreSession && SessionStore.HasSession)
            {
                var st = session ?? SessionStore.LoadSession();
                var ws = st?.Windows != null && windowIndex >= 0 && windowIndex < st.Windows.Count
                    ? st.Windows[windowIndex] : null;
                if (ws != null && ws.Tabs.Count > 0)
                {
                    _initializing = true;
                    foreach (var t in ws.Tabs)
                    {
                        var v = NewView();
                        v.RestoreSession(t.Path, t.Text, t.Enc, t.Eol, t.Zoom, t.Wrap,
                            t.ScrollX, t.ScrollY, t.Caret, t.Dirty, (DocLanguage)t.Language);
                        AddView(v);
                    }
                    TabStrip.SelectedItem = Tabs[Math.Clamp(ws.Active, 0, Tabs.Count - 1)];
                    _active = TabStrip.SelectedItem as TabModel;
                    EditorHost.Content = _active?.View;
                    if (!double.IsNaN(ws.Left))
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Left = ws.Left; Top = ws.Top; Width = ws.Width; Height = ws.Height;
                    }
                    _restored = true;
                    _initializing = false;
                }
            }
            if (!_restored)
            {
                _initializing = true;
                var first = NewView();
                AddView(first);
                _initializing = false;
                _active = Tabs.Count > 0 ? Tabs[0] : null;
                EditorHost.Content = _active?.View;
            }

            ApplyMultiTabUI();
            UpdateMenuChecks();
            UpdateTabWidths();
            UpdateTitle();
            UpdateStatus();

            AiStore.Changed += OnAiStoreChanged;
            Closed += (s, e) => AiStore.Changed -= OnAiStoreChanged;
            ApplyAiPanel(SessionStore.Prefs.AiPanelOpen);

            _chatFlush = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
            _chatFlush.Tick += (s, e) => FlushChatStream();
            Closed += (s, e) => DisposeChat();

            _autosave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autosave.Tick += (s, e) =>
            {
                if (!_sessionDirty) return;
                SaveSessionNow();
                _sessionDirty = false;
            };
            _autosave.Start();
            Closing += MainWindow_Closing;
        }

        public DocumentView NewView()
        {
            var v = new DocumentView();
            v.SetBaseFont(BaseFamily, BaseStyle, BaseWeight, BaseSize);
            return v;
        }

        // ---------- Tab-Verwaltung ----------

        TabModel AddView(DocumentView view, int index = -1)
        {
            var tab = new TabModel(view);
            view.StateChanged += View_StateChanged;
            view.DirtyChanged += View_DirtyChanged;
            view.ContextAction += View_ContextAction;
            if (index < 0 || index > Tabs.Count) Tabs.Add(tab);
            else Tabs.Insert(index, tab);
            TabStrip.SelectedItem = tab;
            UpdateTabWidths();
            _sessionDirty = true;
            return tab;
        }

        void RemoveTab(TabModel tab, bool disposeView = true)
        {
            int idx = Tabs.IndexOf(tab);
            Unhook(tab.View);
            Tabs.Remove(tab);
            if (_active == tab)
            {
                _active = null;
                EditorHost.Content = null;
                if (Tabs.Count > 0) TabStrip.SelectedItem = Tabs[Math.Min(idx, Tabs.Count - 1)];
                if (Tabs.Count > 0) ActivateTab(TabStrip.SelectedItem as TabModel);
            }
            UpdateTabWidths();
            _sessionDirty = true;
            if (disposeView) tab.View.Dispose();
        }

        void Unhook(DocumentView view)
        {
            view.StateChanged -= View_StateChanged;
            view.DirtyChanged -= View_DirtyChanged;
            view.ContextAction -= View_ContextAction;
        }

        public TabModel FindTab(DocumentView view)
            => Tabs.FirstOrDefault(t => ReferenceEquals(t.View, view));

        public bool ActivateExistingTab(string path)
        {
            var full = FullPath(path);
            if (full == null) return false;

            var tab = Tabs.FirstOrDefault(t =>
                string.Equals(FullPath(t.View.Path_), full, StringComparison.OrdinalIgnoreCase));
            if (tab == null) return false;

            TabStrip.SelectedItem = tab;
            ActivateTab(tab);
            return true;
        }

        static string FullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return Path.GetFullPath(path); }
            catch { return null; }
        }

        void PushHistory(TabModel tab)
        {
            if (tab == null) return;
            var v = tab.View;
            ClosedHistory.Add(new ClosedDoc(v.Path_, v.CurrentText, v.Enc, v.Eol, v.Zoom, v.WordWrap,
                v.IsDirty, v.Lang));
            while (ClosedHistory.Count > 15) ClosedHistory.RemoveAt(0);
            TabRestoreItem.IsEnabled = ClosedHistory.Count > 0;
        }

        void View_StateChanged(object sender, EventArgs e)
        {
            _sessionDirty = true;
            FindTab(sender as DocumentView)?.RefreshTitle();
            if (ReferenceEquals(sender, _active?.View))
            {
                UpdateTitle();
                UpdateStatus();
                UpdateEditState();
            }
        }

        void View_DirtyChanged(object sender, EventArgs e)
        {
            _sessionDirty = true;
            FindTab(sender as DocumentView)?.RefreshTitle();
            if (ReferenceEquals(sender, _active?.View)) UpdateTitle();
        }

        void View_ContextAction(DocumentView view, DocAction action, bool value)
        {
            var tab = FindTab(view);
            if (tab != null && !ReferenceEquals(tab, _active)) ActivateTab(tab);
            var v = ActiveView;
            if (v == null) return;

            switch (action)
            {
                case DocAction.Undo: v.DoUndo(); break;
                case DocAction.Redo: v.DoRedo(); break;
                case DocAction.Cut: v.DoCut(); break;
                case DocAction.Copy: v.DoCopy(); break;
                case DocAction.Paste: v.DoPaste(); break;
                case DocAction.Delete: v.DoDelete(); break;
                case DocAction.SelectAll: v.DoSelectAll(); break;
                case DocAction.FormatJson: JsonFormat(); break;
                case DocAction.FormatCode: FormatCode(); break;
                case DocAction.PythonCompletion: PythonCompletion(); break;
                case DocAction.NextSyntaxError: NextSyntaxError(); break;
                case DocAction.SyntaxCheck: SetSyntaxCheckAll(value); break;
                case DocAction.Folding: SetFoldingAll(value); break;
                case DocAction.Find: ShowSearch(false); break;
                case DocAction.FindNext: FindNext(false); break;
                case DocAction.Replace: ShowSearch(true); break;
                case DocAction.GoToLine: ShowGoTo(); break;
                case DocAction.InsertDateTime: v.InsertDateTime(); break;
                case DocAction.Save: v.Save(); break;
                case DocAction.SaveAs: v.SaveAsCore(); break;
                case DocAction.Print: Print(); break;
                case DocAction.WordWrap: v.SetWordWrap(value); break;
                case DocAction.ZoomIn: ZoomBy(10); break;
                case DocAction.ZoomOut: ZoomBy(-10); break;
                case DocAction.ZoomReset: v.SetZoom(100); break;
                case DocAction.Detach: DetachActive(); break;
            }

            UpdateStatus();
            UpdateMenuChecks();
            v.FocusEditor();
        }

        void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            ActivateTab(TabStrip.SelectedItem as TabModel);
        }

        void ActivateTab(TabModel tab)
        {
            if (tab == null) return;
            _active = tab;
            EditorHost.Content = tab.View;
            UpdateTitle();
            UpdateStatus();
            UpdateMenuChecks();
            tab.View.FocusEditor();
        }

        bool PromptDirtyFocused(TabModel tab)
        {
            if (tab == null || !tab.View.IsDirty) return true;
            ActivateTab(tab);
            return tab.View.PromptDirty();
        }

        void SwitchTab(int dir)
        {
            if (Tabs.Count == 0) return;
            int idx = Tabs.IndexOf(_active);
            int next = ((idx < 0 ? 0 : idx) + dir) % Tabs.Count;
            if (next < 0) next += Tabs.Count;
            TabStrip.SelectedItem = Tabs[next];
        }

        void NewTab()
        {
            if (!SessionStore.Prefs.MultiTab)
            {
                if (_active != null && !_active.View.PromptDirty()) return;
                PushHistory(_active);
                if (_active != null) RemoveTab(_active);
                AddView(NewView());
                return;
            }
            AddView(NewView());
        }

        void OpenTab()
        {
            var dlg = new OpenFileDialog
            {
                Filter = DocLanguages.Filter,
                FilterIndex = DocLanguages.FilterIndex(DocLanguage.None)
            };
            var p = ActiveView?.Path_;
            if (p != null) dlg.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(p));
            if (dlg.ShowDialog(this) == true) OpenInNewTab(dlg.FileName);
        }

        public void OpenInNewTab(string path)
        {
            if (!SessionStore.Prefs.MultiTab)
            {
                if (_active != null && !_active.View.PromptDirty()) return;
                var v1 = NewView();
                v1.OpenFile(path);
                PushHistory(_active);
                if (_active != null) RemoveTab(_active);
                AddView(v1);
                return;
            }
            if (Tabs.Count == 1 && _active != null && _active.View.Path_ == null &&
                !_active.View.IsDirty && _active.View.CurrentText.Length == 0)
            {
                _active.View.OpenFile(path);
                _active.RefreshTitle();
                UpdateTitle();
                UpdateStatus();
                UpdateMenuChecks();
                return;
            }
            var view = NewView();
            view.OpenFile(path);
            AddView(view);
        }

        public void CloseTab(TabModel tab)
        {
            if (tab == null) return;
            if (!PromptDirtyFocused(tab)) return;
            PushHistory(tab);
            RemoveTab(tab);
            if (Tabs.Count == 0)
            {
                SaveSessionNow();
                Close();
                return;
            }
        }

        void CloseAllTabs()
        {
            var tabs = Tabs.ToList();
            foreach (var tab in tabs)
                if (!PromptDirtyFocused(tab)) return;
            foreach (var tab in tabs)
            {
                PushHistory(tab);
                RemoveTab(tab);
            }
            if (Tabs.Count == 0)
            {
                SaveSessionNow();
                Close();
                return;
            }
            TabStrip.SelectedItem = Tabs[0];
            SaveSessionNow();
        }

        void RestoreLastClosed()
        {
            if (ClosedHistory.Count == 0) return;
            var d = ClosedHistory[ClosedHistory.Count - 1];
            ClosedHistory.RemoveAt(ClosedHistory.Count - 1);
            var v = NewView();
            v.RestoreSession(d.Path, d.Text, d.Enc, d.Eol, d.Zoom, d.Wrap, 0, 0, 0, d.Dirty, d.Language);
            AddView(v);
            UpdateMenuChecks();
        }

        void DetachActive()
        {
            if (!SessionStore.Prefs.MultiTab || _active == null) return;
            var view = _active.View;
            RemoveTab(_active, false);
            if (Tabs.Count == 0) AddView(NewView());
            var w = new MainWindow(false, -1, null) { Detached = true };
            w.AddView(view);
            var start = w.Tabs.FirstOrDefault(t => !ReferenceEquals(t.View, view));
            if (start != null) w.RemoveTab(start);
            w.Show();
            SaveSessionNow();
        }

        void ApplyMultiTabUI()
        {
            TabStripRow.Visibility = SessionStore.Prefs.MultiTab
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateTabWidths();
        }

        void UpdateTabWidths()
        {
            double avail = Math.Max(140, TabStripRow.ActualWidth - 48);
            int n = Math.Max(1, Tabs.Count);
            double w = Math.Clamp(avail / n - 2, 100, 260);
            foreach (var t in Tabs) t.Width = w;
            TabCloseAllItem.IsEnabled = SessionStore.Prefs.MultiTab && Tabs.Count >= 2;
        }

        // ---------- Titel & Status ----------

        string _lastTitle = null;

        void UpdateTitle()
        {
            string title = (ActiveView?.TabTitle ?? "Unbenannt") + " - NovaNote";
            if (title == _lastTitle) return;
            _lastTitle = title;
            Title = title;
        }

        void UpdateStatus()
        {
            if (_status == null)
            {
                _status = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
                _status.Tick += (s, e) => { _status.Stop(); UpdateStatusNow(); };
            }
            _status.Stop();
            _status.Start();
        }

        void UpdateStatusNow()
        {
            var v = ActiveView;
            if (v == null) return;
            var (caret, selection) = v.GetStatus();
            StatusCaret.Text = caret;
            StatusSelection.Text = selection;
            string syntax = v.SyntaxStatus;
            StatusSyntax.Text = syntax;
            bool failed = syntax.StartsWith("Syntax-Fehler", StringComparison.Ordinal);
            StatusSyntax.Foreground = failed
                ? (TryFindResource("SyntaxErrorBrush") as Brush ?? Brushes.Red)
                : (TryFindResource("SubtleTextBrush") as Brush ?? Brushes.Gray);
            SyntaxNextItem.IsEnabled = v.HasSyntaxErrors;
            StatusEncoding.Text = Codec.Name(v.Enc);
            StatusEol.Text = Codec.EolName(v.Eol);
            StatusZoom.Text = v.Zoom + " %";
            if (ZoomPopup?.IsOpen == true)
            {
                ZoomSlider.Value = v.Zoom;
                ZoomSliderValue.Text = v.Zoom + " %";
            }
            StatusLanguage.Text = v.StatusLanguage + (SessionStore.Prefs.LanguageAuto ? " · Automatik" : "");
        }

        void BuildStatusLanguageMenu()
        {
            foreach (var (header, tag) in new[]
            {
                ("Text", "None"), ("JSON", "Json"), ("XML", "Xml"), ("HTML", "Html"), ("CSS", "Css"),
                ("JavaScript", "JavaScript"), ("C++", "Cpp"), ("Markdown", "Markdown"),
                ("Python", "Python"), ("C#", "CSharp"), ("YAML", "Yaml")
            })
            {
                var mi = new MenuItem { Header = header, Tag = tag, IsCheckable = true };
                mi.Click += Language_Click;
                _langMenuItems.Add(mi);
                _langMenu.Items.Add(mi);
            }
            _langMenu.Items.Add(new Separator());
            _langAutoMenuItem = new MenuItem { Header = "Automatisch erkennen", IsCheckable = true };
            _langAutoMenuItem.Click += LangAuto_Click;
            _langMenu.Items.Add(_langAutoMenuItem);
            StatusLanguage.ContextMenu = _langMenu;
        }

        void StatusLanguage_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateLanguageChecks();
            _langMenu.PlacementTarget = StatusLanguage;
            _langMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            _langMenu.IsOpen = true;
            e.Handled = true;
        }

        void BuildStatusEncodingMenus()
        {
            foreach (var (header, tag) in new[]
            {
                ("UTF-8", "Utf8"), ("UTF-8 mit BOM", "Utf8Bom"), ("UTF-16 LE", "Utf16Le"),
                ("UTF-16 BE", "Utf16Be"), ("ANSI", "Ansi")
            })
            {
                var mi = new MenuItem { Header = header, Tag = tag, IsCheckable = true };
                mi.Click += Encoding_Click;
                _encMenuItems.Add(mi);
                _encMenu.Items.Add(mi);
            }
            foreach (var (header, tag) in new[]
            {
                ("Windows (CRLF)", "CrLf"), ("Unix (LF)", "Lf"), ("Macintosh (CR)", "Cr")
            })
            {
                var mi = new MenuItem { Header = header, Tag = tag, IsCheckable = true };
                mi.Click += Eol_Click;
                _eolMenuItems.Add(mi);
                _eolMenu.Items.Add(mi);
            }
            StatusEncoding.ContextMenu = _encMenu;
            StatusEol.ContextMenu = _eolMenu;
        }

        void StatusEncoding_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateEncodingChecks();
            _encMenu.PlacementTarget = StatusEncoding;
            _encMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            _encMenu.IsOpen = true;
            e.Handled = true;
        }

        void StatusEol_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateEolChecks();
            _eolMenu.PlacementTarget = StatusEol;
            _eolMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            _eolMenu.IsOpen = true;
            e.Handled = true;
        }

        void StatusSyntax_Click(object sender, MouseButtonEventArgs e)
        {
            SetSyntaxCheckAll(!SessionStore.Prefs.SyntaxCheck);
            e.Handled = true;
        }

        void StatusZoom_Click(object sender, MouseButtonEventArgs e)
        {
            var zoom = ActiveView?.Zoom ?? 100;
            ZoomSlider.Value = zoom;
            ZoomSliderValue.Text = zoom + " %";
            ZoomPopup.IsOpen = true;
            e.Handled = true;
        }

        void ZoomSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomSliderValue == null) return;
            ZoomSliderValue.Text = (int)e.NewValue + " %";
            ActiveView?.SetZoom((int)e.NewValue);
            UpdateStatus();
        }

        void UpdateLanguageChecks()
        {
            var lang = ActiveView?.Lang ?? DocLanguage.None;
            LangAutoItem.IsChecked = SessionStore.Prefs.LanguageAuto;
            LangNoneItem.IsChecked = lang == DocLanguage.None;
            LangJsonItem.IsChecked = lang == DocLanguage.Json;
            LangXmlItem.IsChecked = lang == DocLanguage.Xml;
            LangHtmlItem.IsChecked = lang == DocLanguage.Html;
            LangCssItem.IsChecked = lang == DocLanguage.Css;
            LangJsItem.IsChecked = lang == DocLanguage.JavaScript;
            LangCppItem.IsChecked = lang == DocLanguage.Cpp;
            LangMarkdownItem.IsChecked = lang == DocLanguage.Markdown;
            LangPythonItem.IsChecked = lang == DocLanguage.Python;
            LangCSharpItem.IsChecked = lang == DocLanguage.CSharp;
            LangYamlItem.IsChecked = lang == DocLanguage.Yaml;
            foreach (var mi in _langMenuItems)
                mi.IsChecked = Enum.TryParse<DocLanguage>(mi.Tag as string, out var l) && l == lang;
            if (_langAutoMenuItem != null) _langAutoMenuItem.IsChecked = SessionStore.Prefs.LanguageAuto;
            LangJsonFormatItem.IsEnabled = lang == DocLanguage.Json;
            CodeFormatItem.IsEnabled = ActiveView?.FormatSupported == true;
            PythonCompletionItem.IsEnabled = ActiveView?.PythonLspReady == true;
        }

        void UpdateMenuChecks()
        {
            var v = ActiveView;
            TabModeItem.IsChecked = SessionStore.Prefs.MultiTab;
            SessionRestoreItem.IsChecked = SessionStore.Prefs.RestoreSession;
            AskSaveOnCloseItem.IsChecked = SessionStore.Prefs.AskSaveOnClose;
            TabRestoreItem.IsEnabled = ClosedHistory.Count > 0;
            TabDetachItem.IsEnabled = SessionStore.Prefs.MultiTab;
            FormatWrapItem.IsChecked = v?.WordWrap == true;
            ViewLineNumbersItem.IsChecked = SessionStore.Prefs.ShowLineNumbers;
            ViewWhitespaceItem.IsChecked = SessionStore.Prefs.ShowWhitespace;
            SyntaxCheckItem.IsChecked = SessionStore.Prefs.SyntaxCheck;
            FoldingItem.IsChecked = SessionStore.Prefs.Folding;
            SyntaxNextItem.IsEnabled = ActiveView?.HasSyntaxErrors == true;
            UpdateAutoItem.IsChecked = SessionStore.Prefs.UpdateCheck;
            _aiApplying = true;
            AiPanelItem.IsChecked = AiPanel.Visibility == Visibility.Visible;
            AiToggle.IsChecked = AiPanel.Visibility == Visibility.Visible;
            AiAutoApplyItem.IsChecked = SessionStore.Prefs.AiAutoApply;
            _aiApplying = false;
            UpdateEncodingChecks();
            UpdateEolChecks();
            UpdateLanguageChecks();
            UpdateEditState();
            UpdateThemeChecks();
        }

        void UpdateEditState()
        {
            var v = ActiveView;
            EditUndoItem.IsEnabled = v?.CanUndo == true;
            EditRedoItem.IsEnabled = v?.CanRedo == true;
        }

        void UpdateEncodingChecks()
        {
            var v = ActiveView;
            EncUtf8Item.IsChecked = v?.Enc == NovaNoteEncoding.Utf8;
            EncUtf8BomItem.IsChecked = v?.Enc == NovaNoteEncoding.Utf8Bom;
            EncUtf16LeItem.IsChecked = v?.Enc == NovaNoteEncoding.Utf16Le;
            EncUtf16BeItem.IsChecked = v?.Enc == NovaNoteEncoding.Utf16Be;
            EncAnsiItem.IsChecked = v?.Enc == NovaNoteEncoding.Ansi;
            foreach (var mi in _encMenuItems)
                mi.IsChecked = mi.Tag is string t && Enum.TryParse<NovaNoteEncoding>(t, out var e2) && v?.Enc == e2;
        }

        void UpdateEolChecks()
        {
            var v = ActiveView;
            EolCrLfItem.IsChecked = v?.Eol == LineEnding.CrLf;
            EolLfItem.IsChecked = v?.Eol == LineEnding.Lf;
            EolCrItem.IsChecked = v?.Eol == LineEnding.Cr;
            foreach (var mi in _eolMenuItems)
                mi.IsChecked = mi.Tag is string t && Enum.TryParse<LineEnding>(t, out var e2) && v?.Eol == e2;
        }

        void UpdateThemeChecks()
        {
            ThemeLightItem.IsChecked = ThemeService.Mode == ThemeService.Light;
            ThemeDarkItem.IsChecked = ThemeService.Mode == ThemeService.Dark;
            ThemeSystemItem.IsChecked = ThemeService.Mode == ThemeService.System;
        }

        void OnThemeChanged()
        {
            UpdateThemeChecks();
            foreach (var t in Tabs) t.View.ReapplyHighlight();
            SendChatTheme();
        }

        // ---------- Session ----------

        void SaveSessionNow()
        {
            if (!SessionStore.Prefs.RestoreSession)
            {
                SessionStore.ClearSession();
                return;
            }
            var st = new SessionState();
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
            {
                if (w.Tabs.Count == 0) continue;
                var bounds = w.WindowState == System.Windows.WindowState.Normal
                    ? new Rect(w.Left, w.Top, w.Width, w.Height) : w.RestoreBounds;
                var ws = new SessionWindow
                {
                    Active = Math.Max(0, w.Tabs.IndexOf(w._active)),
                    Detached = w.Detached,
                    Left = bounds.X, Top = bounds.Y, Width = bounds.Width, Height = bounds.Height
                };
                foreach (var t in w.Tabs)
                {
                    var (sx, sy, car) = t.View.GetScrollState();
                    ws.Tabs.Add(new SessionTab
                    {
                        Path = t.View.Path_,
                        Text = t.View.CurrentText,
                        Enc = t.View.Enc,
                        Eol = t.View.Eol,
                        Zoom = t.View.Zoom,
                        Wrap = t.View.WordWrap,
                         ScrollX = sx, ScrollY = sy, Caret = car,
                         Dirty = t.View.IsDirty,
                         Language = (int)t.View.Lang
                    });
                }
                st.Windows.Add(ws);
            }
            if (st.Windows.Count > 0) SessionStore.SaveSession(st);
            else SessionStore.ClearSession();
            _sessionDirty = false;
        }

        public bool SaveSessionForUpdate()
        {
            foreach (var tab in Tabs.ToList())
            {
                if (!PromptDirtyFocused(tab))
                    return false;
            }
            SaveSessionNow();
            return true;
        }

        public void AllowUpdateClose()
        {
            _closingForUpdate = true;
        }

        // ---------- Datei ----------

        void NewTab_Click(object sender, RoutedEventArgs e) => NewTab();
        void OpenTab_Click(object sender, RoutedEventArgs e) => OpenTab();
        void FileSave_Click(object sender, RoutedEventArgs e) => ActiveView?.Save();
        void FileSaveAs_Click(object sender, RoutedEventArgs e) => ActiveView?.SaveAsCore();
        void TabClose_Click(object sender, RoutedEventArgs e) => CloseTab(_active);
        void TabRestore_Click(object sender, RoutedEventArgs e) => RestoreLastClosed();
        void TabCloseAll_Click(object sender, RoutedEventArgs e) => CloseAllTabs();
        void FileExit_Click(object sender, RoutedEventArgs e) => Close();

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_closingForUpdate)
            {
                _closingForUpdate = false;
                foreach (var tab in Tabs.ToList()) tab.View.Dispose();
                DisposeChat();
                return;
            }

            var tabs = Tabs.ToList();
            foreach (var tab in tabs)
            {
                if (!PromptDirtyFocused(tab))
                {
                    e.Cancel = true;
                    return;
                }
            }
            _autosave.Stop();
            _status?.Stop();
            SaveSessionNow();
            SessionStore.SavePrefs();
            foreach (var tab in tabs) tab.View.Dispose();
        }

        // ---------- Bearbeiten ----------

        void EditUndo_Click(object sender, RoutedEventArgs e) => ActiveView?.DoUndo();
        void EditRedo_Click(object sender, RoutedEventArgs e) => ActiveView?.DoRedo();
        void EditCut_Click(object sender, RoutedEventArgs e) => ActiveView?.DoCut();
        void EditCopy_Click(object sender, RoutedEventArgs e) => ActiveView?.DoCopy();
        void EditPaste_Click(object sender, RoutedEventArgs e) => ActiveView?.DoPaste();
        void EditDelete_Click(object sender, RoutedEventArgs e) => ActiveView?.DoDelete();
        void EditSelectAll_Click(object sender, RoutedEventArgs e) => ActiveView?.DoSelectAll();
        void EditDateTime_Click(object sender, RoutedEventArgs e) => ActiveView?.InsertDateTime();

        void EditFind_Click(object sender, RoutedEventArgs e) => ShowSearch(false);
        void EditReplace_Click(object sender, RoutedEventArgs e) => ShowSearch(true);
        void EditFindNext_Click(object sender, RoutedEventArgs e) => FindNext(false);
        void EditFindPrev_Click(object sender, RoutedEventArgs e) => FindNext(true);
        void EditGoto_Click(object sender, RoutedEventArgs e) => ShowGoTo();

        public int LineCount => ActiveView?.LineCount ?? 0;
        public string LastTerm => _lastTerm;

        public void SaveSearchArgs(string term, string repl, bool matchCase, bool whole)
        {
            _lastTerm = term;
            _lastRepl = repl;
            _lastMatchCase = matchCase;
            _lastWhole = whole;
        }

        void FindNext(bool backward)
        {
            if (string.IsNullOrEmpty(_lastTerm))
            {
                ShowSearch(false);
                return;
            }
            ActiveView?.DoSearch(_lastTerm, _lastMatchCase, _lastWhole, backward);
        }

        public bool DoSearch(string term, bool matchCase, bool whole, bool backward)
            => ActiveView?.DoSearch(term, matchCase, whole, backward) ?? false;

        public void ReplaceCurrent(string term, string repl, bool matchCase, bool whole)
            => ActiveView?.ReplaceCurrent(term, repl, matchCase, whole);

        public int ReplaceAll(string term, string repl, bool matchCase, bool whole)
            => ActiveView?.ReplaceAll(term, repl, matchCase, whole) ?? 0;

        public void ShowSearch(bool replace)
        {
            if (_searchDlg == null)
            {
                _searchDlg = new SearchDialog(this, replace) { Owner = this };
                _searchDlg.Closed += (s, e) => _searchDlg = null;
                _searchDlg.Show();
            }
            _searchDlg.Activate();
        }

        public void ShowGoTo()
        {
            if (_gotoDlg == null)
            {
                _gotoDlg = new GoToDialog(this) { Owner = this };
                _gotoDlg.Closed += (s, e) => _gotoDlg = null;
                _gotoDlg.Show();
            }
            _gotoDlg.Activate();
        }

        public void GoToLine(int n) => ActiveView?.GoToLine(n);

        // ---------- Format ----------

        void FormatWrap_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.SetWordWrap(FormatWrapItem.IsChecked);
            ActiveView?.FocusEditor();
        }

        void FormatFont_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FontPickerDialog(BaseFamily, BaseStyle, BaseWeight, BaseSize) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                BaseFamily = dlg.SelectedFamily;
                BaseStyle = dlg.SelectedStyle;
                BaseWeight = dlg.SelectedWeight;
                BaseSize = dlg.SelectedSize;
                foreach (var t in Tabs) t.View.SetFont(BaseFamily, BaseStyle, BaseWeight, BaseSize);
                UpdateStatus();
            }
            ActiveView?.FocusEditor();
        }

        void Encoding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag &&
                Enum.TryParse<NovaNoteEncoding>(tag, out var enc))
            {
                ActiveView?.SetEncoding(enc);
                UpdateEncodingChecks();
                UpdateStatus();
            }
        }

        void Eol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag &&
                Enum.TryParse<LineEnding>(tag, out var eol))
            {
                ActiveView?.SetEol(eol);
                UpdateEolChecks();
                UpdateStatus();
            }
        }

        void Language_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag &&
                Enum.TryParse<DocLanguage>(tag, out var lang))
            {
                SessionStore.Prefs.LanguageAuto = false;
                SessionStore.SavePrefs();
                ActiveView?.SetLanguage(lang);
                UpdateLanguageChecks();
                UpdateStatus();
            }
        }

        void LangAuto_Click(object sender, RoutedEventArgs e)
        {
            bool on = sender is MenuItem mi ? mi.IsChecked : LangAutoItem.IsChecked;
            SessionStore.Prefs.LanguageAuto = on;
            SessionStore.SavePrefs();
            if (on) ActiveView?.DetectLanguage();
            UpdateLanguageChecks();
            UpdateStatus();
        }

        void LangJsonFormat_Click(object sender, RoutedEventArgs e) => JsonFormat();

        void JsonFormat()
        {
            var v = ActiveView;
            if (v == null) return;
            if (v.Lang != DocLanguage.Json)
            {
                MessageBox.Show("Das Dokument ist kein JSON.", "JSON formatieren",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!v.FormatJson(out string err))
            {
                MessageBox.Show("JSON-Formatfehler:\n" + err, "JSON formatieren",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            UpdateStatus();
            v.FocusEditor();
        }

        void CodeFormat_Click(object sender, RoutedEventArgs e) => FormatCode();

        async void FormatCode()
        {
            var v = ActiveView;
            if (v == null) return;
            if (!v.FormatSupported)
            {
                Dispatcher.Invoke(new Action(() => MessageBox.Show(
                    "Code-Formatierung ist nur für Python und C# verfügbar.", "Code formatieren",
                    MessageBoxButton.OK, MessageBoxImage.Information)));
                return;
            }

            string err = null;
            Exception ex = null;
            try
            {
                err = await v.FormatCodeAsync();
            }
            catch (Exception caught)
            {
                ex = caught;
            }

            Dispatcher.Invoke(new Action(() =>
            {
                if (err != null)
                {
                    MessageBox.Show("Formatfehler:\n" + err, "Code formatieren",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (ex != null)
                {
                    MessageBox.Show("Formatfehler:\n" + ex.Message, "Code formatieren",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                UpdateStatus();
                v.FocusEditor();
            }));
        }

        void PythonCompletion_Click(object sender, RoutedEventArgs e) => PythonCompletion();

        async void PythonCompletion()
        {
            var v = ActiveView;
            if (v == null || !v.PythonLspReady) return;
            try
            {
                await v.ShowPythonCompletionAsync();
            }
            catch
            {
            }
        }

        // ---------- Ansicht ----------

        void TabDetach_Click(object sender, RoutedEventArgs e) => DetachActive();

        void TabMode_Click(object sender, RoutedEventArgs e)
        {
            bool on = TabModeItem.IsChecked;
            if (!on)
            {
                var extras = Tabs.Take(Math.Max(0, Tabs.Count - 1)).ToList();
                foreach (var tab in extras)
                {
                    if (!PromptDirtyFocused(tab))
                    {
                        TabModeItem.IsChecked = true;
                        return;
                    }
                    PushHistory(tab);
                    RemoveTab(tab);
                }
                if (Tabs.Count == 0) AddView(NewView());
                TabStrip.SelectedItem = Tabs[0];
            }
            SessionStore.Prefs.MultiTab = on;
            SessionStore.SavePrefs();
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
                w.ApplyMultiTabUI();
        }

        void SessionRestore_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.RestoreSession = SessionRestoreItem.IsChecked;
            SessionStore.SavePrefs();
            if (!SessionStore.Prefs.RestoreSession) SessionStore.ClearSession();
            else SaveSessionNow();
        }

        void AskSaveOnClose_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.AskSaveOnClose = AskSaveOnCloseItem.IsChecked;
            SessionStore.SavePrefs();
        }

        void ViewLineNumbers_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.ShowLineNumbers = ViewLineNumbersItem.IsChecked;
            SessionStore.SavePrefs();
            ApplyViewOptionsAll();
        }

        void ViewWhitespace_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.ShowWhitespace = ViewWhitespaceItem.IsChecked;
            SessionStore.SavePrefs();
            ApplyViewOptionsAll();
        }

        void SyntaxCheck_Click(object sender, RoutedEventArgs e) => SetSyntaxCheckAll(SyntaxCheckItem.IsChecked);

        void SetSyntaxCheckAll(bool on)
        {
            SessionStore.Prefs.SyntaxCheck = on;
            SessionStore.SavePrefs();
            SyntaxCheckItem.IsChecked = on;
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
                foreach (var t in w.Tabs) t.View.SetSyntaxCheck(on);
            UpdateStatus();
            ActiveView?.FocusEditor();
        }

        void SyntaxNext_Click(object sender, RoutedEventArgs e) => NextSyntaxError();

        void Folding_Click(object sender, RoutedEventArgs e) => SetFoldingAll(FoldingItem.IsChecked);

        void SetFoldingAll(bool on)
        {
            SessionStore.Prefs.Folding = on;
            SessionStore.SavePrefs();
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
                foreach (var t in w.Tabs) t.View.SetFolding(on);
            ActiveView?.FocusEditor();
        }

        void NextSyntaxError()
        {
            if (ActiveView?.GoToNextSyntaxError() == true) UpdateStatus();
        }

        void ApplyViewOptionsAll()
        {
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
                foreach (var t in w.Tabs) t.View.ApplyPreferences();
        }

        void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(10);
        void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(-10);
        void ZoomReset_Click(object sender, RoutedEventArgs e) => ActiveView?.SetZoom(100);

        void ZoomBy(int delta)
        {
            var v = ActiveView;
            if (v != null) v.SetZoom(v.Zoom + delta);
        }

        void StatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (ViewStatusBarItem.Parent is FrameworkElement border)
                border.Visibility = ViewStatusBarItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            ActiveView?.FocusEditor();
        }

        void Theme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag)
            {
                ThemeService.SetMode(tag);
                SessionStore.Prefs.Theme = tag;
                SessionStore.SavePrefs();
            }
            ActiveView?.FocusEditor();
        }

        // ---------- KI ----------

        void ToggleAiPanel() => ApplyAiPanel(AiPanel.Visibility != Visibility.Visible);

        void ApplyAiPanel(bool open)
        {
            _aiApplying = true;
            SessionStore.Prefs.AiPanelOpen = open;
            SessionStore.SavePrefs();
            AiPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            AiSplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            RootGrid.ColumnDefinitions[1].Width = open ? GridLength.Auto : new GridLength(0);
            AiPanel.MaxWidth = AiWidthMax();
            RootGrid.ColumnDefinitions[2].Width = open
                ? new GridLength(ClampAiWidth(SessionStore.Prefs.AiPanelWidth ?? DefaultAiWidth(), AiWidthMax()))
                : new GridLength(0);
            AiPanelItem.IsChecked = open;
            AiToggle.IsChecked = open;
            _aiApplying = false;

            if (open)
            {
                RefreshAiPanel();
                AiInput.Focus();
                _ = EnsureChatAsync();
                _ = AiDetectAsync();
            }
            else
            {
                ActiveView?.FocusEditor();
            }
        }

        double DefaultAiWidth()
        {
            double w = ActualWidth > 0 && !double.IsNaN(ActualWidth) ? ActualWidth : Width;
            if (double.IsNaN(w) || w <= 0) w = 900;
            return Math.Max(180, Math.Round(w * 0.5));
        }

        double AiWidthMax()
        {
            return SessionStore.Prefs.AiPanelWidth.HasValue ? 720 : Math.Max(720, DefaultAiWidth());
        }

        double ClampAiWidth(double w, double max = 720)
        {
            if (w < 180) return 180;
            if (w > max) return max;
            return Math.Round(w);
        }

        void SyncDefaultAiWidth()
        {
            if (_aiApplying || !AiPanel.IsVisible || SessionStore.Prefs.AiPanelWidth.HasValue) return;
            double max = AiWidthMax();
            AiPanel.MaxWidth = max;
            RootGrid.ColumnDefinitions[2].Width = new GridLength(ClampAiWidth(DefaultAiWidth(), max));
        }

        void AiSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!AiSplitter.IsVisible) return;
            SessionStore.Prefs.AiPanelWidth = ClampAiWidth(RootGrid.ColumnDefinitions[2].ActualWidth, AiPanel.MaxWidth);
            SessionStore.SavePrefs();
        }

        void AiToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_aiApplying) return;
            ApplyAiPanel(true);
        }

        void AiToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_aiApplying) return;
            ApplyAiPanel(false);
        }

        void AiPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_aiApplying) return;
            ApplyAiPanel(AiPanelItem.IsChecked);
        }

        void AiAutoApply_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.AiAutoApply = AiAutoApplyItem.IsChecked;
            SessionStore.SavePrefs();
            ActiveView?.FocusEditor();
        }

        void OnAiStoreChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => RefreshAiPanel()));
        }

        void RefreshAiPanel()
        {
            if (AiProviderBox == null) return;
            var provider = AiStore.ActiveProvider;
            var selectedModel = provider?.ActiveModel;

            _aiSyncing = true;
            AiProviderBox.ItemsSource = AiStore.State.Providers;
            AiProviderBox.SelectedItem = provider;
            AiModelBox.ItemsSource = provider?.Models ?? new List<AiModel>();
            AiModelBox.SelectedItem = provider?.Models.FirstOrDefault(m =>
                selectedModel != null && string.Equals(m.Id, selectedModel.Id, StringComparison.OrdinalIgnoreCase));
            _aiSyncing = false;

            UpdateAiStatus();
        }

        void AiProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_aiSyncing) return;
            if (AiProviderBox.SelectedItem is AiProvider provider) AiStore.SetActiveProvider(provider);
            RefreshAiPanel();
        }

        void AiModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_aiSyncing) return;
            var provider = AiStore.ActiveProvider;
            if (provider != null && AiModelBox.SelectedItem is AiModel model)
                AiStore.SetActiveModel(provider, model.Id);
            UpdateAiStatus();
        }

        void UpdateAiStatus()
        {
            var provider = AiStore.ActiveProvider;
            var model = provider?.ActiveModel;
            if (provider == null)
            {
                AiStatus.Text = "KI: kein Anbieter erfasst";
                AiStatus.ToolTip = "KI → KI-Einstellungen…";
                UpdateAiAttachUi();
                UpdateTokenBar();
                return;
            }
            if (model == null)
            {
                AiStatus.Text = $"KI: {provider.Name} · kein Modell · KI → Modelle scannen";
                AiStatus.ToolTip = provider.BaseUrl;
                UpdateAiAttachUi();
                UpdateTokenBar();
                return;
            }
            string vision = model.VisionOverride.HasValue
                ? (model.VisionEnabled ? "Ja (manuell)" : "Nein (manuell)")
                : (model.Vision ? "Ja" : "Nein");
            string ctx = model.ContextOverride.HasValue
                ? $"{model.EffectiveContext} (manuell)"
                : (model.ContextDetected ? model.EffectiveContext.ToString() : $"{model.EffectiveContext} (Annahme)");
            AiStatus.Text = $"KI: {provider.Name} · {model.DisplayName} · Kontext {ctx} · " +
                            $"Vision {vision} · Tools {(model.SupportsTools ? "Ja" : "Nein")}";
            AiStatus.ToolTip = $"{provider.BaseUrl} · Modell-ID {model.Id}";
            if (_aiMessages.Count > 0 && _tokenMode != "exakt (Server)")
            {
                _tokenShown = ContextBudget.Count(_aiMessages, provider, model);
                _tokenMode = ContextBudget.ModeLabel(ContextBudget.KindFor(provider, model));
            }
            UpdateAiAttachUi();
            UpdateTokenBar();
        }

        static string FmtTokens(int n)
        {
            if (n < 1000) return n.ToString();
            return (n / 1000.0).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + "k";
        }

        Brush FindBrush(string key)
        {
            try { return FindResource(key) as Brush; }
            catch { return null; }
        }

        Brush TokenBrushFor(double ratio)
        {
            if (ratio >= 0.95) return FindBrush("SyntaxErrorBrush") ?? FindBrush("AccentBrush");
            if (ratio >= 0.7) return FindBrush("WarnBrush") ?? FindBrush("AccentBrush");
            return FindBrush("AccentBrush");
        }

        void UpdateTokenBar()
        {
            if (AiTokenBar == null) return;
            var model = AiStore.ActiveProvider?.ActiveModel;
            if (model == null)
            {
                AiTokenText.Text = "Kontext –";
                AiTokenBar.Maximum = 1;
                AiTokenBar.Value = 0;
                AiTokenMode.Text = "0 %";
                AiTokenBar.ToolTip = "Kein Modell gewählt.";
                return;
            }

            int ctx = ContextBudget.ContextFor(model);
            int shown = _tokenShown;
            double ratio = ctx > 0 ? Math.Min(1.0, (double)shown / ctx) : 0;
            AiTokenBar.Maximum = ctx;
            AiTokenBar.Value = Math.Min(shown, ctx);
            var brush = TokenBrushFor(ratio);
            if (brush != null) AiTokenBar.Foreground = brush;
            AiTokenText.Text = shown <= 0 ? "Kontext frei" : $"{FmtTokens(shown)} / {FmtTokens(ctx)} Kontext";
            AiTokenMode.Text = $"{Math.Round(ratio * 100)} %";

            string limit = model.ContextOverride.HasValue
                ? $"{FmtTokens(ctx)} manuell gesetzt"
                : (model.ContextDetected ? $"{FmtTokens(ctx)} erkannt" : $"{FmtTokens(ctx)} angenommen");
            string tip = $"Prompt {shown} Tokens · Reserve {ContextBudget.Reserve(ctx)} für die Antwort · Limit {limit} · {_tokenMode}";
            AiTokenBar.ToolTip = tip;
            AiTokenMode.ToolTip = tip;
        }

        void PulseTokenBar()
        {
            if (AiTokenBar == null) return;
            var anim = new DoubleAnimation(1.0, 0.35, TimeSpan.FromMilliseconds(140)) { AutoReverse = true };
            AiTokenBar.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        bool VisionReady() => AiStore.SupportsVision(AiStore.ActiveProvider?.ActiveModel);

        void UpdateAiAttachUi()
        {
            if (AiAttachButton == null) return;
            bool vision = VisionReady();
            AiAttachButton.IsEnabled = vision && !_aiBusy;
            AiAttachButton.ToolTip = vision
                ? "Bild anhängen (auch Drag & Drop auf das Eingabefeld oder Strg+V für Screenshots)"
                : "Das aktuelle Modell unterstützt keine Bilder (Vision)";
            AiInput.AllowDrop = vision;
            AiAttachStrip.Visibility = _attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!vision && _attachments.Count > 0) ClearAttachments();
        }

        void ClearAttachments()
        {
            _attachments.Clear();
            UpdateAiAttachUi();
        }

        void AddAttachment(ChatImage image)
        {
            if (image == null)
            {
                AppendAi("Bild konnte nicht gelesen werden.");
                return;
            }
            if (!VisionReady())
            {
                AppendAi("Das aktuelle Modell unterstützt keine Bilder (Vision) — Anhang ignoriert.");
                return;
            }
            if (_attachments.Count >= ChatImages.MaxImages)
            {
                AppendAi($"Maximal {ChatImages.MaxImages} Bilder pro Nachricht.");
                return;
            }
            _attachments.Add(image);
            UpdateAiAttachUi();
            string extra = string.IsNullOrEmpty(image.Note) ? "" : $" — {image.Note}";
            AppendAi($"Bild angehängt: {image.FileName} ({image.Bytes} Bytes){extra}");
        }

        void AiAttach_Click(object sender, RoutedEventArgs e)
        {
            if (!VisionReady())
            {
                AppendAi("Das aktuelle Modell unterstützt keine Bilder (Vision). KI → Modelle scannen oder in „KI-Einstellungen“ Vision erzwingen.");
                return;
            }
            var dlg = new OpenFileDialog
            {
                Filter = "Bilder (*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp|Alle Dateien (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != true) return;
            foreach (var file in dlg.FileNames) AddAttachment(ChatImages.FromFile(file));
            AiInput.Focus();
        }

        void AiAttachRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ChatImage image)
            {
                _attachments.Remove(image);
                UpdateAiAttachUi();
            }
        }

        DragDropEffects DropEffectsFor(IDataObject data)
        {
            var files = ChatImages.ImageFilesFromDrop(data?.GetData(DataFormats.FileDrop));
            if (files.Count == 0) return VisionReady() ? DragDropEffects.Copy : DragDropEffects.None;
            if (!VisionReady())
            {
                var now = DateTime.Now;
                if (now - _lastDropHint > TimeSpan.FromSeconds(3))
                {
                    _lastDropHint = now;
                    AppendAi("Das aktuelle Modell unterstützt keine Bilder (Vision) — Drag & Drop blockiert.");
                }
                return DragDropEffects.None;
            }
            return DragDropEffects.Copy;
        }

        int AttachDroppedFiles(IDataObject data)
        {
            var files = ChatImages.ImageFilesFromDrop(data?.GetData(DataFormats.FileDrop));
            if (files.Count == 0) return 0;
            if (!VisionReady())
            {
                AppendAi($"Das aktuelle Modell unterstützt keine Bilder (Vision) — {files.Count} Datei(en) ignoriert.");
                return 0;
            }
            foreach (var file in files) AddAttachment(ChatImages.FromFile(file));
            return files.Count;
        }

        void AiInput_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DropEffectsFor(e.Data);
            e.Handled = true;
        }

        void AiInput_DragLeave(object sender, DragEventArgs e)
        {
            if (VisionReady()) e.Effects = DragDropEffects.None;
        }

        void AiInput_Drop(object sender, DragEventArgs e)
        {
            if (AttachDroppedFiles(e.Data) > 0) AiInput.Focus();
            e.Handled = true;
        }

        bool PasteClipboardImage()
        {
            if (!VisionReady()) return false;
            var image = ChatImages.FromClipboard();
            if (image == null) return false;
            AddAttachment(image);
            return true;
        }

        async Task AiDetectAsync()
        {
            var provider = AiStore.ActiveProvider;
            if (provider == null)
            {
                AiStatus.Text = "KI: kein Anbieter erfasst";
                return;
            }

            AiStatus.Text = $"KI: verbinde {provider.Name}…";
            var result = await LocalAiClient.ScanAsync(provider, CancellationToken.None);
            if (result.Error != null)
            {
                AiStatus.Text = "KI: " + result.Error;
                return;
            }

            AiStore.MergeScanned(provider, result.Models);
            AiStore.Save();
            RefreshAiPanel();
        }

        async void AiScan_Click(object sender, RoutedEventArgs e)
        {
            var provider = AiStore.ActiveProvider;
            if (provider == null)
            {
                AppendAi("Kein Anbieter erfasst. KI → KI-Einstellungen…");
                return;
            }

            AiScanItem.IsEnabled = false;
            AiStatus.Text = $"KI: scanne {provider.Name}…";
            var result = await LocalAiClient.ScanAsync(provider, CancellationToken.None);
            AiScanItem.IsEnabled = true;

            if (result.Error != null)
            {
                AppendAi("Scan: " + result.Error);
                AiStatus.Text = "KI: " + result.Error;
                return;
            }

            int added = AiStore.MergeScanned(provider, result.Models);
            AiStore.Save();
            RefreshAiPanel();
            AppendAi($"Scan: {result.Models.Count} Modelle bei {provider.Name}, {added} neue. " +
                     $"Aktives Modell: {provider.ActiveModel?.DisplayName ?? "keins"}.");
            ActiveView?.FocusEditor();
        }

        void AiSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_aiDlg == null)
            {
                _aiDlg = new AiSettingsDialog { Owner = this };
                _aiDlg.Closed += (s, ev) => _aiDlg = null;
                _aiDlg.ShowDialog();
                RefreshAiPanel();
            }
            ActiveView?.FocusEditor();
        }

        void AppendAi(string text) => AddChatMessage("info", text);

        // ---------- Chat-WebView ----------

        class ChatEntry
        {
            public string Id;
            public string Role;
            public string Content;
            public List<string> Images;
            public string ToolName;
            public string Args;
            public string Result;
            public string State;
            public bool IsError;
        }

        static string ChatAssetsFolder => Path.Combine(AppContext.BaseDirectory, "Assets", "Chat");

        static string ChatUserDataFolder => Path.Combine(SessionStore.DataDir, "WebView2");

        string NextChatId(string prefix) => prefix + "-" + (++_chatSeq);

        ChatEntry FindChatEntry(string id) => _chatTranscript.FirstOrDefault(e => e.Id == id);

        void PostChat(object payload)
        {
            string json = JsonSerializer.Serialize(payload, ChatJson);
            if (!_chatReady || AiChat?.CoreWebView2 == null)
            {
                _chatPending.Add(json);
                return;
            }
            try { AiChat.CoreWebView2.PostWebMessageAsJson(json); }
            catch { _chatPending.Add(json); }
        }

        void SendChatTheme()
        {
            if (!_chatReady) return;
            PostChat(new { type = "setTheme", dark = ThemeService.IsDark });
        }

        ChatEntry AddChatMessage(string role, string text, List<string> images = null)
        {
            var entry = new ChatEntry { Id = NextChatId(role), Role = role, Content = text, Images = images };
            _chatTranscript.Add(entry);
            PostChat(new { type = "addMessage", id = entry.Id, role = role, content = text, images = images });
            if (_chatUnavailable && AiLog != null)
            {
                string line = text;
                if (images != null && images.Count > 0) line += $" [{images.Count} Bild/er]";
                AiLog.AppendText(AiLog.Text.Length > 0 ? "\n" + line : line);
                AiLog.ScrollToEnd();
            }
            return entry;
        }

        async Task EnsureChatAsync()
        {
            if (AiChat == null || _chatReady || _chatInitializing || _chatUnavailable) return;
            _chatInitializing = true;
            try
            {
                string runtimeVersion;
                try { runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
                catch { runtimeVersion = null; }
                if (string.IsNullOrEmpty(runtimeVersion))
                {
                    ShowChatFallback("WebView2-Runtime nicht installiert — einfache Textausgabe.");
                    return;
                }

                try { Directory.CreateDirectory(ChatUserDataFolder); } catch { }
                if (_chatEnv == null) _chatEnv = await CoreWebView2Environment.CreateAsync(null, ChatUserDataFolder);
                await AiChat.EnsureCoreWebView2Async(_chatEnv);
                var core = AiChat.CoreWebView2;
                if (core == null)
                {
                    ShowChatFallback("WebView2 konnte nicht gestartet werden — einfache Textausgabe.");
                    return;
                }

                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                AiChat.AllowExternalDrop = false;
                core.NavigationStarting += Chat_NavigationStarting;
                core.WebMessageReceived += Chat_WebMessageReceived;
                core.SetVirtualHostNameToFolderMapping("novanote.chat", ChatAssetsFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                var nav = new TaskCompletionSource<bool>();
                core.NavigationCompleted += (s, ev) => nav.TrySetResult(true);
                core.Navigate("https://novanote.chat/chat.html");
                var done = await Task.WhenAny(nav.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                if (done != nav.Task)
                {
                    ShowChatFallback("WebView2-Chatfrontend nicht geladen — einfache Textausgabe.");
                    return;
                }

                _chatReady = true;
                AiChat.Visibility = Visibility.Visible;
                AiLog.Visibility = Visibility.Collapsed;
                _chatPending.Clear();
                ReplayChat();
                SendChatTheme();
                PostChat(new { type = "setBusy", busy = _aiBusy });
            }
            catch (Exception ex)
            {
                ShowChatFallback($"WebView2 nicht verfügbar ({ex.Message}) — einfache Textausgabe.");
            }
            finally
            {
                _chatInitializing = false;
            }
        }

        void ShowChatFallback(string hint)
        {
            _chatUnavailable = true;
            _chatReady = false;
            if (AiChat != null) AiChat.Visibility = Visibility.Collapsed;
            if (AiLog != null)
            {
                AiLog.Visibility = Visibility.Visible;
                foreach (var e in _chatTranscript.Where(e => e.Role == "info" || e.Role == "user"))
                    AiLog.AppendText(AiLog.Text.Length > 0 ? "\n" + e.Content : e.Content);
            }
            AiStatus.Text = "KI: " + hint;
            AiStatus.ToolTip = hint;
        }

        void ReplayChat()
        {
            foreach (var e in _chatTranscript)
            {
                if (e.Role == "tool")
                {
                    PostChat(new { type = "addToolCall", id = e.Id, toolName = e.ToolName, args = e.Args });
                    if (e.Result != null)
                        PostChat(new { type = "updateToolResult", id = e.Id, result = e.Result, isError = e.IsError });
                    PostChat(new { type = "updateToolStatus", id = e.Id, state = e.State ?? "running" });
                }
                else
                {
                    PostChat(new { type = "addMessage", id = e.Id, role = e.Role, content = e.Content, images = e.Images });
                }
            }
        }

        void DisposeChat()
        {
            try
            {
                _chatFlush?.Stop();
                AiChat?.Dispose();
            }
            catch
            {
            }
        }

        void Chat_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri == null || !e.Uri.StartsWith("https://novanote.chat/", StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        }

        void Chat_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                switch (ChatString(root, "action"))
                {
                    case "insertIntoEditor":
                        InsertChatCode(ChatString(root, "content"));
                        break;
                    case "confirmToolCall":
                        _ = ChatConfirmTool(ChatString(root, "id"));
                        break;
                    case "discardToolCall":
                        ChatDiscardTool(ChatString(root, "id"));
                        break;
                    case "chatEscape":
                        if (_aiBusy) _aiCts?.Cancel();
                        break;
                }
            }
            catch
            {
            }
        }

        static string ChatString(JsonElement e, string key)
        {
            if (e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
            return null;
        }

        void InsertChatCode(string code)
        {
            var view = ActiveView;
            if (view == null) { AppendAi("Kein aktives Dokument zum Einfügen."); return; }
            if (string.IsNullOrEmpty(code)) { AppendAi("Kein Code zum Einfügen."); return; }
            view.InsertAtCaret(code);
            view.FocusEditor();
            UpdateStatus();
            AppendAi("Code am Cursor eingefügt (Strg+Z macht es rückgängig).");
        }

        string BuildAiSystemPrompt(AiModel model, int imageCount = 0)
        {
            var view = ActiveView;
            string name = string.IsNullOrEmpty(view?.Path_) ? "neu (ohne Datei)" : System.IO.Path.GetFileName(view.Path_);
            string lang = view?.StatusLanguage ?? "Text";
            int lines = view?.LineCount ?? 0;
            int sel = view?.SelectedText?.Length ?? 0;

            var sb = new StringBuilder();
            sb.Append($"Du bist der KI-Assistent für NovaNote. Aktive Datei: {name} — {lang}, {lines} Zeilen");
            if (sel > 0) sb.Append($", {sel} Zeichen ausgewählt");
            sb.Append(".\n");
            sb.Append($"Dokument-Art: {lang}. get_language zeigt die Art, set_language stellt sie um (z. B. markdown, python, csharp); " +
                      "set_language vor dem Schreiben eines Markdown-/Code-Dokuments. format_code braucht python oder csharp.\n");
            if (imageCount > 0)
                sb.Append($"Der Nutzer hat {imageCount} Bild(er) angehängt (image_url-Parts im content-Array); beschreibe sie bei Bedarf.\n");
            sb.Append("Der Dokumenttext wird nicht automatisch geschickt. Lies ihn bei Bedarf selbst über die Werkzeuge: ");
            sb.Append("get_document (kompletter Text oder Ausschnitt mit from_line/count), search_document (Suchtreffer), ");
            sb.Append("goto_line/select_lines (Navigation/Auswahl).\n");
            sb.Append("Änderungen machst du mit replace_all_text, replace_lines oder insert_at_line und prüfst danach syntax_check.\n");
            sb.Append("Reine Fragen beantwortest du als Text ohne Codeblock; wenn du Code erzeugst, ");
            sb.Append("umschließe ihn mit dreifachen Backticks (```).\n");
            sb.Append($"Werkzeuge: {string.Join(", ", AiTools.Names)}.\n");
            return sb.ToString();
        }

        void TrimAiTranscript(AiProvider provider, AiModel model)
        {
            var lastImage = _aiMessages.LastOrDefault(m => m.Role == "user" && m.Images != null && m.Images.Count > 0);
            foreach (var m in _aiMessages)
                if (m != lastImage && m.Images != null)
                {
                    m.Images = null;
                    m.ImageTokens = 0;
                }

            var res = ContextBudget.ApplyBudget(provider, model, _aiMessages);
            _aiMessages.Clear();
            _aiMessages.AddRange(res.Messages);
            _tokenShown = res.Tokens;
            _tokenMode = ContextBudget.ModeLabel(ContextBudget.KindFor(provider, model));

            int ctx = ContextBudget.ContextFor(model);
            bool trimmed = res.DroppedMessages > 0 || res.DroppedImages > 0 || res.TruncatedText;
            if (res.DroppedMessages > 0)
                AppendAi($"Kontext voll ({FmtTokens(res.Tokens)} / {FmtTokens(ctx)} Tokens): {res.DroppedMessages} ältere Nachrichten gekürzt.");
            if (res.DroppedImages > 0)
                AppendAi($"Kontext voll: {res.DroppedImages} alte Bildanhänge entfernt.");
            if (res.TruncatedText)
                AppendAi("Kontext voll: die letzte Nachricht wurde gekürzt.");
            if (trimmed) PulseTokenBar();
            UpdateTokenBar();
        }

        void TrimAiHard(AiProvider provider, AiModel model)
        {
            var system = _aiMessages.Where(m => m.Role == "system").ToList();
            var rest = _aiMessages.Where(m => m.Role != "system").ToList();
            int keep = Math.Min(rest.Count, 4);
            var kept = rest.Skip(rest.Count - keep).ToList();
            while (kept.Count > 0 && kept[0].Role == "tool") kept.RemoveAt(0);
            _aiMessages.Clear();
            _aiMessages.AddRange(system);
            _aiMessages.AddRange(kept);
            _tokenShown = ContextBudget.Count(_aiMessages, provider, model);
            UpdateTokenBar();
        }

        void SetAiBusy(bool busy)
        {
            _aiBusy = busy;
            AiSendButton.IsEnabled = !busy;
            AiCancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            AiScanItem.IsEnabled = !busy;
            if (busy) AiStatus.Text = "KI: denkt…";
            UpdateAiAttachUi();
            PostChat(new { type = "setBusy", busy });
        }

        void ChatStreamChunk(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            lock (_chatStreamLock) _chatStreamBuf.Append(text);
        }

        void FlushChatStream(bool stop = false)
        {
            string text;
            lock (_chatStreamLock)
            {
                text = _chatStreamBuf.ToString();
                _chatStreamBuf.Clear();
            }
            if (text.Length > 0 && _chatStreamEntry != null)
            {
                _chatStreamEntry.Content += text;
                if (!_chatStreamShown)
                {
                    _chatStreamShown = true;
                    PostChat(new { type = "addMessage", id = _chatStreamEntry.Id, role = "assistant", content = text });
                }
                else
                {
                    PostChat(new { type = "appendChunk", id = _chatStreamEntry.Id, chunk = text });
                }
            }
            if (stop) _chatFlush?.Stop();
        }

        async void AiSend_Click(object sender, RoutedEventArgs e) => await AiSendAsync();

        async Task AiSendAsync()
        {
            if (_aiBusy) return;
            string prompt = AiInput.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;
            KiHistory.Add(prompt);

            var provider = AiStore.ActiveProvider;
            var model = provider?.ActiveModel;
            if (provider == null || model == null)
            {
                AppendAi("Kein Anbieter oder Modell gewählt. KI → KI-Einstellungen…");
                return;
            }

            if (_attachments.Count > 0 && !VisionReady())
            {
                AppendAi($"Das aktuelle Modell unterstützt keine Bilder (Vision) — {_attachments.Count} Anhang/Anhänge entfernt.");
                ClearAttachments();
            }
            List<string> images = VisionReady() && _attachments.Count > 0
                ? _attachments.Select(a => a.DataUri).ToList()
                : null;
            int imageCount = images?.Count ?? 0;
            int imageTokens = images != null ? _attachments.Sum(a => ChatImages.EstimateTokens(a)) : 0;

            AddChatMessage("user", prompt, images);
            AiInput.Clear();
            ClearAttachments();
            _aiPendingCall = null;
            _aiPendingChatId = null;
            _contextRetried = false;

            _aiMessages.RemoveAll(m => m.Role == "system");
            _aiMessages.Insert(0, new AiMessage { Role = "system", Content = BuildAiSystemPrompt(model, imageCount) });
            var userMsg = new AiMessage { Role = "user", Content = prompt, Images = images, ImageTokens = imageTokens };
            foreach (var m in _aiMessages)
                if (m != userMsg && m.Images != null)
                {
                    m.Images = null;
                    m.ImageTokens = 0;
                }
            _aiMessages.Add(userMsg);

            TrimAiTranscript(provider, model);

            _aiCts = new CancellationTokenSource();
            await AiRunAsync(0);
        }

        async Task AiRunAsync(int startRound)
        {
            var provider = AiStore.ActiveProvider;
            var model = provider?.ActiveModel;
            if (provider == null || model == null)
            {
                AppendAi("Kein Anbieter oder Modell gewählt. KI → KI-Einstellungen…");
                return;
            }

            SetAiBusy(true);
            try
            {
                for (int round = startRound; round < 6; round++)
                {
                    _chatStreamEntry = new ChatEntry { Id = NextChatId("msg"), Role = "assistant", Content = "" };
                    _chatTranscript.Add(_chatStreamEntry);
                    _chatStreamShown = false;
                    lock (_chatStreamLock) _chatStreamBuf.Clear();
                    _chatFlush.Start();

                    int estimateBefore = ContextBudget.Count(_aiMessages, provider, model);
                    var reply = await LocalAiClient.ChatStreamAsync(
                        provider, model, _aiMessages, AiTools.Definitions(), _aiCts.Token, ChatStreamChunk);
                    FlushChatStream(true);

                    if (reply.HasUsage)
                    {
                        ContextBudget.Calibrate(provider, model, reply.PromptTokens, estimateBefore);
                        _tokenShown = reply.PromptTokens + reply.CompletionTokens;
                        _tokenMode = "exakt (Server)";
                        UpdateTokenBar();
                    }

                    if (reply.Error != null)
                    {
                        if (_chatStreamEntry != null && _chatStreamEntry.Content.Length == 0)
                            _chatTranscript.Remove(_chatStreamEntry);

                        if (LocalAiClient.IsContextError(reply.Error) && !_contextRetried)
                        {
                            _contextRetried = true;
                            if (_chatStreamEntry != null) _chatTranscript.Remove(_chatStreamEntry);
                            TrimAiHard(provider, model);
                            AppendAi($"Kontext voll ({FmtTokens(_tokenShown)} / {FmtTokens(ContextBudget.ContextFor(model))}): ältere Nachrichten gekürzt, neuer Versuch.");
                            _chatStreamEntry = null;
                            continue;
                        }

                        string hint = LocalAiClient.IsContextError(reply.Error)
                            ? " — Kontext zu klein: ältere Nachrichten sind gekürzt; Kontext in „KI-Einstellungen…“ erhöhen oder „Verlauf löschen“."
                            : "";
                        AppendAi("Fehler: " + reply.Error + hint);
                        break;
                    }

                    if (reply.ToolsAccepted && reply.ToolCalls.Count > 0 && !model.SupportsTools)
                    {
                        model.SupportsTools = true;
                        AiStore.Save();
                    }

                    if (reply.ToolCalls.Count > 0)
                    {
                        _aiMessages.Add(new AiMessage
                        {
                            Role = "assistant",
                            Content = reply.Content,
                            ToolCalls = reply.ToolCalls
                        });
                        _chatStreamEntry = null;

                        foreach (var call in reply.ToolCalls)
                        {
                            var toolEntry = new ChatEntry
                            {
                                Id = NextChatId("tool"),
                                Role = "tool",
                                ToolName = call.Name,
                                Args = call.Arguments,
                                State = "running"
                            };
                            _chatTranscript.Add(toolEntry);
                            PostChat(new { type = "addToolCall", id = toolEntry.Id, toolName = call.Name, args = call.Arguments });

                            if (AiTools.IsEditTool(call.Name) && !SessionStore.Prefs.AiAutoApply && !_chatUnavailable)
                            {
                                toolEntry.State = "pending";
                                _aiPendingCall = call;
                                _aiPendingChatId = toolEntry.Id;
                                _aiPendingRound = round + 1;
                                PostChat(new { type = "updateToolStatus", id = toolEntry.Id, state = "pending" });
                                AppendAi("Editor-Aktion wartet auf Bestätigung: im Tool-Card „Bestätigen“ klicken.");
                                SetAiBusy(false);
                                AiInput.Focus();
                                return;
                            }

                            var result = await RunTool(call, toolEntry);
                            _aiMessages.Add(new AiMessage
                            {
                                Role = "tool",
                                ToolCallId = call.Id,
                                Content = result
                            });
                        }

                        TrimAiTranscript(provider, model);
                        continue;
                    }

                    string content = reply.Content ?? "";
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _chatTranscript.Remove(_chatStreamEntry);
                        AppendAi("KI: (leere Antwort)");
                        break;
                    }

                    if (!_chatStreamShown)
                        PostChat(new { type = "addMessage", id = _chatStreamEntry.Id, role = "assistant", content = content });
                    _aiMessages.Add(new AiMessage { Role = "assistant", Content = content });
                    _chatStreamEntry = null;

                    TrimAiTranscript(provider, model);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                FlushChatStream(true);
                if (_chatStreamEntry != null && _chatStreamEntry.Content.Length == 0)
                    _chatTranscript.Remove(_chatStreamEntry);
                AppendAi("Abgebrochen.");
            }
            finally
            {
                _chatStreamEntry = null;
                _chatFlush?.Stop();
                SetAiBusy(false);
                UpdateAiStatus();
                AiInput.Focus();
            }
        }

        static bool IsToolError(string result) =>
            string.IsNullOrEmpty(result) ||
            result.StartsWith("Kein aktives Dokument", StringComparison.Ordinal) ||
            result.StartsWith("Unbekanntes Tool", StringComparison.Ordinal) ||
            result.StartsWith("Kein Suchbegriff", StringComparison.Ordinal) ||
            result.StartsWith("Kein Code übergeben", StringComparison.Ordinal) ||
            result.StartsWith("Unbekannte Sprache", StringComparison.Ordinal) ||
            result.StartsWith("Formatfehler", StringComparison.Ordinal) ||
            result.StartsWith("Aktion fehlgeschlagen", StringComparison.Ordinal);

        async Task<string> RunTool(AiToolCall call, ChatEntry entry)
        {
            string result;
            bool isError;
            try
            {
                result = await AiTools.Execute(call.Name, call.Arguments, ActiveView);
                isError = IsToolError(result);
            }
            catch (Exception ex)
            {
                result = "Aktion fehlgeschlagen: " + ex.Message;
                isError = true;
            }
            entry.Result = result;
            entry.IsError = isError;
            entry.State = isError ? "error" : "done";
            if (string.Equals(call.Name, "set_language", StringComparison.OrdinalIgnoreCase))
            {
                UpdateLanguageChecks();
                UpdateStatus();
            }
            PostChat(new { type = "updateToolResult", id = entry.Id, result = result, isError = isError });
            if (_chatUnavailable) AppendAi($"→ {call.Name} ({AiTools.ShortArgs(call.Arguments)})\n← {result}");
            return result;
        }

        async Task ChatConfirmTool(string id)
        {
            if (_aiPendingCall == null || _aiPendingChatId != id) return;
            var call = _aiPendingCall;
            var chatId = _aiPendingChatId;
            _aiPendingCall = null;
            _aiPendingChatId = null;

            var entry = FindChatEntry(chatId);
            if (entry == null) return;
            var result = await RunTool(call, entry);
            _aiMessages.Add(new AiMessage { Role = "tool", ToolCallId = call.Id, Content = result });
            TrimAiTranscript(AiStore.ActiveProvider, AiStore.ActiveModel);
            UpdateStatus();

            _aiCts = new CancellationTokenSource();
            await AiRunAsync(_aiPendingRound);
        }

        void ChatDiscardTool(string id)
        {
            if (_aiPendingCall == null || _aiPendingChatId != id) return;
            _aiMessages.Add(new AiMessage
            {
                Role = "tool",
                ToolCallId = _aiPendingCall.Id,
                Content = "Aktion vom Nutzer abgelehnt."
            });
            _aiPendingCall = null;
            _aiPendingChatId = null;

            var entry = FindChatEntry(id);
            if (entry != null)
            {
                entry.Result = "Aktion vom Nutzer abgelehnt.";
                entry.IsError = false;
                entry.State = "dismissed";
            }
            PostChat(new { type = "updateToolResult", id = id, result = "Aktion vom Nutzer abgelehnt.", isError = false });
            PostChat(new { type = "updateToolStatus", id = id, state = "dismissed" });

            TrimAiTranscript(AiStore.ActiveProvider, AiStore.ActiveModel);
            AppendAi("Aktion verworfen.");
            AiInput.Focus();
        }

        void AiCancel_Click(object sender, RoutedEventArgs e) => _aiCts?.Cancel();

        void AiClear_Click(object sender, RoutedEventArgs e)
        {
            _aiMessages.Clear();
            _aiPendingCall = null;
            _aiPendingChatId = null;
            _contextRetried = false;
            _tokenShown = 0;
            _tokenMode = ContextBudget.ModeLabel(ContextBudget.KindFor(AiStore.ActiveProvider, AiStore.ActiveModel));
            ContextBudget.ResetCalibration(AiStore.ActiveProvider, AiStore.ActiveModel);
            _chatTranscript.Clear();
            _chatPending.Clear();
            _chatSeq = 0;
            PostChat(new { type = "clearChat" });
            AiLog.Clear();
            ClearAttachments();
            UpdateTokenBar();
            AiInput.Focus();
        }

        void AiFavoritesPopup_Click(object sender, RoutedEventArgs e)
        {
            var list = KiHistory.Favorites.ToList();
            FavoritesItems.ItemsSource = list;
            FavoritesEmptyHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AiFavoritesPopup.IsOpen = true;
        }

        void AiHistoryPopup_Click(object sender, RoutedEventArgs e)
        {
            var list = KiHistory.Entries.ToList();
            HistoryItems.ItemsSource = list;
            HistoryEmptyHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AiHistoryPopup.IsOpen = true;
        }

        void HistoryStar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is InputEntry entry)
            {
                KiHistory.ToggleFavorite(entry.Text);
                HistoryItems.ItemsSource = KiHistory.Entries.ToList();
            }
        }

        void PopupInsert_Click(object sender, MouseButtonEventArgs e)
        {
            string text = (sender as FrameworkElement)?.DataContext switch
            {
                InputEntry entry => entry.Text,
                Favorite favorite => favorite.Text,
                _ => null
            };
            if (string.IsNullOrEmpty(text)) return;
            AiInput.Text = text;
            AiInput.Focus();
            AiFavoritesPopup.IsOpen = false;
            AiHistoryPopup.IsOpen = false;
        }

        void AiHistoryClear_Click(object sender, RoutedEventArgs e)
        {
            KiHistory.Clear();
            HistoryItems.ItemsSource = KiHistory.Entries.ToList();
            HistoryEmptyHint.Visibility = Visibility.Visible;
        }

        void AiFavoritesManage_Click(object sender, RoutedEventArgs e)
        {
            if (_favDlg != null)
            {
                _favDlg.Activate();
                return;
            }
            _favDlg = new FavoritesDialog { Owner = this };
            var origin = AiPanel.PointToScreen(new Point(0, 0));
            _favDlg.WindowStartupLocation = WindowStartupLocation.Manual;
            _favDlg.Left = origin.X + Math.Max(0, (AiPanel.ActualWidth - _favDlg.Width) / 2);
            _favDlg.Top = origin.Y + Math.Max(0, (AiPanel.ActualHeight - _favDlg.Height) / 2);
            _favDlg.ShowDialog();
            _favDlg = null;
        }

        void AiInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift) return;
                e.Handled = true;
                _ = AiSendAsync();
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && PasteClipboardImage())
            {
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _aiBusy)
            {
                e.Handled = true;
                _aiCts?.Cancel();
            }
        }

        // ---------- Tastatur / Binding ----------

        void SetupBindings()
        {
            Bind(Commands.New, Key.T, ModifierKeys.Control, (s, e) => NewTab());
            Bind(Commands.Open, Key.O, ModifierKeys.Control, (s, e) => OpenTab());
            Bind(Commands.Save, Key.S, ModifierKeys.Control, (s, e) => ActiveView?.Save());
            Bind(Commands.SaveAs, Key.S, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => ActiveView?.SaveAsCore());
            Bind(Commands.Print, Key.P, ModifierKeys.Control, (s, e) => Print());
            Bind(Commands.Find, Key.F, ModifierKeys.Control, (s, e) => ShowSearch(false));
            Bind(Commands.FindNext, Key.F3, ModifierKeys.None, (s, e) => FindNext(false));
            Bind(Commands.FindPrev, Key.F3, ModifierKeys.Shift, (s, e) => FindNext(true));
            Bind(Commands.Replace, Key.H, ModifierKeys.Control, (s, e) => ShowSearch(true));
            Bind(Commands.Goto, Key.G, ModifierKeys.Control, (s, e) => ShowGoTo());
            Bind(Commands.DateTime, Key.F5, ModifierKeys.None, (s, e) => ActiveView?.InsertDateTime());
            Bind(Commands.ZoomIn, Key.OemPlus, ModifierKeys.Control, (s, e) => ZoomBy(10));
            Bind(Commands.ZoomIn, Key.Add, ModifierKeys.Control, (s, e) => ZoomBy(10));
            Bind(Commands.ZoomOut, Key.OemMinus, ModifierKeys.Control, (s, e) => ZoomBy(-10));
            Bind(Commands.ZoomOut, Key.Subtract, ModifierKeys.Control, (s, e) => ZoomBy(-10));
            Bind(Commands.ZoomReset, Key.D0, ModifierKeys.Control, (s, e) => ActiveView?.SetZoom(100));
            Bind(Commands.ZoomReset, Key.NumPad0, ModifierKeys.Control, (s, e) => ActiveView?.SetZoom(100));
            Bind(Commands.CloseTab, Key.W, ModifierKeys.Control, (s, e) => CloseTab(_active));
            Bind(Commands.RestoreTab, Key.T, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => RestoreLastClosed());
            Bind(Commands.NextTab, Key.Tab, ModifierKeys.Control, (s, e) => SwitchTab(1));
            Bind(Commands.PrevTab, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => SwitchTab(-1));
            Bind(Commands.Detach, Key.D, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => DetachActive());
            Bind(Commands.CloseAll, Key.W, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => CloseAllTabs());
            Bind(Commands.FormatJson, Key.J, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => JsonFormat());
            Bind(Commands.FormatCode, Key.F, ModifierKeys.Control | ModifierKeys.Shift, (s, e) => FormatCode());
            Bind(Commands.PythonCompletion, Key.Space, ModifierKeys.Control, (s, e) => PythonCompletion());
            Bind(Commands.NextSyntaxError, Key.F4, ModifierKeys.None, (s, e) => NextSyntaxError());
            Bind(Commands.AiToggle, Key.K, ModifierKeys.Control | ModifierKeys.Alt, (s, e) => ToggleAiPanel());
        }

        void Bind(RoutedCommand cmd, Key key, ModifierKeys mod, ExecutedRoutedEventHandler handler)
        {
            InputBindings.Add(new KeyBinding(cmd, key, mod));
            CommandBindings.Add(new CommandBinding(cmd, handler));
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool inAiInput = AiInput != null && Keyboard.FocusedElement == AiInput;
            if (inAiInput && Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.Z || e.Key == Key.Y)) return;
            if (inAiInput && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z) return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                ActiveView?.DoUndo();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y ||
                Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z)
            {
                ActiveView?.DoRedo();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            int idx = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 0,
                Key.D2 or Key.NumPad2 => 1,
                Key.D3 or Key.NumPad3 => 2,
                Key.D4 or Key.NumPad4 => 3,
                Key.D5 or Key.NumPad5 => 4,
                Key.D6 or Key.NumPad6 => 5,
                Key.D7 or Key.NumPad7 => 6,
                Key.D8 or Key.NumPad8 => 7,
                Key.D9 or Key.NumPad9 => Tabs.Count - 1,
                _ => -1
            };
            if (idx >= 0 && idx < Tabs.Count)
            {
                TabStrip.SelectedItem = Tabs[idx];
                e.Handled = true;
            }
        }

        // ---------- Datei-Drag & Drop ----------

        void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                Array.Exists(files, File.Exists))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                e.Handled = true;
                foreach (var f in files)
                    if (File.Exists(f)) OpenInNewTab(f);
            }
        }

        // ---------- Tab-Drag (Detach/Merge) ----------

        void TabStrip_Down(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(TabStrip);
            _dragTab = TabFromSource(e.OriginalSource as DependencyObject);
        }

        void TabStrip_Move(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStart == null) return;
            var p = e.GetPosition(TabStrip);
            if (Math.Abs(p.X - _dragStart.Value.X) < 8 && Math.Abs(p.Y - _dragStart.Value.Y) < 8) return;
            var tab = _dragTab;
            _dragStart = null;
            if (tab == null) return;
            DragDrop.DoDragDrop(TabStrip, new DataObject(ViewDataFormat, tab.View), DragDropEffects.Move);
        }

        static TabModel TabFromSource(DependencyObject d)
        {
            while (d != null)
            {
                if (d is ListBoxItem li && li.DataContext is TabModel t) return t;
                DependencyObject parent = d is Visual
                    ? VisualTreeHelper.GetParent(d)
                    : LogicalTreeHelper.GetParent(d);
                d = parent;
            }
            return null;
        }

        void TabStrip_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(ViewDataFormat)
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        void TabStrip_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (!e.Data.GetDataPresent(ViewDataFormat)) return;
            var view = e.Data.GetData(ViewDataFormat) as DocumentView;
            if (view == null) return;
            var source = Application.Current.Windows.OfType<MainWindow>()
                .FirstOrDefault(w => w.FindTab(view) != null);
            if (source == null || source == this) return;
            var tab = source.FindTab(view);
            source.RemoveTab(tab, false);
            if (source.Tabs.Count == 0) source.AddView(source.NewView());
            int dropIndex = GetDropIndex(e);
            view.TransferOut();
            AddView(view, dropIndex);
            SaveSessionNow();
        }

        int GetDropIndex(DragEventArgs e)
        {
            var p = e.GetPosition(TabStrip);
            for (int i = 0; i < Tabs.Count; i++)
            {
                var c = TabStrip.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (c == null) continue;
                var b = c.TranslatePoint(new Point(0, 0), TabStrip);
                if (p.X < b.X + c.ActualWidth / 2) return i;
            }
            return Tabs.Count;
        }

        // ---------- Drucken & Hilfe ----------

        void PageSetup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PageSetupDialogWin(LastPrinterName ?? "Standarddrucker", MargL, MargT, MargR, MargB) { Owner = this };
            dlg.ShowDialog();
            ActiveView?.FocusEditor();
        }

        void Print_Click(object sender, RoutedEventArgs e) => Print();

        public void SetPrintMargins(double l, double t, double r, double b)
        {
            MargL = l; MargT = t; MargR = r; MargB = b;
        }

        void Print()
        {
            var v = ActiveView;
            if (v == null) return;
            v.Print(MargL, MargT, MargR, MargB, this, out string printer);
            if (printer != null) LastPrinterName = printer;
        }

        void UpdateAuto_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.UpdateCheck = UpdateAutoItem.IsChecked;
            SessionStore.SavePrefs();
        }

        async void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            await UpdateService.CheckAsync(this, true);
        }

        void HelpAbout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutDialog { Owner = this };
            dlg.ShowDialog();
            ActiveView?.FocusEditor();
        }
    }

    public class TabModel : INotifyPropertyChanged
    {
        public DocumentView View { get; }

        string _title = "Unbenannt";
        string _tip = "Unbenannt";
        double _width = 220;

        public string Title
        {
            get => _title;
            set { if (_title == value) return; _title = value; Raise(nameof(Title)); }
        }

        public string Tip
        {
            get => _tip;
            set { if (_tip == value) return; _tip = value; Raise(nameof(Tip)); }
        }

        public double Width
        {
            get => _width;
            set { if (_width == value) return; _width = value; Raise(nameof(Width)); }
        }

        public TabModel(DocumentView view)
        {
            View = view;
            RefreshTitle();
        }

        public void RefreshTitle()
        {
            Title = View.TabTitle;
            Tip = View.Path_ ?? "Unbenannt";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Raise(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ClosedDoc
    {
        public string Path;
        public string Text;
        public NovaNoteEncoding Enc;
        public LineEnding Eol;
        public int Zoom;
        public bool Wrap;
        public bool Dirty;
        public DocLanguage Language;

        public ClosedDoc(string path, string text, NovaNoteEncoding enc, LineEnding eol,
                         int zoom, bool wrap, bool dirty, DocLanguage language)
        {
            Path = path; Text = text; Enc = enc; Eol = eol; Zoom = zoom; Wrap = wrap;
            Dirty = dirty; Language = language;
        }
    }

    static class Commands
    {
        public static readonly RoutedCommand New = new();
        public static readonly RoutedCommand Open = new();
        public static readonly RoutedCommand Save = new();
        public static readonly RoutedCommand SaveAs = new();
        public static readonly RoutedCommand Print = new();
        public static readonly RoutedCommand Find = new();
        public static readonly RoutedCommand FindNext = new();
        public static readonly RoutedCommand FindPrev = new();
        public static readonly RoutedCommand Replace = new();
        public static readonly RoutedCommand Goto = new();
        public static readonly RoutedCommand DateTime = new();
        public static readonly RoutedCommand ZoomIn = new();
        public static readonly RoutedCommand ZoomOut = new();
        public static readonly RoutedCommand ZoomReset = new();
        public static readonly RoutedCommand CloseTab = new();
        public static readonly RoutedCommand RestoreTab = new();
        public static readonly RoutedCommand NextTab = new();
        public static readonly RoutedCommand PrevTab = new();
        public static readonly RoutedCommand Detach = new();
        public static readonly RoutedCommand CloseAll = new();
        public static readonly RoutedCommand FormatJson = new();
        public static readonly RoutedCommand FormatCode = new();
        public static readonly RoutedCommand PythonCompletion = new();
        public static readonly RoutedCommand NextSyntaxError = new();
        public static readonly RoutedCommand AiToggle = new();
    }
}
