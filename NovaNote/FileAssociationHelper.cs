using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Velopack;

namespace NovaNote
{
    /// <summary>
    /// Hilfsklasse für Dateiendungen im Windows-Registry-Bereich HKCU.
    /// Die Registrierung läuft ohne Administratorrechte, weil ausschließlich
    /// HKEY_CURRENT_USER\Software\Classes geschrieben wird.
    /// </summary>
    public static class FileAssociationHelper
    {
        /// <summary>Die ProgID, unter der Windows unseren Editor als Anwendung erkennt.</summary>
        public const string ProgId = "NovaNote.Editor";

        /// <summary>Anzeigename für Explorer/"Öffnen mit"-Dialog.</summary>
        public const string Description = "NovaNote Editor";

        /// <summary>Endungen, die mit dem Editor verknüpft werden.</summary>
        public static readonly string[] Extensions = { ".txt", ".json", ".xml", ".html" };

        /// <summary>Unter Schlüssel im HKCU-Bereich für Klassen/ProgIDs.</summary>
        const string ClassesRoot = @"Software\Classes";

        // Windows-API: Explorer darüber informieren, dass sich Dateizuordnungen geändert haben.
        const int SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll", SetLastError = false)]
        static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        /// <summary>
        /// Legt die ProgID und die Endungsverknüpfungen an und benachrichtigt den Explorer.
        /// </summary>
        /// <param name="exePath">
        /// Pfad zur ausführbaren Datei. Wenn null, wird Environment.ProcessPath verwendet
        /// (bei Velopack die aktuell gestartete EXE im Update-Ordner).
        /// </param>
        /// <returns>true, wenn Registrierung erfolgreich war; sonst false.</returns>
        public static bool RegisterAssociations(string exePath = null)
        {
            exePath ??= Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            try
            {
                // ProgID definieren: HKCU\Software\Classes\NovaNote.Editor
                // Der Standardwert ist der Anzeigename im "Öffnen mit"-Dialog.
                using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{ProgId}"))
                {
                    if (progIdKey == null)
                        return false;

                    progIdKey.SetValue("", Description);

                    // Icon für den Explorer: EXE selbst, Icon-Index 0 (novanote.ico im exe eingebettet).
                    using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", $"\"{exePath}\",0");
                    }

                    // Öffnen-Befehl: Windows übergibt %1 als Dateipfad.
                    using (var commandKey = progIdKey.CreateSubKey(@"shell\open\command"))
                    {
                        commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                foreach (var extension in Extensions)
                {
                    // Endungen sind im HKCU-Bereich ohne leading dot? Nein: ".txt".
                    using (var extKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{extension}"))
                    {
                        if (extKey == null)
                            continue;

                        // Standardwert: ProgID, damit Doppelklick auf die Endung diesen Editor nutzt.
                        // Achtung: Wenn Windows bereits eine eigene "UserChoice"-Associerung gesetzt hat,
                        // hat diese für Explorer manchmal Vorrang; unsere ProgID erscheint dann nur im
                        // Dialog "Öffnen mit". UserChoice darf von Anwendungen nicht geschrieben werden.
                        extKey.SetValue("", ProgId);

                        // Eintrag für den "Öffnen mit"-Dialog. Der Wertname ist die ProgID, Inhalt leer.
                        using (var openWithKey = extKey.CreateSubKey("OpenWithProgids"))
                        {
                            openWithKey?.SetValue(ProgId, "");
                        }
                    }
                }

                NotifyShell();
                return true;
            }
            catch (Exception)
            {
                // Registry-Zugriff kann z. B. durch fehlende Rechte oder beschädigte Schlüssel fehlschlagen.
                return false;
            }
        }

        /// <summary>
        /// Entfernt bei Deinstallation unsere registrierten Associerungen rückstandslos.
        /// </summary>
        /// <returns>true, wenn Aufräumen erfolgreich war; sonst false.</returns>
        public static bool UnregisterAssociations()
        {
            try
            {
                // ProgID komplett entfernen (inkl. DefaultIcon und shell\open\command).
                Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{ProgId}", false);

                foreach (var extension in Extensions)
                {
                    using (var extKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{extension}", true))
                    {
                        if (extKey == null)
                            continue;

                        // Standardwert entfernen, aber nur wenn er unsere ProgID war.
                        // Sonst bleiben eventuell andere Benutzerverknüpfungen erhalten.
                        var current = extKey.GetValue("") as string;
                        if (current == ProgId)
                        {
                            extKey.SetValue("", null); // null löscht den Standardwert
                        }

                        using (var openWithKey = extKey.OpenSubKey("OpenWithProgids", true))
                        {
                            if (openWithKey == null)
                                continue;

                            if (openWithKey.GetValue(ProgId) != null)
                            {
                                openWithKey.SetValue(ProgId, null); // ProgID-Eintrag löschen
                            }

                            // Wenn nur noch unsere ProgID drin war, den leeren Unterkey entfernen.
                            if (openWithKey.GetValueNames().Length == 0)
                            {
                                extKey.DeleteSubKey("OpenWithProgids", false);
                            }
                        }
                    }
                }

                NotifyShell();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Benachrichtigt Windows/Explorer, dass sich Dateizuordnungen geändert haben.
        /// </summary>
        static void NotifyShell()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // Benachrichtigung ist optional; ohne sie wird der Explorer erst beim nächsten Refresh aktualisiert.
            }
        }
    }

    /// <summary>
    /// Fluent-Adapter für die Velopack-Hooks.
    /// Die installierte Velopack-Version verwendet OnFirstRun/OnAfterUpdateFastCallback/...
    /// Diese Extension-Methoden ermöglichen die vom Setup erwartete Schreibweise
    /// .WithFirstRun(), .WithAfterUpdate(), .WithBeforeUninstall().
    /// </summary>
    public static class VelopackAppExtensions
    {
        public static VelopackApp WithFirstRun(this VelopackApp app, Action<SemanticVersion> hook)
        {
            app.OnFirstRun(version => hook(version));
            return app;
        }

        public static VelopackApp WithAfterUpdate(this VelopackApp app, Action<SemanticVersion> hook)
        {
            app.OnAfterUpdateFastCallback(version => hook(version));
            return app;
        }

        public static VelopackApp WithBeforeUninstall(this VelopackApp app, Action<SemanticVersion> hook)
        {
            app.OnBeforeUninstallFastCallback(version => hook(version));
            return app;
        }
    }
}
