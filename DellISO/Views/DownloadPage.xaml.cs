using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Text.RegularExpressions;
using DellISO; 

namespace DellISO.Views
{
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : false;
        }
    }

    public sealed partial class DownloadPage : Page
    {
        public MainViewModel ViewModel => SettingPage.SharedViewModel;

        public DownloadPage()
        {
            this.InitializeComponent();
        }

        private void OnTagChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            string raw = sender.Text;
            string clean = Regex.Replace(raw, "[^a-zA-Z0-9]", "").ToUpper();
            if (raw != clean)
            {
                int pos = sender.SelectionStart;
                sender.Text = clean;
                sender.SelectionStart = Math.Min(pos, clean.Length);
            }
        }
    }
}