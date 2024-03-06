using System;
using System.Windows;
using FilterWheel.Localization;
using FilterWheelShared.Common;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            CopyRightTB.Text = ThorlabsProduct.CopyRight;
            VersionTB.Text = $" {LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.Version)} {ThorlabsProduct.Version}";
        }

        private void PART_CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
