using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace NovaNote
{
    public partial class FontPickerDialog : Window
    {
        public FontFamily SelectedFamily { get; private set; }
        public FontStyle SelectedStyle { get; private set; } = FontStyles.Normal;
        public FontWeight SelectedWeight { get; private set; } = FontWeights.Normal;
        public double SelectedSize { get; private set; } = 15;

        readonly string[] _styleNames = { "Normal", "Fett", "Kursiv", "Fett Kursiv" };

        public FontPickerDialog(FontFamily family, FontStyle style, FontWeight weight, double size)
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);

            var families = Fonts.SystemFontFamilies
                .Select(f => new { Name = f.Source, F = f })
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            foreach (var f in families) FamilyList.Items.Add(f.Name);
            for (int s = 9; s <= 72; s++) SizeList.Items.Add(s.ToString());

            var initial = families.FirstOrDefault(x => x.F.Source == family.Source);
            if (initial != null) FamilyList.SelectedItem = initial.Name;
            else if (FamilyList.Items.Count > 0) FamilyList.SelectedIndex = 0;

            SelectStyle(style, weight);
            string sizeText = ((int)Math.Round(size * 72.0 / 96.0)).ToString();
            if (!SizeList.Items.Contains(sizeText)) SizeList.Items.Add(sizeText);
            SizeList.SelectedItem = sizeText;
            UpdatePreview();
        }

        void SelectStyle(FontStyle style, FontWeight weight)
        {
            bool bold = weight == FontWeights.Bold;
            bool italic = style == FontStyles.Italic;
            string name = bold && italic ? "Fett Kursiv" : bold ? "Fett" : italic ? "Kursiv" : "Normal";
            if (!StyleList.Items.Contains(name)) StyleList.Items.Add(name);
            StyleList.SelectedItem = name;
        }

        void FamilyList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FamilyList.SelectedItem == null) return;
            var family = new FontFamily((string)FamilyList.SelectedItem);
            var faces = family.GetTypefaces().ToList();
            bool bold = faces.Any(tf => tf.Weight == FontWeights.Bold);
            bool italic = faces.Any(tf => tf.Style == FontStyles.Italic);
            StyleList.Items.Clear();
            StyleList.Items.Add("Normal");
            if (bold) StyleList.Items.Add("Fett");
            if (italic) StyleList.Items.Add("Kursiv");
            if (bold && italic) StyleList.Items.Add("Fett Kursiv");
            StyleList.SelectedIndex = 0;
            UpdatePreview();
        }

        void StyleList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdatePreview();
        void SizeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdatePreview();

        void UpdatePreview()
        {
            if (FamilyList.SelectedItem == null) return;
            Preview.FontFamily = new FontFamily((string)FamilyList.SelectedItem);
            string st = StyleList.SelectedItem as string ?? "Normal";
            Preview.FontStyle = st.Contains("Kursiv") ? FontStyles.Italic : FontStyles.Normal;
            Preview.FontWeight = st.Contains("Fett") ? FontWeights.Bold : FontWeights.Normal;
            if (double.TryParse(SizeList.SelectedItem as string, out double pt))
                Preview.FontSize = pt * 96.0 / 72.0;
        }

        void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (FamilyList.SelectedItem == null) return;
            SelectedFamily = new FontFamily((string)FamilyList.SelectedItem);
            string st = StyleList.SelectedItem as string ?? "Normal";
            SelectedStyle = st.Contains("Kursiv") ? FontStyles.Italic : FontStyles.Normal;
            SelectedWeight = st.Contains("Fett") ? FontWeights.Bold : FontWeights.Normal;
            if (double.TryParse(SizeList.SelectedItem as string, out double pt))
                SelectedSize = pt * 96.0 / 72.0;
            DialogResult = true;
        }
    }
}
