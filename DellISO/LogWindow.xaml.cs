using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DellISO.Views;

namespace DellISO
{
    public sealed partial class LogWindow : Window
    {
        public MainViewModel ViewModel => SettingPage.SharedViewModel;

        public LogWindow()
        {
            this.InitializeComponent();

            ViewModel.LogUpdated += ViewModel_LogUpdated;

            this.Closed += (s, e) => ViewModel.LogUpdated -= ViewModel_LogUpdated;
        }

        private void ViewModel_LogUpdated(object sender, System.EventArgs e)
        {
            if (LogBox.Text.Length > 0)
            {
                LogBox.Select(LogBox.Text.Length, 0);
            }
        }
    }
}