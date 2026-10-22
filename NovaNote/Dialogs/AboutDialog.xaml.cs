using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Velopack.Locators;

namespace NovaNote
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);

            var asm = Assembly.GetEntryAssembly();
            var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info))
                info = asm?.GetName().Version?.ToString() ?? "unbekannt";
            var version = info.Split('+')[0];

            var install = VelopackLocator.IsCurrentSet
                ? (VelopackLocator.Current.IsPortable ? "portable" : "installiert")
                : "nicht installiert";

            VersionText.Text = $"Version {version} · .NET {Environment.Version.ToString(2)} WPF · Velopack: {install}";
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            await UpdateService.CheckAsync(this, true);

            if (button != null && IsLoaded) button.IsEnabled = true;
        }

        private void GitHub_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
