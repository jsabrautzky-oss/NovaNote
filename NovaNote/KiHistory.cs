using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaNote
{
    public class InputEntry
    {
        public string Text { get; set; } = "";
        public DateTime At { get; set; }

        [JsonIgnore]
        public bool Favorite => KiHistory.IsFavorite(Text);
    }

    public class Favorite
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public class KiHistoryState
    {
        public List<InputEntry> Entries { get; set; } = new List<InputEntry>();
    }

    public class KiFavoritesState
    {
        public List<Favorite> Items { get; set; } = new List<Favorite>();
    }

    public static class KiHistory
    {
        public const int MaxEntries = 10;

        static string Dir => SessionStore.DataDir;

        static string HistoryPath => Path.Combine(Dir, "ki-eingaben.json");
        static string FavoritesPath => Path.Combine(Dir, "ki-favoriten.json");

        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { WriteIndented = true };

        static KiHistoryState _history = new KiHistoryState();
        static KiFavoritesState _favorites = new KiFavoritesState();

        public static event Action Changed;

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (File.Exists(HistoryPath))
                    _history = JsonSerializer.Deserialize<KiHistoryState>(File.ReadAllText(HistoryPath)) ?? new KiHistoryState();
            }
            catch
            {
                _history = new KiHistoryState();
            }
            try
            {
                if (File.Exists(FavoritesPath))
                    _favorites = JsonSerializer.Deserialize<KiFavoritesState>(File.ReadAllText(FavoritesPath)) ?? new KiFavoritesState();
            }
            catch
            {
                _favorites = new KiFavoritesState();
            }
        }

        static void SaveHistory()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(HistoryPath, JsonSerializer.Serialize(_history, Opt));
                Changed?.Invoke();
            }
            catch { }
        }

        static void SaveFavorites()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FavoritesPath, JsonSerializer.Serialize(_favorites, Opt));
                Changed?.Invoke();
            }
            catch { }
        }

        public static IReadOnlyList<InputEntry> Entries => _history.Entries;

        public static void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            _history.Entries.RemoveAll(e => e.Text == text);
            _history.Entries.Insert(0, new InputEntry { Text = text, At = DateTime.Now });
            if (_history.Entries.Count > MaxEntries)
                _history.Entries.RemoveRange(MaxEntries, _history.Entries.Count - MaxEntries);
            SaveHistory();
        }

        public static void Clear()
        {
            _history.Entries.Clear();
            SaveHistory();
        }

        public static IReadOnlyList<Favorite> Favorites => _favorites.Items;

        public static bool IsFavorite(string text) =>
            !string.IsNullOrWhiteSpace(text) && _favorites.Items.Any(f => f.Text == text);

        public static void ToggleFavorite(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var existing = _favorites.Items.FirstOrDefault(f => f.Text == text);
            if (existing != null) _favorites.Items.Remove(existing);
            else _favorites.Items.Add(new Favorite { Id = AiStore.NewId("fav"), Text = text });
            SaveFavorites();
        }

        public static Favorite AddFavorite(string text)
        {
            var f = new Favorite { Id = AiStore.NewId("fav"), Text = text ?? "" };
            _favorites.Items.Add(f);
            SaveFavorites();
            return f;
        }

        public static void UpdateFavorite(Favorite favorite, string text)
        {
            if (favorite == null) return;
            favorite.Text = text ?? "";
            SaveFavorites();
        }

        public static void RemoveFavorite(Favorite favorite)
        {
            if (favorite == null) return;
            _favorites.Items.Remove(favorite);
            SaveFavorites();
        }
    }
}
