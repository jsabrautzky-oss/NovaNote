using System;
using System.Collections.Generic;
using System.Globalization;
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
            SamplerPresetBox.ItemsSource = new[]
            {
                new SamplerPreset("Präzises Coding & Logik", 0.1, 0.90, 0.05, 1.0),
                new SamplerPreset("Ausgewogener Standard-Chat", 0.7, 0.95, 0.05, 1.1),
                new SamplerPreset("Kreatives Schreiben / RP", 1.1, 1.00, 0.10, 1.15),
                new SamplerPreset("Alle leer (Server-Standard)", null, null, null, null),
            };
            SamplerPresetBox.SelectedIndex = -1;
            ReasoningBox.ItemsSource = AiEffort.ModelOptions;

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

        void RefreshModels(string focusKey = null)
        {
            var selectedId = (ModelList.SelectedItem as AiModelRow)?.Id;
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
            if (selectedId != null)
            {
                var again = rows.FirstOrDefault(r => r.Id == selectedId);
                if (again != null) ModelList.SelectedItem = again;
            }
            UpdateButtons();
            RestoreEditorFocus(focusKey);
        }

        void RestoreEditorFocus(string focusKey)
        {
            TextBox box = focusKey switch
            {
                "temp" => TempBox,
                "topp" => TopPBox,
                "minp" => MinPBox,
                "repeat" => RepeatBox,
                "ctx" => ContextBox,
                _ => null,
            };
            if (box == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                box.Focus();
                box.CaretIndex = box.Text.Length;
            }));
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
            TempBox.Text = SamplerNum(model?.Temperature);
            TempBox.IsEnabled = has;
            TopPBox.Text = SamplerNum(model?.TopP);
            TopPBox.IsEnabled = has;
            MinPBox.Text = SamplerNum(model?.MinP);
            MinPBox.IsEnabled = has;
            RepeatBox.Text = SamplerNum(model?.RepeatPenalty);
            RepeatBox.IsEnabled = has;
            SamplerPresetBox.IsEnabled = has;
            SamplerPresetBox.SelectedIndex = -1;
            ReasoningBox.SelectedItem = AiEffort.Find(AiEffort.ModelOptions, model?.ReasoningEffort);
            ReasoningBox.IsEnabled = has;
            _loading = false;
        }

        void CommitContext() => CommitContextCore("ctx");

        void CommitContextCore(string focusKey)
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
            RefreshModels(focusKey);
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

        static string SamplerNum(double? v) => v.HasValue ? v.Value.ToString(CultureInfo.CurrentCulture) : "";

        static bool TryParseSampler(string text, out double value)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) { value = 0; return false; }
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        void CommitSampling() => CommitSamplingCore(null);

        void CommitSamplingCore(string focusKey)
        {
            if (_loading || _current == null) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;

            var fields = new (TextBox Box, string Label, double Min, double Max, Action<AiModel, double?> Set)[]
            {
                (TempBox, "Temp", 0, 2, (m, v) => m.Temperature = v),
                (TopPBox, "Top-P", 0, 1, (m, v) => m.TopP = v),
                (MinPBox, "Min-P", 0, 1, (m, v) => m.MinP = v),
                (RepeatBox, "Repeat Penalty", 1, 2, (m, v) => m.RepeatPenalty = v),
            };

            var parsed = new double?[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f.Box.Text.Trim().Length == 0) { parsed[i] = null; continue; }
                if (!TryParseSampler(f.Box.Text, out double v) || v < f.Min || v > f.Max)
                {
                    StatusText.Text = $"{f.Label} muss eine Zahl zwischen {f.Min} und {f.Max} sein (leer = Server-Standard).";
                    return;
                }
                parsed[i] = v;
            }

            for (int i = 0; i < fields.Length; i++) fields[i].Set(model, parsed[i]);
            RefreshModels(focusKey);
            StatusText.Text = $"Sampler für \"{row.Name}\" gesetzt.";
        }

        string SamplerFocusKey(object sender)
        {
            if (ReferenceEquals(sender, TempBox)) return "temp";
            if (ReferenceEquals(sender, TopPBox)) return "topp";
            if (ReferenceEquals(sender, MinPBox)) return "minp";
            if (ReferenceEquals(sender, RepeatBox)) return "repeat";
            return null;
        }

        void Sampling_LostFocus(object sender, RoutedEventArgs e) => CommitSamplingCore(SamplerFocusKey(sender));

        void Sampling_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitSamplingCore(SamplerFocusKey(sender));
                e.Handled = true;
            }
        }

        sealed class SamplerPreset
        {
            public string Label { get; }
            public double? Temp { get; }
            public double? TopP { get; }
            public double? MinP { get; }
            public double? Repeat { get; }
            public SamplerPreset(string label, double? temp, double? topP, double? minP, double? repeat)
            {
                Label = label; Temp = temp; TopP = topP; MinP = minP; Repeat = repeat;
            }
            public override string ToString() => Label;
        }

        void SamplerPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _presetBusy || _current == null) return;
            ApplySamplerPreset(SamplerPresetBox.SelectedItem as SamplerPreset);
        }

        bool _presetBusy;

        void ApplySamplerPreset(SamplerPreset preset)
        {
            if (preset == null || _loading) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            TempBox.Text = SamplerNum(preset.Temp);
            TopPBox.Text = SamplerNum(preset.TopP);
            MinPBox.Text = SamplerNum(preset.MinP);
            RepeatBox.Text = SamplerNum(preset.Repeat);
            CommitSamplingCore(null);
            StatusText.Text = preset.Temp == null
                ? $"Sampler für \"{row.Name}\" geleert – Server-/Modell-Standard."
                : $"Sampler für \"{row.Name}\" auf Vorlage \"{preset.Label}\" gesetzt " +
                  $"({SamplerNum(preset.Temp)} / {SamplerNum(preset.TopP)} / {SamplerNum(preset.MinP)} / {SamplerNum(preset.Repeat)}).";
            _presetBusy = true;
            SamplerPresetBox.SelectedIndex = -1;
            _presetBusy = false;
        }

        void ReasoningBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _current == null) return;
            var row = ModelList.SelectedItem as AiModelRow;
            if (row == null) return;
            var model = _current.Models.FirstOrDefault(m => m.Id == row.Id);
            if (model == null) return;
            model.ReasoningEffort = (ReasoningBox.SelectedItem as AiEffort.Option)?.Value;
            StatusText.Text = model.ReasoningEffort == null
                ? $"Denken für \"{row.Name}\": Server-Standard – NovaNote sendet kein reasoning_effort."
                : $"Denken für \"{row.Name}\": reasoning_effort = \"{model.ReasoningEffort}\".";
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
            SessionStore.SavePrefs();
            DialogResult = true;
        }
    }
}
