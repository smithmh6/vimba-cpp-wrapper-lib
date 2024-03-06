using System.Windows.Media;
using Telerik.Windows.Controls;

namespace FilterWheel.Infrastructure
{
    public static class ApplicationTheme
    {
        public const int RibbonHeight = 32;

        // Fluent theme
        public static void SetFontFamily()
        {
            FluentPalette.Palette.FontSizeS = 10;
            FluentPalette.Palette.FontSize = 12;
            FluentPalette.Palette.FontSizeL = 14;
            FluentPalette.Palette.FontFamily = new FontFamily("Segoe UI");
        }

        public static void SetColorStyle(int index)
        {
            switch (index)
            {
                case 0:
                    //light
                    FluentPalette.Palette.AccentColor = (Color)ColorConverter.ConvertFromString("#FF0086AF");
                    FluentPalette.Palette.AccentFocusedColor = (Color)ColorConverter.ConvertFromString("#FF009fc4");
                    FluentPalette.Palette.AccentMouseOverColor = (Color)ColorConverter.ConvertFromString("#FF00BFE8");
                    FluentPalette.Palette.AccentPressedColor = (Color)ColorConverter.ConvertFromString("#FF005B70");
                    FluentPalette.Palette.AlternativeColor = (Color)ColorConverter.ConvertFromString("#FFF2F2F2");
                    FluentPalette.Palette.BasicColor = (Color)ColorConverter.ConvertFromString("#33000000");
                    FluentPalette.Palette.BasicSolidColor = (Color)ColorConverter.ConvertFromString("#FFCDCDCD");
                    FluentPalette.Palette.ComplementaryColor = (Color)ColorConverter.ConvertFromString("#FFCCCCCC");
                    FluentPalette.Palette.IconColor = (Color)ColorConverter.ConvertFromString("#CC000000");
                    FluentPalette.Palette.MainColor = (Color)ColorConverter.ConvertFromString("#1A000000");
                    FluentPalette.Palette.MarkerColor = (Color)ColorConverter.ConvertFromString("#FF000000");
                    FluentPalette.Palette.MarkerInvertedColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
                    FluentPalette.Palette.MarkerMouseOverColor = (Color)ColorConverter.ConvertFromString("#FF000000");
                    FluentPalette.Palette.MouseOverColor = (Color)ColorConverter.ConvertFromString("#33000000");
                    FluentPalette.Palette.PressedColor = (Color)ColorConverter.ConvertFromString("#4C000000");
                    FluentPalette.Palette.PrimaryBackgroundColor = (Color)ColorConverter.ConvertFromString("#FFFAFAFA");
                    FluentPalette.Palette.PrimaryColor = (Color)ColorConverter.ConvertFromString("#66FFFFFF");
                    FluentPalette.Palette.PrimaryMouseOverColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
                    FluentPalette.Palette.ReadOnlyBackgroundColor = (Color)ColorConverter.ConvertFromString("#00FFFFFF");
                    FluentPalette.Palette.ReadOnlyBorderColor = (Color)ColorConverter.ConvertFromString("#FFCDCDCD");
                    FluentPalette.Palette.ValidationColor = (Color)ColorConverter.ConvertFromString("#FFE81123");
                    FluentPalette.Palette.DisabledOpacity = 0.3;
                    FluentPalette.Palette.InputOpacity = 1;
                    FluentPalette.Palette.ReadOnlyOpacity = 0.5;
                    //if you use scichart in you solution
                    //SciChart.Charting.ThemeManager.DefaultTheme = "ExpressionLight";
                    break;
                case 1:
                default:
                    //dark
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
                    //if you use scichart in you solution
                    //SciChart.Charting.ThemeManager.DefaultTheme = "ExpressionDark";
                    break;
            }
        }
    }
}
