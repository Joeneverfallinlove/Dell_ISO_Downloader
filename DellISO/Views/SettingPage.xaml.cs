using Microsoft.UI.Xaml.Controls;
using DellISO;

namespace DellISO.Views
{
    public sealed partial class SettingPage : Page
    {
        public static MainViewModel SharedViewModel { get; } = new MainViewModel();

        public MainViewModel ViewModel => SharedViewModel;

        public SettingPage()
        {
            this.InitializeComponent();
        }
    }
}