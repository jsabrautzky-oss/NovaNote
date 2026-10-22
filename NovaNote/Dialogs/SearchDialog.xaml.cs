using System.Windows;
using System.Windows.Input;

namespace NovaNote
{
    public partial class SearchDialog : Window
    {
        readonly MainWindow _main;
        readonly bool _replace;

        public SearchDialog(MainWindow main, bool replaceMode)
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            _main = main;
            _replace = replaceMode;
            if (!_replace)
            {
                ReplaceLabel.Visibility = Visibility.Collapsed;
                ReplaceBox.Visibility = Visibility.Collapsed;
                ReplaceButton.Visibility = Visibility.Collapsed;
                ReplaceAllButton.Visibility = Visibility.Collapsed;
                Title = "Suchen";
            }
            else
            {
                Title = "Ersetzen";
            }
            Loaded += (s, e) => { SearchBox.Text = _main.LastTerm; SearchBox.Focus(); SearchBox.SelectAll(); };
        }

        void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Find_Click(sender, e); e.Handled = true; }
        }

        void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _replace) { Replace_Click(sender, e); e.Handled = true; }
        }

        void Find_Click(object sender, RoutedEventArgs e)
        {
            _main.SaveSearchArgs(SearchBox.Text, ReplaceBox.Text,
                MatchCase.IsChecked == true, WholeWord.IsChecked == true);
            _main.DoSearch(SearchBox.Text, MatchCase.IsChecked == true,
                WholeWord.IsChecked == true, Backward.IsChecked == true);
        }

        void Replace_Click(object sender, RoutedEventArgs e)
        {
            _main.SaveSearchArgs(SearchBox.Text, ReplaceBox.Text,
                MatchCase.IsChecked == true, WholeWord.IsChecked == true);
            _main.ReplaceCurrent(SearchBox.Text, ReplaceBox.Text,
                MatchCase.IsChecked == true, WholeWord.IsChecked == true);
        }

        void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            int n = _main.ReplaceAll(SearchBox.Text, ReplaceBox.Text,
                MatchCase.IsChecked == true, WholeWord.IsChecked == true);
            MessageBox.Show(this, $"{n} Vorkommen wurden ersetzt.", "Ersetzen",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
