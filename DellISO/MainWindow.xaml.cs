using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using System;
using System.IO;
using DellISO.Views;

namespace DellISO
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // 1. 获取窗口句柄
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(myWndId);

            // 2. 获取图标的绝对路径 (解决官方文档要求的 "fully qualified path")
            // 注意：这里用 Path.GetFullPath 确保给系统的是完整路径
            string relativePath = Path.Combine(AppContext.BaseDirectory, "Assets/ps.ico");
            string fullPath = Path.GetFullPath(relativePath);

            // 3. 只有当文件确实存在时才设置，防止发布后路径变动导致报错
            if (File.Exists(fullPath))
            {
                appWindow.SetIcon(fullPath);
            }

            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            SettingPage.SharedViewModel.Cleanup();
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            NavView_Navigate("DownloadPage");
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
                NavView_Navigate(item.Tag.ToString());
        }

        private void NavView_Navigate(string tag)
        {
            Type type = tag switch
            {
                "DownloadPage" => typeof(Views.DownloadPage),
                "SettingPage" => typeof(Views.SettingPage),
                "AboutPage" => typeof(Views.AboutPage),
                _ => null
            };
            if (type != null) ContentFrame.Navigate(type);
        }
    }
}