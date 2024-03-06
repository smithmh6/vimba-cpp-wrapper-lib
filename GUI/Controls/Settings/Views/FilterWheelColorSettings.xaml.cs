using Settings.ViewModels;
using System;
using System.Windows.Controls;

namespace Settings.Views
{
    /// <summary>
    /// Interaction logic for FilterWheelSettings.xaml
    /// </summary>
    public partial class FilterWheelColorSettings : UserControl
    {
        public FilterWheelColorSettings()
        {
            InitializeComponent();
            this.DataContext = new FilterWheelColorSettingsViewModel();
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

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            var vm = this.DataContext as FilterWheelColorSettingsViewModel;
            ColorGrid.ItemsSource = vm.SimpleSlots;
        }

        public void UpdateSource()
        {
            var vm = this.DataContext as FilterWheelColorSettingsViewModel;
            vm.UpdateSource();
            ColorGrid.ItemsSource = vm.SimpleSlots;
        }
    }
}
