using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NovaNote
{
    public partial class FavoritesDialog : Window
    {
        bool _loading;

        public FavoritesDialog()
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            Closed += (s, e) => CleanupEmpty();
            Refresh(selectFirst: true);
        }

        void Refresh(bool selectFirst = false)
        {
            var items = KiHistory.Favorites.ToList();
            var selectedId = (FavoriteList.SelectedItem as Favorite)?.Id;
            FavoriteList.ItemsSource = items;
            var selected = selectFirst ? items.FirstOrDefault()
                : items.FirstOrDefault(f => f.Id == selectedId);
            FavoriteList.SelectedItem = selected;
            UpdateEditor(selected);
        }

        void UpdateEditor(Favorite favorite)
        {
            _loading = true;
            TextEdit.Text = favorite?.Text ?? "";
            _loading = false;
            TextEdit.IsEnabled = favorite != null;
            DeleteButton.IsEnabled = favorite != null;
        }

        void FavoriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEditor(FavoriteList.SelectedItem as Favorite);
        }

        void TextEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            var favorite = FavoriteList.SelectedItem as Favorite;
            if (favorite == null) return;
            KiHistory.UpdateFavorite(favorite, TextEdit.Text);
            Refresh();
        }

        void NewFavorite_Click(object sender, RoutedEventArgs e)
        {
            var favorite = KiHistory.AddFavorite("");
            Refresh();
            FavoriteList.SelectedItem = favorite;
            UpdateEditor(favorite);
            TextEdit.Focus();
        }

        void DeleteFavorite_Click(object sender, RoutedEventArgs e)
        {
            KiHistory.RemoveFavorite(FavoriteList.SelectedItem as Favorite);
            Refresh(selectFirst: true);
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();

        void CleanupEmpty()
        {
            foreach (var f in KiHistory.Favorites.Where(f => string.IsNullOrWhiteSpace(f.Text)).ToList())
                KiHistory.RemoveFavorite(f);
        }
    }
}
