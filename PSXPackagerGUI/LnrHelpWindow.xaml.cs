using System;
using System.Diagnostics;
using System.Windows;

namespace PSXPackagerGUI
{
    public partial class LnrHelpWindow : Window
    {
        public LnrHelpWindow()
        {
            InitializeComponent();
        }

        private void Manual_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/LNR/GUI-AYUDA.md")
                { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo abrir el manual: " + ex.Message,
                    "Ayuda", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
    }
}