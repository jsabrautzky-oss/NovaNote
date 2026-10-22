using System;
using System.Globalization;
using System.Windows;

namespace NovaNote
{
    public partial class PageSetupDialogWin : Window
    {
        public PageSetupDialogWin(string printer, double l, double t, double r, double b)
        {
            InitializeComponent();
            ThemeService.AttachWindow(this);
            PrinterBox.Text = printer;
            MargL.Text = l.ToString(CultureInfo.CurrentCulture);
            MargT.Text = t.ToString(CultureInfo.CurrentCulture);
            MargR.Text = r.ToString(CultureInfo.CurrentCulture);
            MargB.Text = b.ToString(CultureInfo.CurrentCulture);
            Loaded += (s, e) => MargL.Focus();
        }

        void Ok_Click(object sender, RoutedEventArgs e)
        {
            var ci = CultureInfo.CurrentCulture;
            if (double.TryParse(MargL.Text, NumberStyles.Float, ci, out double l) &&
                double.TryParse(MargT.Text, NumberStyles.Float, ci, out double t) &&
                double.TryParse(MargR.Text, NumberStyles.Float, ci, out double r) &&
                double.TryParse(MargB.Text, NumberStyles.Float, ci, out double b) &&
                l >= 0 && t >= 0 && r >= 0 && b >= 0)
            {
                ((MainWindow)Owner).SetPrintMargins(l, t, r, b);
                DialogResult = true;
            }
            else
            {
                MessageBox.Show(this, "Geben Sie gültige Ränder in Zoll ein.",
                    "Seiteneinrichtung", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
