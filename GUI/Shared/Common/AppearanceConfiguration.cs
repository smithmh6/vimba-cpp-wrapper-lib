namespace FilterWheelShared.Common
{
    public static class AppearanceConfiguration
    {
        // We support 2 set of styles for main window appearance.
        // 1. Standard mode: Large button with text shown under it, no menus in the top left corner.
        // 2. Compact mode: Small button without text, show menus in the top left corner.
        public static int LargeButtonSize = 64;
        public static int LargeRibbonHeight = 60;
        public static int LargeIconSize = 26; // Not used
        public static int SmallButtonSize = 32;
        public static int SmallRibbonHeight = 32;
        public static int SmallIconSize = 24;

        public static int RibbonHeight;
        public static int RibbonButtonSize;
        public static int RibbonIconSize;

        public static bool IsButtonTextDisplay;
        public static bool IsMenuDisplay;

        public static string ThemeName; // Material or Fluent

        public static bool IsMaterialTheme()
        {
            return (ThemeName == "Material") ? true : false;
        }
        public static bool IsFluentTheme()
        {
            return (ThemeName == "Fluent") ? true : false;
        }

        // The method for you to set whether to show in standard mode
        public static void SetRibbonAppearance(bool isStandard)
        {
            if (isStandard)
            {
                RibbonHeight = LargeRibbonHeight;
                RibbonButtonSize = LargeButtonSize;
                RibbonIconSize = LargeIconSize;
                IsButtonTextDisplay = true;
            }
            else
            {
                RibbonHeight = SmallRibbonHeight;
                RibbonButtonSize = SmallButtonSize;
                RibbonIconSize = SmallIconSize;
                IsButtonTextDisplay = false;
            }
        }

        public static void SetShowHideMenu(bool show)
        {
            IsMenuDisplay = show;
        }
    }
}
