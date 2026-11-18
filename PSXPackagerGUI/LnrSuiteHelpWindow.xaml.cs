using System.Windows;

namespace PSXPackagerGUI
{
    public partial class LnrSuiteHelpWindow : Window
    {
        public LnrSuiteHelpWindow() => InitializeComponent();

        private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
    }
}
