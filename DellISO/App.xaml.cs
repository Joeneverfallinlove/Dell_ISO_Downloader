using Microsoft.UI.Xaml;
using System;

namespace DellISO
{
    public partial class App : Application
    {
        public Window MainWindowObj { get; private set; }

        public App()
        {

            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindowObj = new MainWindow();
            MainWindowObj.Activate();
        }
    }
}