using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace PSXPackagerGUI
{
    /// <summary>
    /// A link-capable replacement for MessageBox, which cannot render
    /// clickable URLs. Merely opening links never authorizes an import.
    /// </summary>
    public partial class LnrChdmanImportDialog : Window
    {
        public LnrChdmanImportDialog()
        {
            InitializeComponent();
        }

        private void SourceLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            var url = e.Uri.AbsoluteUri;
            // All supported links are fixed, public documentation pages.
            if (url != "https://wiki.recalbox.com/es/tutorials/utilities/rom-conversion/chdman" &&
                url != "https://onionui.github.io/docs/advanced/chdman" &&
                url != "https://www.mamedev.org/release.html")
                return;

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo abrir el navegador: " + ex.Message,
                    "No se pudo abrir el enlace",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Import_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void No_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}