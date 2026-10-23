using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NovaNote
{
    public class AppPrefs
    {
        public bool RestoreSession { get; set; } = true;
        public bool AskSaveOnClose { get; set; } = true;
        public bool MultiTab { get; set; } = true;
        public bool LanguageAuto { get; set; } = true;
        public bool ShowLineNumbers { get; set; } = true;
        public bool ShowWhitespace { get; set; } = true;
        public bool SyntaxCheck { get; set; } = true;
        public bool Folding { get; set; } = true;
        public string Theme { get; set; } = ThemeService.System;
        public bool UpdateCheck { get; set; } = true;
        public string DismissedUpdateVersion { get; set; } = null;
        public bool AiPanelOpen { get; set; } = false;
        public double? AiPanelWidth { get; set; } = null;
        public bool AiAutoApply { get; set; } = false;
    }

    public class SessionTab
    {
        public string Path { get; set; } = null;
        public string Text { get; set; } = "";
        public NovaNoteEncoding Enc { get; set; } = NovaNoteEncoding.Utf8;
        public LineEnding Eol { get; set; } = LineEnding.CrLf;
        public int Zoom { get; set; } = 100;
        public bool Wrap { get; set; } = true;
        public double ScrollX { get; set; }
        public double ScrollY { get; set; }
        public int Caret { get; set; }
        public bool Dirty { get; set; }
        public int Language { get; set; }
    }

    public class SessionWindow
    {
        public List<SessionTab> Tabs { get; set; } = new List<SessionTab>();
        public int Active { get; set; }
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 900;
        public double Height { get; set; } = 640;
        public bool Detached { get; set; }
    }

    public class SessionState
    {
        public List<SessionWindow> Windows { get; set; } = new List<SessionWindow>();
    }

    public static class SessionStore
    {
        public static string DataDir
        {
            get
            {
                var testDir = Environment.GetEnvironmentVariable("NOVANOTE_DIR");
                if (!string.IsNullOrWhiteSpace(testDir)) return testDir;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaNote");
            }
        }

        static string Dir => DataDir;

        static string PrefPath => Path.Combine(Dir, "einstellungen.json");
        static string SessionPath => Path.Combine(Dir, "sitzung.json");

        public static AppPrefs Prefs { get; private set; } = new AppPrefs();

        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { WriteIndented = true };

        public static void LoadPrefs()
        {
            try
            {
                if (File.Exists(PrefPath))
                    Prefs = JsonSerializer.Deserialize<AppPrefs>(File.ReadAllText(PrefPath)) ?? new AppPrefs();
            }
            catch
            {
                Prefs = new AppPrefs();
            }
        }

        public static void SavePrefs()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PrefPath, JsonSerializer.Serialize(Prefs, Opt));
            }
            catch { }
        }

        public static bool HasSession => File.Exists(SessionPath);

        public static SessionState LoadSession()
        {
            try
            {
                if (HasSession)
                    return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(SessionPath));
            }
            catch { }
            return null;
        }

        public static void SaveSession(SessionState state)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SessionPath, JsonSerializer.Serialize(state, Opt));
            }
            catch { }
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionPath)) File.Delete(SessionPath);
            }
            catch { }
        }
    }
}
