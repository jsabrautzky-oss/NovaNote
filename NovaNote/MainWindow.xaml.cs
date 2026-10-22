using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        Point? _dragStart;
        TabModel _dragTab;
        bool _closingForUpdate;

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
            Closed += (s, e) => ThemeService.ThemeChanged -= OnThemeChanged;
            SetupBindings();
            TabStrip.ItemsSource = Tabs;
            TabStrip.SizeChanged += (s, e) => UpdateTabWidths();

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
                Filter = "Textdokumente (*.txt)|*.txt|Markdown (*.md)|*.md|Python (*.py)|*.py|C# (*.cs)|*.cs|YAML (*.yaml)|*.yaml;*.yml|Alle Dateien (*.*)|*.*",
                FilterIndex = 1
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
            if (!tab.View.PromptDirty()) return;
            PushHistory(tab);
            RemoveTab(tab);
            if (Tabs.Count == 0) AddView(NewView());
        }

        void CloseAllTabs()
        {
            var tabs = Tabs.ToList();
            foreach (var tab in tabs)
                if (!tab.View.PromptDirty()) return;
            foreach (var tab in tabs)
            {
                PushHistory(tab);
                RemoveTab(tab);
            }
            if (Tabs.Count == 0) AddView(NewView());
            else TabStrip.SelectedItem = Tabs[0];
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
            StatusLanguage.Text = v.StatusLanguage;
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
            LangJsonFormatItem.IsEnabled = lang == DocLanguage.Json;
            CodeFormatItem.IsEnabled = ActiveView?.FormatSupported == true;
            PythonCompletionItem.IsEnabled = ActiveView?.PythonLspReady == true;
        }

        void UpdateMenuChecks()
        {
            var v = ActiveView;
            TabModeItem.IsChecked = SessionStore.Prefs.MultiTab;
            SessionRestoreItem.IsChecked = SessionStore.Prefs.RestoreSession;
            TabRestoreItem.IsEnabled = ClosedHistory.Count > 0;
            TabDetachItem.IsEnabled = SessionStore.Prefs.MultiTab;
            FormatWrapItem.IsChecked = v?.WordWrap == true;
            ViewLineNumbersItem.IsChecked = SessionStore.Prefs.ShowLineNumbers;
            ViewWhitespaceItem.IsChecked = SessionStore.Prefs.ShowWhitespace;
            SyntaxCheckItem.IsChecked = SessionStore.Prefs.SyntaxCheck;
            FoldingItem.IsChecked = SessionStore.Prefs.Folding;
            SyntaxNextItem.IsEnabled = ActiveView?.HasSyntaxErrors == true;
            UpdateAutoItem.IsChecked = SessionStore.Prefs.UpdateCheck;
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
        }

        void UpdateEolChecks()
        {
            var v = ActiveView;
            EolCrLfItem.IsChecked = v?.Eol == LineEnding.CrLf;
            EolLfItem.IsChecked = v?.Eol == LineEnding.Lf;
            EolCrItem.IsChecked = v?.Eol == LineEnding.Cr;
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
            _sessionDirty = false;
        }

        public bool SaveSessionForUpdate()
        {
            foreach (var tab in Tabs.ToList())
            {
                if (!tab.View.PromptDirty())
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
                return;
            }

            var tabs = Tabs.ToList();
            if (SessionStore.Prefs.MultiTab && tabs.Count >= 2)
            {
                var r = MessageBox.Show(this,
                    $"NovaNote schließen? Es sind {tabs.Count} Tabs geöffnet.\n\n" +
                    "Möchten Sie alle Tabs schließen und das Fenster schließen?",
                    "Alle Tabs schließen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            foreach (var tab in tabs)
            {
                if (!tab.View.PromptDirty())
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
                ActiveView?.SetLanguage(lang);
                UpdateLanguageChecks();
                UpdateStatus();
            }
        }

        void LangAuto_Click(object sender, RoutedEventArgs e)
        {
            SessionStore.Prefs.LanguageAuto = LangAutoItem.IsChecked;
            SessionStore.SavePrefs();
            if (SessionStore.Prefs.LanguageAuto) ActiveView?.DetectLanguage();
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
                    if (!tab.View.PromptDirty())
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
        }

        void Bind(RoutedCommand cmd, Key key, ModifierKeys mod, ExecutedRoutedEventHandler handler)
        {
            InputBindings.Add(new KeyBinding(cmd, key, mod));
            CommandBindings.Add(new CommandBinding(cmd, handler));
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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
    }
}
