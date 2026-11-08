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
        /// PSXPackagerLNR.exe is a tiny native launcher. It runs the existing
        /// full PSXPackagerGUI.exe with --lnr-batch, so the GUI/.NET runtime
        /// is shipped only once. Direct calls to PSXPackagerGUI.exe retain
        /// the original classic interface.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var directLnr = e.Args.Any(argument =>
                string.Equals(argument, "--lnr-batch", StringComparison.OrdinalIgnoreCase));

            // Also support the full executable-name copy shipped in beta.16.
            // Existing users can continue launching that older variant.
            var executableName = Path.GetFileName(Environment.ProcessPath ?? "");
            directLnr |= string.Equals(
                executableName, "PSXPackagerLNR.exe", StringComparison.OrdinalIgnoreCase);

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
