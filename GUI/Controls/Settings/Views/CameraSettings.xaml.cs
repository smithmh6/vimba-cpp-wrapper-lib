using Settings.ViewModels;
using System;
using System.Globalization;
using System.Windows.Controls;
using FilterWheelShared.Localization;

namespace Settings.Views
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class CameraSettingsUC : UserControl
    {
        public CameraSettingsUC()
        {
            Init(true);
            InitializeComponent();

            NumberFormatInfo nfi = (NumberFormatInfo)System.Threading.Thread.CurrentThread
                       .CurrentCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "";
            TopNumeric.NumberFormatInfo = nfi;
            LeftNumeric.NumberFormatInfo = nfi;
            BottomNumeric.NumberFormatInfo = nfi;
            RightNumeric.NumberFormatInfo = nfi;

            var vm = new CameraSettingsViewModel();
            DataContext = vm;
        }

        public static void Init(bool isStandAlone)
        {
            if (!isStandAlone)
            {
                CultureInfo.CurrentCulture =
                CultureInfo.CurrentUICulture =
                CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }

            PluginCommon.PluginTheme.LoadPluginTheme(isStandAlone);

            LocalizationManager.GetInstance().AddListener(PluginCommon.Localization.PluginLocalizationService.GetInstance());
        }

        public event EventHandler<bool> SettingWindowCloseEvent;

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SettingWindowCloseEvent?.Invoke(this, false);
        }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SettingWindowCloseEvent?.Invoke(this, true);
        }

        public void UpdateSource()
        {
            var vm = this.DataContext as CameraSettingsViewModel;
            vm.StartUpdate();
        }
    }
}
