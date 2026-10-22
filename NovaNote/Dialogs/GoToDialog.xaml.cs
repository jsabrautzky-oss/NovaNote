using System.Windows;
using System.Windows.Input;

namespace NovaNote
{
    public partial class GoToDialog : Window
    {
        readonly MainWindow _main;

        public GoToDialog(MainWindow main)
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            _main = main;
            Loaded += (s, e) => { LineBox.Focus(); };
        }

        void LineBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Ok_Click(sender, e); e.Handled = true; }
        }

        void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(LineBox.Text.Trim(), out int n) && n >= 1 && n <= _main.LineCount)
            {
                _main.GoToLine(n);
                Close();
            }
            else
            {
                MessageBox.Show(this, $"Geben Sie eine Zeile zwischen 1 und {_main.LineCount} ein.",
                    "Gehe zu Zeile", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
