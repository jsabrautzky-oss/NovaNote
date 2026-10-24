using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NovaNote
{
    public partial class AiSettingsDialog : Window
    {
        readonly List<AiProvider> _providers;
        AiProvider _current;
        bool _loading;

        public AiSettingsDialog()
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);

            _providers = AiStore.State.Providers.Select(p => p.Clone()).ToList();
            KindBox.ItemsSource = new[] { "LM Studio", "Ollama", "OpenAI-kompatibel", "Eigener Anbieter" };
            AutoApplyBox.IsChecked = SessionStore.Prefs.AiAutoApply;

            ProviderList.ItemsSource = _providers;
            _current = _providers.FirstOrDefault(p => p.Id == AiStore.State.ActiveProviderId) ?? _providers.FirstOrDefault();
            ProviderList.SelectedItem = _current;
            LoadProviderFields();
            RefreshModels();
        }

        public class AiModelRow
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string Context { get; set; }
            public string Vision { get; set; }
            public string Tools { get; set; }
            public bool Active { get; set; }
            public bool Missing { get; set; }
        }

        void ProviderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _current = ProviderList.SelectedItem as AiProvider;
            LoadProviderFields();
            RefreshModels();
        }

        void LoadProviderFields()
        {
            _loading = true;
            NameBox.Text = _current?.Name ?? "";
            UrlBox.Text = _current?.BaseUrl ?? "";
            KindBox.SelectedItem = _current == null ? null : AiStore.KindLabel(_current.Kind);
            SetKey(_current?.ApiKey ?? "");
            _loading = false;
        }

        void SetKey(string value)
        {
            _loading = true;
            if (ShowKeyBox.IsChecked == true) KeyTextBox.Text = value;
            else KeyBox.Password = value;
            _loading = false;
        }

        void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _current == null) return;
            _current.Name = NameBox.Text;
            ProviderList.Items.Refresh();
        }

        void UrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _current == null) return;
            _current.BaseUrl = UrlBox.Text.Trim();
            ProviderList.Items.Refresh();
        }

        void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _current == null) return;
            _current.Kind = AiStore.KindFromLabel(KindBox.SelectedItem as string);
            if (string.IsNullOrWhiteSpace(_current.BaseUrl))
            {
                _current.BaseUrl = AiStore.PresetUrl(_current.Kind);
                _loading = true;
                UrlBox.Text = _current.BaseUrl;
                _loading = false;
            }
        }

        void KeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _loading = true;
            if (_current != null) _current.ApiKey = KeyBox.Password;
            KeyTextBox.Text = KeyBox.Password;
            _loading = false;
        }

        void KeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            _loading = true;
            if (_current != null) _current.ApiKey = KeyTextBox.Text;
            KeyBox.Password = KeyTextBox.Text;
            _loading = false;
        }

        void ShowKey_Changed(object sender, RoutedEventArgs e)
        {
            bool show = ShowKeyBox.IsChecked == true;
            KeyTextBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            KeyBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            SetKey(_current?.ApiKey ?? "");
        }

        void RefreshModels()
        {
            var rows = new List<AiModelRow>();
            if (_current != null)
            {
                foreach (var m in _current.Models)
                {
                    rows.Add(new AiModelRow
                    {
                        Name = m.DisplayName,
                        Id = m.Id,
                        Context = m.ContextOverride.HasValue
                            ? $"{m.EffectiveContext} (manuell)"
                            : (m.ContextDetected ? m.EffectiveContext.ToString() : $"{m.EffectiveContext} (Annahme)"),
                        Vision = m.VisionOverride.HasValue
                            ? (m.VisionEnabled ? "Ja (manuell)" : "Nein (manuell)")
                            : (m.Vision ? "Ja" : "Nein"),
                        Tools = m.SupportsTools ? "Ja" : "Nein",
                        Active = string.Equals(m.Id, _current.ActiveModelId, StringComparison.OrdinalIgnoreCase),
                        Missing = !m.Available
                    });
                }
            }
            ModelList.ItemsSource = rows;
            UpdateButtons();
        }

        void ModelList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

        void UpdateButtons()
        {
            var row = ModelList.SelectedItem as AiModelRow;
            bool has = row != null && _current != null;
            SetActiveButton.IsEnabled = has;
            RemoveButton.IsEnabled = has;
            var model = _current?.Models.FirstOrDefault(m => m.Id == row?.Id);
            _loading = true;
            VisionBox.IsChecked = model?.VisionOverride == true;
            VisionBox.IsEnabled = has;
            ContextBox.Text = model == null ? "" : model.EffectiveContext.ToString();
            ContextBox.IsEnabled = has;
            ContextManualBox.IsChecked = model?.ContextOverride.HasValue == true;
            ContextManualBox.IsEnabled = has;
            _loading = false;
        }

        void CommitContext()
        {
            if (_loading || _current == null) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;
            if (!int.TryParse(ContextBox.Text.Trim(), out int ctx) || ctx < 256)
            {
                StatusText.Text = "Kontext muss eine Zahl ab 256 Tokens sein.";
                return;
            }
            if (ContextManualBox.IsChecked == true)
            {
                model.ContextOverride = ctx;
                StatusText.Text = $"Kontext für \"{row.Name}\" auf {ctx} Tokens festgelegt (Scan überschreibt ihn nicht).";
            }
            else
            {
                model.ContextOverride = null;
                model.ContextLength = ctx;
                StatusText.Text = $"Kontext für \"{row.Name}\" auf {ctx} Tokens gesetzt — ein Scan kann den Wert ersetzen.";
            }
            RefreshModels();
        }

        void ContextBox_LostFocus(object sender, RoutedEventArgs e) => CommitContext();

        void ContextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitContext();
                e.Handled = true;
            }
        }

        void ContextManual_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _current == null) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;
            if (ContextManualBox.IsChecked == true)
            {
                if (int.TryParse(ContextBox.Text.Trim(), out int ctx) && ctx >= 256)
                {
                    model.ContextOverride = ctx;
                    StatusText.Text = $"Kontext für \"{row.Name}\" manuell auf {ctx} Tokens festgelegt.";
                }
                else
                {
                    model.ContextOverride = model.ContextLength;
                    StatusText.Text = $"Kontext für \"{row.Name}\" manuell auf {model.ContextLength} Tokens festgelegt.";
                }
            }
            else
            {
                model.ContextOverride = null;
                StatusText.Text = $"Kontext für \"{row.Name}\" folgt wieder der Erkennung ({model.ContextLength} Tokens).";
            }
            RefreshModels();
        }

        void VisionBox_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _current == null) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;
            model.VisionOverride = VisionBox.IsChecked == true ? true : (bool?)null;
            RefreshModels();
            StatusText.Text = VisionBox.IsChecked == true
                ? $"Vision für \"{row.Name}\" erzwungen — Bilder im Chat erlaubt."
                : $"Vision für \"{row.Name}\" folgt wieder der Erkennung (Metadaten oder Modellname).";
        }

        async void Scan_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                StatusText.Text = "Kein Anbieter ausgewählt.";
                return;
            }

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            StatusText.Text = $"Suche Modelle bei {_current.Name} ({_current.BaseUrl})…";

            var result = await LocalAiClient.ScanAsync(_current, CancellationToken.None);

            if (button != null) button.IsEnabled = true;
            if (result.Error != null)
            {
                StatusText.Text = result.Error;
                return;
            }

            int added = AiStore.MergeScanned(_current, result.Models);
            RefreshModels();
            StatusText.Text = $"{result.Models.Count} Modelle gefunden, {added} neue hinzugefügt. " +
                             $"Aktives Modell: {_current.ActiveModel?.DisplayName ?? "keins"}.";
        }

        void SetActive_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || !(ModelList.SelectedItem is AiModelRow row)) return;
            _current.ActiveModelId = row.Id;
            RefreshModels();
            StatusText.Text = $"Aktives Modell: {row.Name} ({row.Id}).";
        }

        void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || !(ModelList.SelectedItem is AiModelRow row)) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;
            _current.Models.Remove(model);
            if (string.Equals(_current.ActiveModelId, row.Id, StringComparison.OrdinalIgnoreCase))
                _current.ActiveModelId = _current.Models.FirstOrDefault()?.Id;
            RefreshModels();
            StatusText.Text = $"Modell \"{row.Name}\" entfernt.";
        }

        void AddManual_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                StatusText.Text = "Kein Anbieter ausgewählt.";
                return;
            }
            string id = NewIdBox.Text.Trim();
            if (string.IsNullOrEmpty(id))
            {
                StatusText.Text = "Modell-ID fehlt.";
                return;
            }
            if (_current.Models.Any(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText.Text = $"Modell \"{id}\" ist bereits erfasst.";
                return;
            }
            int ctx = int.TryParse(NewContextBox.Text.Trim(), out int c) ? Math.Max(256, c) : 4096;
            _current.Models.Add(new AiModel
            {
                Id = id,
                Name = NewNameBox.Text.Trim(),
                ContextLength = ctx,
                ContextOverride = ctx,
                Vision = NewVisionBox.IsChecked == true,
                VisionOverride = NewVisionBox.IsChecked == true ? true : (bool?)null,
                SupportsTools = NewToolsBox.IsChecked == true,
                Manual = true,
                Available = true
            });
            if (string.IsNullOrEmpty(_current.ActiveModelId)) _current.ActiveModelId = id;
            NewIdBox.Clear();
            NewNameBox.Clear();
            NewContextBox.Clear();
            RefreshModels();
            StatusText.Text = $"Modell \"{id}\" manuell hinzugefügt.";
        }

        void NewProvider_Click(object sender, RoutedEventArgs e)
        {
            var kind = AiStore.KindFromLabel(KindBox.SelectedItem as string);
            var provider = new AiProvider
            {
                Id = AiStore.NewId(kind.ToString().ToLowerInvariant()),
                Name = "Neuer Anbieter",
                Kind = kind,
                BaseUrl = AiStore.PresetUrl(kind)
            };
            _providers.Add(provider);
            ProviderList.ItemsSource = _providers;
            ProviderList.SelectedItem = provider;
            NameBox.Focus();
            StatusText.Text = "Neuer Anbieter angelegt. Name und Adresse anpassen, dann Modelle scannen.";
        }

        void DeleteProvider_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            _providers.Remove(_current);
            ProviderList.ItemsSource = _providers;
            ProviderList.SelectedItem = _providers.FirstOrDefault();
            StatusText.Text = _providers.Count == 0
                ? "Kein Anbieter mehr vorhanden."
                : $"Anbieter \"{_current.Name}\" gelöscht.";
        }

        void Ok_Click(object sender, RoutedEventArgs e)
        {
            var active = ProviderList.SelectedItem as AiProvider ?? _providers.FirstOrDefault();
            AiStore.ReplaceState(new AiState
            {
                Providers = _providers,
                ActiveProviderId = active?.Id
            });
            SessionStore.Prefs.AiAutoApply = AutoApplyBox.IsChecked == true;
            SessionStore.SavePrefs();
            DialogResult = true;
        }
    }
}
