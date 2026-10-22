using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace NovaNote
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new System.Version(1, 0, 0);
            VersionText.Text = $"Version {v.Major}.{v.Minor}.{v.Build} – .NET 9 WPF";
        }
    }
}
