using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PSXPackagerGUI.Common;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        }

        /// <summary>
        /// The GUI distribution includes two entry-point names using the
        /// same self-contained application bundle. PSXPackagerLNR.exe goes
        /// straight to the LNR batch workflow; PSXPackagerGUI.exe preserves
        /// the original multi-page application for advanced users.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var executableName = Path.GetFileName(Environment.ProcessPath ?? "");
            var directLnr = string.Equals(
                executableName, "PSXPackagerLNR.exe", StringComparison.OrdinalIgnoreCase);

            // Both are ordinary main windows. Closing the LNR launcher
            // exits the app instead of exposing/starting the classic GUI.
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            Window window = directLnr ? new LnrBatchWindow() : new MainWindow();
            MainWindow = window;
            window.Show();
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.LogError($"An unhandled exception occured.", e.ExceptionObject as Exception);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.LogError("An unhandled exception occured.", e.Exception);
            e.Handled = true;
        }
    }
}
