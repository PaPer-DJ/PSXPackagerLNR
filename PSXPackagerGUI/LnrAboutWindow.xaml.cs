using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace PSXPackagerGUI
{
    /// <summary>Credits for the original project and the separate LNR adaptations.</summary>
    public partial class LnrAboutWindow : Window
    {
        public LnrAboutWindow()
        {
            InitializeComponent();
            VersionLabel.Text = LnrVersionInfo.Display;
        }

        private void Original_OnClick(object sender, RoutedEventArgs e) =>
            OpenProject("https://github.com/RupertAvery/PSXPackager");

        private void Fork_OnClick(object sender, RoutedEventArgs e) =>
            OpenProject("https://github.com/PaPer-DJ/PSXPackagerLNR");

        private void OpenProject(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo abrir el navegador: " + ex.Message,
                                "Enlace", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CompressionTechnical_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            // Link to the dedicated appendix within the LNR fork section.
            OpenProject("https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/README.md#anexo-tecnico-lnr-nivel-3");
        }

        private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
    }
}