using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace NovaNote
{
    public partial class App : Application
    {
        public static string[] StartupArgs { get; set; } = Array.Empty<string>();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Codec.Name(NovaNoteEncoding.Utf8);
            SessionStore.LoadPrefs();
            ThemeService.SetMode(SessionStore.Prefs.Theme);

            var args = StartupArgs.ToList();

            // Hilfe anzeigen und beenden.
            if (args.RemoveAll(x => x == "/?") > 0)
            {
                MessageBox.Show(
                    "Verwendung: NovaNote [Option] [Datei]\n\n" +
                    "  Datei            Datei beim Start öffnen (Kodierung wird erkannt)\n" +
                    "  /N               Neues Fenster in eigener Instanz öffnen\n" +
                    "  /S x y w h       Lage und Größe des Fensters (Pixel)\n" +
                    "  /?               Diese Benutzung anzeigen",
                    "NovaNote – Benutzung", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            // /N startet eine zweite Instanz und gibt alle restlichen Argumente weiter.
            if (args.RemoveAll(x => x == "/N") > 0)
            {
                var rest = string.Join(" ", args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a));
                try
                {
                    Process.Start(new ProcessStartInfo(Environment.ProcessPath, rest) { UseShellExecute = true });
                }
                catch { }
                Shutdown(0);
                return;
            }

            // Fensterposition/Größe über /S x y w h.
            int[] rect = null;
            int si = args.IndexOf("/S");
            if (si >= 0 && args.Count >= si + 5)
            {
                var vals = new int[4];
                bool ok = true;
                for (int i = 0; i < 4; i++)
                    ok &= int.TryParse(args[si + 1 + i], out vals[i]);
                if (ok)
                {
                    rect = vals;
                    args.RemoveRange(si, 5);
                }
            }

            // Dateipfad erkennen, z. B. durch Doppelklick auf .txt/.json/.xml/.html.
            string file = args.FirstOrDefault(a => !a.StartsWith("/") && !a.StartsWith("-"));

            // Session wiederherstellen, falls aktiv und vorhanden.
            SessionState session = null;
            int extra = 0;
            if (SessionStore.Prefs.RestoreSession && SessionStore.HasSession)
            {
                session = SessionStore.LoadSession();
                if (session?.Windows != null && session.Windows.Count > 0)
                    extra = session.Windows.Count - 1;
                else
                    session = null;
            }

            var window = new MainWindow(session != null, 0, session);
            if (rect != null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = rect[0];
                window.Top = rect[1];
                window.Width = rect[2];
                window.Height = rect[3];
            }
            window.Show();

            // Weitere windows aus der Session öffnen.
            for (int i = 1; i <= extra; i++)
            {
                var w = new MainWindow(true, i, session);
                if (i < session.Windows.Count) w.Detached = session.Windows[i].Detached;
                w.Show();
            }

            // Wenn eine Datei übergeben wurde, diese im aktiven Fenster öffnen.
            // Das entspricht dem Öffnen durch Windows-Dateizuordnung.
            if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                window.OpenInNewTab(file);
        }
    }
}
