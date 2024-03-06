using CameraControl.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using Telerik.Windows.Controls;
using FilterWheelShared.Controls.OpenFolderDialog;
using LocalizationManager = FilterWheelShared.Localization.LocalizationManager;
using System.Windows;

namespace CameraControl
{
    public partial class CameraControlUC : UserControl
    {
        public CameraControlUC()
        {
            Init(true);
            InitializeComponent();

            NumberFormatInfo nfi = (NumberFormatInfo)System.Threading.Thread.CurrentThread
                       .CurrentCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "";
            ExposureNumeric.NumberFormatInfo = nfi;
            GainNumeric.NumberFormatInfo = nfi;   

            var vm = new CameraControlViewModel();
            vm.SetSettingWindowCallback(UpdateSettingWindow);
            this.DataContext = vm;
            Loaded += CameraControlUC_Loaded;
        }

        private void CameraControlUC_Loaded(object sender, RoutedEventArgs e)
        {
            (this.DataContext as CameraControlViewModel).UpdateFilterSettings();
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

        private void RadButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox element)
            {
                TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                element.MoveFocus(request);
            }
        }
        private void RadFilePathPicker_DialogOpening(object sender, Telerik.Windows.Controls.FileDialogs.DialogOpeningEventArgs e)
        {
            e.Cancel = true;
            var path = FilterWheelShared.DeviceDataService.CaptureService.Instance.SaveFilePath;
            if (!Directory.Exists(path))
                path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dialog = new OpenFolderDialog() { SelectedPath = path };
            if (dialog.ShowDialog() == true)
            {
                RadFilePathPicker picker = sender as RadFilePathPicker;

                if (picker != null)
                {
                    picker.FilePath = dialog.SelectedPath;
                    picker.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }

        private Settings.Views.FilterSettingsWindow _settingsWindow = null;
        private void SlotSettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new Settings.Views.FilterSettingsWindow() 
                { 
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                _settingsWindow.Closed += (s, arg) => 
                {
                    (this.DataContext as CameraControlViewModel).UpdateFilterSettings();
                };
            }
            if (!_settingsWindow.IsOpen)
            {
                _settingsWindow.UpdateData();
                _settingsWindow.Show();
            }
            _settingsWindow.IsTopmost = true;
            _settingsWindow.IsTopmost = false;
        }

        private void UpdateSettingWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsOpen)
            {
                _settingsWindow.UpdateSettingData();
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var index = (int)(sender as RadioButton).GetValue(Infrastructure.RadioButtonExtension.IndexProperty);
            (this.DataContext as CameraControlViewModel).UpdateFilterSettings(index);
        }
    }
}
