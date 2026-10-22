using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Velopack;
using Velopack.Locators;

namespace NovaNote
{
    public static class UpdateService
    {
        public const string RepoUrl = "https://github.com/jsabrautzky-oss/NovaNote";
        public const string ReleasesApiUrl = "https://api.github.com/repos/jsabrautzky-oss/NovaNote/releases?per_page=100";
        public const string SetupAssetName = "NovaNote-win-Setup.exe";
        const string FullAssetPrefix = "NovaNote-";
        const string FullAssetSuffix = "-full.nupkg";

        static bool _startupScheduled;
        static bool _promptActive;
        static bool _downloadActive;

        public static bool IsInstalled =>
            VelopackLocator.IsCurrentSet &&
            !VelopackLocator.Current.IsPortable &&
            VelopackLocator.Current.CurrentlyInstalledVersion != null;

        public static void StartStartupCheck(Window window)
        {
            if (_startupScheduled) return;
            _startupScheduled = true;
            if (!IsInstalled || !SessionStore.Prefs.UpdateCheck) return;

            window.Dispatcher.BeginInvoke(new Action(async () =>
            {
                await CheckAsync(window, false);
            }));
        }

        public static async Task CheckAsync(Window owner, bool manual)
        {
            if (_promptActive || _downloadActive)
            {
                if (manual)
                    MessageBox.Show(owner, "Ein Update-Vorgang läuft bereits.", "Update prüfen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!manual && (!IsInstalled || !SessionStore.Prefs.UpdateCheck)) return;

            var current = GetCurrentVersion();
            if (current == null)
            {
                if (manual) MessageBox.Show(owner, "Aktuelle Version konnte nicht ermittelt werden.", "Update prüfen", MessageBoxButton.OK, MessageBoxImage.Warning);
                else Log("Aktuelle Version konnte nicht ermittelt werden.");
                return;
            }

            GitHubUpdateInfo latest;
            try
            {
                latest = await FindLatestSetupAsync(current);
            }
            catch (Exception ex)
            {
                if (manual) MessageBox.Show(owner, $"GitHub-Update-Check fehlgeschlagen:\n\n{ex.Message}", "Update prüfen", MessageBoxButton.OK, MessageBoxImage.Warning);
                else Log($"GitHub-Update-Check fehlgeschlagen: {ex}");
                return;
            }

            if (latest == null)
            {
                if (manual) MessageBox.Show(owner, "Kein Update verfügbar.", "Update prüfen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (latest.Version <= current)
            {
                if (manual) MessageBox.Show(owner, $"Kein Update verfügbar.\n\nAktuelle Version: {current}", "Update prüfen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dismissed = SessionStore.Prefs.DismissedUpdateVersion;
            if (!manual && !string.IsNullOrEmpty(dismissed) &&
                string.Equals(dismissed, latest.Version.ToString(), StringComparison.OrdinalIgnoreCase))
                return;

            _promptActive = true;
            var result = MessageBox.Show(owner,
                $"Ein Update für NovaNote ist verfügbar:\n\n" +
                $"Neue Version: {latest.Version}\n" +
                $"Aktuelle Version: {current}\n\n" +
                "Das NovaNote-Setup wird heruntergeladen und ausgeführt.\n" +
                "NovaNote wird danach beendet.\n\n" +
                "Fortfahren?",
                "Update verfügbar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            _promptActive = false;

            if (result != MessageBoxResult.Yes)
            {
                SessionStore.Prefs.DismissedUpdateVersion = latest.Version.ToString();
                SessionStore.SavePrefs();
                return;
            }

            await DownloadAndRunSetupAsync(owner, latest.SetupDownloadUrl, latest.Version.ToString());
        }

        static SemanticVersion GetCurrentVersion()
        {
            if (VelopackLocator.IsCurrentSet)
            {
                var installed = VelopackLocator.Current.CurrentlyInstalledVersion;
                if (installed != null) return installed;
            }

            var asm = Assembly.GetEntryAssembly();
            var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info))
                info = asm?.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(info)) return null;

            info = info.Split('+')[0].TrimStart('v');
            try { return SemanticVersion.Parse(info); }
            catch { return null; }
        }

        static async Task<GitHubUpdateInfo> FindLatestSetupAsync(SemanticVersion current)
        {
            var includePrerelease = current.ToString().Contains("-");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovaNote/UpdateChecker");
            client.Timeout = TimeSpan.FromSeconds(15);

            var releases = await client.GetFromJsonAsync<GitHubReleaseDto[]>(ReleasesApiUrl);
            if (releases == null) return null;

            GitHubUpdateInfo best = null;
            foreach (var release in releases)
            {
                var version = ParseReleaseVersion(release);
                if (version == null || version <= current) continue;
                if (!includePrerelease && release.Prerelease) continue;

                var setup = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, SetupAssetName, StringComparison.OrdinalIgnoreCase));
                if (setup == null) continue;

                var url = setup.BrowserDownloadUrl;
                if (string.IsNullOrEmpty(url)) continue;

                if (best == null || version > best.Version)
                {
                    best = new GitHubUpdateInfo
                    {
                        Version = version,
                        SetupDownloadUrl = url,
                        TagName = release.TagName ?? setup.Name
                    };
                }
            }

            return best;
        }

        static SemanticVersion ParseReleaseVersion(GitHubReleaseDto release)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(release.TagName))
                candidates.Add(release.TagName.TrimStart('v'));

            foreach (var asset in release.Assets ?? new List<GitHubAssetDto>())
            {
                if (asset.Name == null) continue;
                if (asset.Name.StartsWith(FullAssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith(FullAssetSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(asset.Name.Substring(FullAssetPrefix.Length, asset.Name.Length - FullAssetPrefix.Length - FullAssetSuffix.Length));
                }
            }

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                try { return SemanticVersion.Parse(candidate); }
                catch { }
            }

            return null;
        }

        static async Task DownloadAndRunSetupAsync(Window owner, string url, string version)
        {
            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
            {
                if (!w.SaveSessionForUpdate()) return;
            }
            SessionStore.SavePrefs();

            _downloadActive = true;
            var oldCursor = owner.Cursor;
            owner.Cursor = Cursors.Wait;

            try
            {
                var setupPath = Path.Combine(Path.GetTempPath(), SetupAssetName);
                if (File.Exists(setupPath)) File.Delete(setupPath);

                await DownloadSetupAsync(url, setupPath);

                if (owner is AboutDialog) owner.Close();

                LaunchSetup(setupPath);

                foreach (var w in Application.Current.Windows.OfType<MainWindow>())
                    w.AllowUpdateClose();

                Application.Current.Shutdown(0);
                Log($"Update-Setup gestartet: {version}");
            }
            catch (Exception ex)
            {
                owner.Cursor = oldCursor;
                MessageBox.Show(owner, $"Update-Setup konnte nicht gestartet werden:\n\n{ex.Message}", "Update fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Warning);
                Log($"Update-Setup fehlgeschlagen: {ex}");
            }
            finally
            {
                _downloadActive = false;
                owner.Cursor = oldCursor;
            }
        }

        static async Task DownloadSetupAsync(string url, string setupPath)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var file = new FileStream(setupPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file);
            file.Flush();
        }

        static void LaunchSetup(string setupPath)
        {
            var exeArgs = string.Join(" ", App.StartupArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            var setupArgs = "--silent";
            if (!string.IsNullOrEmpty(exeArgs)) setupArgs += " -- " + exeArgs;

            Process.Start(new ProcessStartInfo(setupPath, setupArgs) { UseShellExecute = true });
        }

        static void Log(string message)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaNote");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "update.log"), $"{DateTime.Now:O} {message}\n");
            }
            catch { }
        }

        class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")] public string TagName { get; set; }
            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
            [JsonPropertyName("assets")] public List<GitHubAssetDto> Assets { get; set; } = new List<GitHubAssetDto>();
        }

        class GitHubAssetDto
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; }
        }

        class GitHubUpdateInfo
        {
            public SemanticVersion Version { get; set; }
            public string SetupDownloadUrl { get; set; }
            public string TagName { get; set; }
        }
    }
}
