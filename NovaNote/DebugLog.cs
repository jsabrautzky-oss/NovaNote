using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NovaNote
{
    public static class DebugLog
    {
        static readonly object Gate = new object();
        const long MaxBytes = 10L * 1024 * 1024;
        const int MaxMsgChars = 50000;

        public static bool Enabled { get; set; } = false;

        public static string LogPath => Path.Combine(SessionStore.DataDir, "debug.log");

        public static void Log(string category, string message)
        {
            if (!Enabled) return;
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " [" + (category ?? "INFO") + "] " + Flatten(message);
                lock (Gate)
                {
                    RotateIfNeeded();
                    File.AppendAllText(LogPath, line + "\n");
                }
            }
            catch
            {
            }
        }

        public static void Session(string title)
        {
            Log("APP", title + " – NovaNote " + Version() + " · Log: " + LogPath);
        }

        static bool _started;
        public static void TryStart()
        {
            if (_started) return;
            _started = true;
            Session("Start");
        }

        public static void OpenFolder()
        {
            try
            {
                string path = LogPath;
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
                    {
                        UseShellExecute = true
                    });
                }
                else
                {
                    Directory.CreateDirectory(SessionStore.DataDir);
                    Process.Start(new ProcessStartInfo(SessionStore.DataDir) { UseShellExecute = true });
                }
            }
            catch
            {
            }
        }

        static string Version()
        {
            try { return typeof(DebugLog).Assembly.GetName().Version?.ToString() ?? "?"; }
            catch { return "?"; }
        }

        static void RotateIfNeeded()
        {
            try
            {
                var fi = new FileInfo(LogPath);
                if (!fi.Exists || fi.Length <= MaxBytes) return;
                string backup = LogPath + ".1";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LogPath, backup);
            }
            catch
            {
            }
        }

        static string Flatten(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            if (message.Length > MaxMsgChars)
                message = message.Substring(0, MaxMsgChars) + "…(gekürzt)";
            return message.Replace("\r\n", "\\n").Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
