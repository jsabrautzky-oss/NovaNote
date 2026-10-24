using System.Windows;

namespace NovaNote
{
    public partial class UpdateProgressDialog : Window
    {
        bool _allowClose;

        public UpdateProgressDialog()
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            Closing += (s, e) => { if (!_allowClose) e.Cancel = true; };
        }

        public void SetText(string text) => InfoText.Text = text;

        public void SetProgress(int percent)
        {
            Bar.Value = percent;
            PercentText.Text = $"{percent}%";
        }

        public void AllowClose()
        {
            _allowClose = true;
        }
    }
}
