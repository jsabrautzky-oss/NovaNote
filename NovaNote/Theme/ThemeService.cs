using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace NovaNote
{
    public static class ThemeService
    {
        public const string Light = "hell";
        public const string Dark = "dunkel";
        public const string System = "system";

        public static string Mode { get; private set; } = System;
        public static bool IsDark { get; private set; }

        public static event Action ThemeChanged;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        static ThemeService()
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (Mode == System && e.Category == UserPreferenceCategory.General)
                Apply();
        }

        public static void SetMode(string mode)
        {
            Mode = mode;
            Apply();
        }

        private static bool ReadSystemPrefersLight()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var v = key?.GetValue("AppsUseLightTheme");
                if (v is int i) return i != 0;
            }
            catch { }
            return true;
        }

        public static void Apply()
        {
            IsDark = Mode == Dark || (Mode == System && !ReadSystemPrefersLight());

            var app = Application.Current;
            if (app == null) return;

            var dicts = app.Resources.MergedDictionaries;
            var themeUri = $"pack://application:,,,/Theme/{(IsDark ? "Dark" : "Light")}.xaml";
            var replacement = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Absolute) };
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.ToString();
                if (src != null && (src.Contains("Theme/Light") || src.Contains("Theme/Dark")))
                {
                    dicts[i] = replacement;
                    break;
                }
            }

            foreach (Window w in app.Windows.Cast<Window>().ToList())
                ApplyTitleBar(w);

            ThemeChanged?.Invoke();
        }

        public static void ApplyTitleBar(Window window)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        }

        public static void AttachWindow(Window window)
        {
            window.SourceInitialized += (s, e) => ApplyTitleBar(window);
        }
    }
}
