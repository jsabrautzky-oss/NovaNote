using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaNote
{
    public enum AiKind
    {
        LmStudio,
        Ollama,
        OpenAiCompatible,
        Custom
    }

    public class AiModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int ContextLength { get; set; } = ContextBudget.FallbackContext;
        public int? ContextOverride { get; set; }
        public bool Vision { get; set; }
        public bool? VisionOverride { get; set; }
        public bool SupportsTools { get; set; }
        public bool Manual { get; set; }
        public bool Available { get; set; } = true;

        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public double? MinP { get; set; }
        public double? RepeatPenalty { get; set; }
        public string ReasoningEffort { get; set; } = null;

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        [JsonIgnore]
        public bool VisionEnabled => VisionOverride ?? Vision;

        [JsonIgnore]
        public int EffectiveContext => ContextOverride ?? ContextLength;

        [JsonIgnore]
        public bool ContextDetected => ContextOverride == null && ContextLength != ContextBudget.FallbackContext;

        public AiModel Clone() => new AiModel        {
            Id = Id,
            Name = Name,
            ContextLength = ContextLength,
            ContextOverride = ContextOverride,
            Vision = Vision,
            VisionOverride = VisionOverride,
            SupportsTools = SupportsTools,
            Manual = Manual,
            Available = Available,
            Temperature = Temperature,
            TopP = TopP,
            MinP = MinP,
            RepeatPenalty = RepeatPenalty,
            ReasoningEffort = ReasoningEffort
        };
    }

    public class AiProvider
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public AiKind Kind { get; set; } = AiKind.LmStudio;
        public string BaseUrl { get; set; } = LocalAiClient.DefaultBaseUrl;
        public string ApiKeyEnc { get; set; } = null;
        public List<AiModel> Models { get; set; } = new List<AiModel>();
        public string ActiveModelId { get; set; } = null;

        [JsonIgnore]
        public string ApiKey
        {
            get => AiStore.Unprotect(ApiKeyEnc);
            set { ApiKeyEnc = string.IsNullOrEmpty(value) ? null : AiStore.Protect(value); }
        }

        [JsonIgnore]
        public AiModel ActiveModel
        {
            get
            {
                var chosen = Models.FirstOrDefault(m => m.Id == ActiveModelId);
                if (chosen != null) return chosen;
                return Models.FirstOrDefault(m => m.Available) ?? Models.FirstOrDefault();
            }
        }

        [JsonIgnore]
        public string KindName => Kind switch
        {
            AiKind.LmStudio => "LM Studio",
            AiKind.Ollama => "Ollama",
            AiKind.OpenAiCompatible => "OpenAI-kompatibel",
            _ => "Eigene"
        };

        public AiProvider Clone() => new AiProvider
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            BaseUrl = BaseUrl,
            ApiKeyEnc = ApiKeyEnc,
            ActiveModelId = ActiveModelId,
            Models = Models.Select(m => m.Clone()).ToList()
        };
    }

    public class AiState
    {
        public List<AiProvider> Providers { get; set; } = new List<AiProvider>();
        public string ActiveProviderId { get; set; } = null;

        [JsonIgnore]
        public AiProvider ActiveProvider =>
            Providers.FirstOrDefault(p => p.Id == ActiveProviderId) ?? Providers.FirstOrDefault();
    }

    public static class AiStore
    {
        static string Dir => SessionStore.DataDir;

        static string StatePath => Path.Combine(Dir, "ki.json");

        public static AiState State { get; private set; } = CreateDefault();

        public static event Action Changed;

        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { WriteIndented = true };

        public static AiState CreateDefault()
        {
            var lm = new AiProvider
            {
                Id = "lmstudio",
                Name = "LM Studio",
                Kind = AiKind.LmStudio,
                BaseUrl = LocalAiClient.DefaultBaseUrl
            };
            var ollama = new AiProvider
            {
                Id = "ollama",
                Name = "Ollama",
                Kind = AiKind.Ollama,
                BaseUrl = LocalAiClient.OllamaBaseUrl
            };
            return new AiState
            {
                Providers = new List<AiProvider> { lm, ollama },
                ActiveProviderId = lm.Id
            };
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(StatePath))
                {
                    var st = JsonSerializer.Deserialize<AiState>(File.ReadAllText(StatePath));
                    if (st != null && st.Providers.Count > 0) State = st;
                }
            }
            catch
            {
                State = CreateDefault();
            }
            if (State.ActiveProvider == null) State.ActiveProviderId = State.Providers.FirstOrDefault()?.Id;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(StatePath, JsonSerializer.Serialize(State, Opt));
                Changed?.Invoke();
            }
            catch { }
        }

        public static void ReplaceState(AiState state)
        {
            State = state ?? CreateDefault();
            Save();
        }

        public static AiProvider ActiveProvider => State.ActiveProvider;

        public static AiModel ActiveModel => ActiveProvider?.ActiveModel;

        public static bool SupportsVision(AiModel model) => model != null && model.VisionEnabled;

        public static string NewId(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static AiProvider AddProvider(string name, AiKind kind, string url)
        {
            var p = new AiProvider
            {
                Id = NewId(kind.ToString().ToLowerInvariant()),
                Name = string.IsNullOrWhiteSpace(name) ? KindLabel(kind) : name.Trim(),
                Kind = kind,
                BaseUrl = string.IsNullOrWhiteSpace(url) ? PresetUrl(kind) : url.Trim()
            };
            State.Providers.Add(p);
            State.ActiveProviderId = p.Id;
            Save();
            return p;
        }

        public static void RemoveProvider(AiProvider provider)
        {
            if (provider == null) return;
            State.Providers.Remove(provider);
            if (State.ActiveProviderId == provider.Id)
                State.ActiveProviderId = State.Providers.FirstOrDefault()?.Id;
            Save();
        }

        public static void SetActiveProvider(AiProvider provider)
        {
            if (provider == null) return;
            State.ActiveProviderId = provider.Id;
            Save();
        }

        public static void SetActiveModel(AiProvider provider, string modelId)
        {
            if (provider == null) return;
            provider.ActiveModelId = modelId;
            Save();
        }

        public static string PresetUrl(AiKind kind) => kind switch
        {
            AiKind.LmStudio => LocalAiClient.DefaultBaseUrl,
            AiKind.Ollama => LocalAiClient.OllamaBaseUrl,
            _ => LocalAiClient.DefaultBaseUrl
        };

        public static string KindLabel(AiKind kind) => kind switch
        {
            AiKind.LmStudio => "LM Studio",
            AiKind.Ollama => "Ollama",
            AiKind.OpenAiCompatible => "OpenAI-kompatibel",
            _ => "Eigener Anbieter"
        };

        public static AiKind KindFromLabel(string label) => label switch
        {
            "Ollama" => AiKind.Ollama,
            "OpenAI-kompatibel" => AiKind.OpenAiCompatible,
            "Eigener Anbieter" => AiKind.Custom,
            _ => AiKind.LmStudio
        };

        public static int MergeScanned(AiProvider provider, List<AiModelInfo> scanned)
        {
            if (provider == null || scanned == null) return 0;
            int added = 0;
            foreach (var info in scanned)
            {
                var existing = provider.Models.FirstOrDefault(m =>
                    string.Equals(m.Id, info.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (!string.IsNullOrWhiteSpace(info.Name)) existing.Name = info.Name;
                    if (info.ContextLength > 0 && existing.ContextOverride == null)
                        existing.ContextLength = info.ContextLength;
                    existing.Vision = info.Vision;
                    existing.SupportsTools = info.SupportsTools;
                    existing.Available = true;
                }
                else
                {
                    provider.Models.Add(new AiModel
                    {
                        Id = info.Id,
                        Name = string.IsNullOrWhiteSpace(info.Name) ? info.Id : info.Name,
                        ContextLength = info.ContextLength > 0 ? info.ContextLength : ContextBudget.FallbackContext,
                        Vision = info.Vision,
                        SupportsTools = info.SupportsTools,
                        Manual = false,
                        Available = true
                    });
                    added++;
                }
            }
            foreach (var m in provider.Models)
                if (!m.Manual && !scanned.Any(s => string.Equals(s.Id, m.Id, StringComparison.OrdinalIgnoreCase)))
                    m.Available = false;
            if (string.IsNullOrEmpty(provider.ActiveModelId) && provider.Models.Count > 0)
                provider.ActiveModelId = provider.Models.FirstOrDefault(m => m.Available)?.Id ?? provider.Models[0].Id;
            return added;
        }

        internal static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            try
            {
                var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
            }
        }

        internal static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            try
            {
                var bytes = Convert.FromBase64String(stored);
                try
                {
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
                }
                catch
                {
                    return Encoding.UTF8.GetString(bytes);
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public static class AiEffort
    {
        public sealed class Option
        {
            public string Label { get; }
            public string Value { get; }
            public Option(string label, string value) { Label = label; Value = value; }
            public override string ToString() => Label;
        }

        static readonly (string Value, string Label)[] Values =
        {
            ("off", "Aus (off)"),
            ("minimal", "Minimal"),
            ("low", "Niedrig (low)"),
            ("medium", "Mittel (medium)"),
            ("xhigh", "Extrem (xhigh)"),
            ("adaptive", "Adaptiv"),
        };

        public static readonly Option[] ModelOptions = Build("keine – Server-Standard");
        public static readonly Option[] ChatOptions = Build("Denken: Modell-Standard");

        static Option[] Build(string firstLabel)
        {
            var list = new List<Option> { new Option(firstLabel, null) };
            foreach (var v in Values) list.Add(new Option(v.Label, v.Value));
            return list.ToArray();
        }

        public static Option Find(Option[] options, string value) =>
            options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)) ?? options[0];

        public static string Resolve(AiModel model)
        {
            var chat = SessionStore.Prefs.AiReasoningEffort;
            if (!string.IsNullOrWhiteSpace(chat)) return chat.Trim();
            var perModel = model?.ReasoningEffort;
            return string.IsNullOrWhiteSpace(perModel) ? null : perModel.Trim();
        }

        public static string LabelOf(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Standard";
            var hit = Values.FirstOrDefault(v => string.Equals(v.Value, value, StringComparison.OrdinalIgnoreCase));
            return hit.Label ?? value;
        }
    }
}
