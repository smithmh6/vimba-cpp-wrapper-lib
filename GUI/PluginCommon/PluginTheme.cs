using System;
using System.Collections.Generic;
using System.Windows;

namespace PluginCommon
{
    public static class PluginTheme
    {
        // Load Telerik resources, please follow the steps for customization 
        public static void LoadPluginTheme(bool isStandAlone)
        {
            // First step: Be aware of which Telerik controls you're using

            // Second step: Uncomment the file names corresponding to the controls you're using
            // Please turn to Telerik website if you don't know which should be included
            var telerikStyleFiles = new List<string>
            {
                //"System.Windows.xaml",
                //"Telerik.Windows.Controls.xaml",
                //"Telerik.Windows.Controls.Input.xaml",
                //"Telerik.Windows.Controls.Navigation.xaml",
                //"Telerik.Windows.Controls.RibbonView.xaml",
                //"Telerik.Windows.Controls.Diagrams.xaml",
                //"Telerik.Windows.Controls.Diagrams.Extensions.xaml",
                //"Telerik.Windows.Controls.Diagrams.Ribbon.xaml",
                //"Telerik.Windows.Cloud.Controls.xaml",
                //"Telerik.Windows.Controls.Docking.xaml",
                //"Telerik.Windows.Controls.ScheduleView.xaml",
                //"Telerik.Windows.Controls.GanttView.xaml",
                //"Telerik.Windows.Controls.Chart.xaml",
                //"Telerik.Windows.Controls.GridView.xaml",
                //"Telerik.Windows.Controls.FileDialogs.xaml",
                //"Telerik.Windows.Controls.DataVisualization.xaml",
                //"Telerik.Windows.Controls.Pivot.xaml",
                //"Telerik.Windows.Controls.PivotFieldList.xaml",
                //"Telerik.Windows.Controls.ImageEditor.xaml",
                //"Telerik.Windows.Controls.VirtualGrid.xaml",
                //"Telerik.Windows.Controls.Spreadsheet.xaml",
                //"Telerik.Windows.Controls.ConversationalUI.xaml",
                //"Telerik.Windows.Controls.Media.xaml",
                //"Telerik.Windows.Controls.SyntaxEditor.xaml"
            };

            foreach (string str in telerikStyleFiles)
            {
                string uriString = string.Format("/Telerik.Windows.Themes.Fluent;component/Themes/" + str);
                ResourceDictionary item = new ResourceDictionary
                {
                    Source = new Uri(uriString, UriKind.RelativeOrAbsolute)
                };
                if (IsNewResourceDictionary(item))
                    Application.Current.Resources.MergedDictionaries.Add(item);
            }

            // Third step: Load your customized styles ( The files are in Styles folder)
            var dict = new ResourceDictionary
            {
                Source = new Uri(string.Format("/PluginCommon;component/Styles/PluginStyles.xaml"), UriKind.RelativeOrAbsolute)
            };
            if (IsNewResourceDictionary(dict))
                Application.Current.Resources.MergedDictionaries.Add(dict);

            // Set color style, only used when the plugin loaded by other application
            if (!isStandAlone)
                SetColorStyle();
        }

        private static bool IsNewResourceDictionary(ResourceDictionary dict)
        {
            for (int i = 0; i < Application.Current.Resources.MergedDictionaries.Count; i++)
            {
                if (Application.Current.Resources.MergedDictionaries[i].Source == dict.Source)
                    return false;
            }
            return true;
        }

        public static void SetColorStyle()
        {
            /*
             * If using Telerik control, you need to add Telerik.Windows.Controls as reference, uncomment "using Telerik.Windows.Controls;" in LoadPluginTheme() and uncomment below lines
             * 
                FluentPalette.Palette.FontSizeS = 10;
                FluentPalette.Palette.FontSize = 12;
                FluentPalette.Palette.FontSizeL = 14;
                FluentPalette.Palette.FontFamily = new FontFamily("Segoe UI");

                // Dark theme by default
                FluentPalette.Palette.AccentColor = (Color)ColorConverter.ConvertFromString("#FF0086AF");
                FluentPalette.Palette.AccentFocusedColor = (Color)ColorConverter.ConvertFromString("#FF009fc4");
                FluentPalette.Palette.AccentMouseOverColor = (Color)ColorConverter.ConvertFromString("#FF00BFE8");
                FluentPalette.Palette.AccentPressedColor = (Color)ColorConverter.ConvertFromString("#FF005B70");
                FluentPalette.Palette.AlternativeColor = (Color)ColorConverter.ConvertFromString("#FF2B2B2B");
                FluentPalette.Palette.BasicColor = (Color)ColorConverter.ConvertFromString("#4CFFFFFF");
                FluentPalette.Palette.BasicSolidColor = (Color)ColorConverter.ConvertFromString("#FF4C4C4C");
                FluentPalette.Palette.ComplementaryColor = (Color)ColorConverter.ConvertFromString("#FF333333");
                FluentPalette.Palette.IconColor = (Color)ColorConverter.ConvertFromString("#CCFFFFFF");
                FluentPalette.Palette.MainColor = (Color)ColorConverter.ConvertFromString("#33FFFFFF");
                FluentPalette.Palette.MarkerColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
                FluentPalette.Palette.MarkerInvertedColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
                FluentPalette.Palette.MarkerMouseOverColor = (Color)ColorConverter.ConvertFromString("#FF000000");
                FluentPalette.Palette.MouseOverColor = (Color)ColorConverter.ConvertFromString("#4CFFFFFF");
                FluentPalette.Palette.PressedColor = (Color)ColorConverter.ConvertFromString("#26FFFFFF");
                FluentPalette.Palette.PrimaryBackgroundColor = (Color)ColorConverter.ConvertFromString("#FF0D0D0D");
                FluentPalette.Palette.PrimaryColor = (Color)ColorConverter.ConvertFromString("#66FFFFFF");
                FluentPalette.Palette.PrimaryMouseOverColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
                FluentPalette.Palette.ReadOnlyBackgroundColor = (Color)ColorConverter.ConvertFromString("#00FFFFFF");
                FluentPalette.Palette.ReadOnlyBorderColor = (Color)ColorConverter.ConvertFromString("#FF4C4C4C");
                FluentPalette.Palette.ValidationColor = (Color)ColorConverter.ConvertFromString("#FFE81123");
                FluentPalette.Palette.DisabledOpacity = 0.3;
                FluentPalette.Palette.InputOpacity = 1;
                FluentPalette.Palette.ReadOnlyOpacity = 0.5;
            */
        }
    }
}
