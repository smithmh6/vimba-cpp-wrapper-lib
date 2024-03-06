using Settings.ViewModels;
using System.Windows;

namespace Settings.Views
{
    /// <summary>
    /// Interaction logic for FilterSettingsWindow.xaml
    /// </summary>
    public partial class FilterSettingsWindow
    {
        public FilterSettingsWindow()
        {
            InitializeComponent();
            this.DataContext = new FilterSettingsWindowViewModel();
        }

        private void RadButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void UpdateData()
        {
            (this.DataContext as FilterSettingsWindowViewModel).UpdateSlots();
        }

        public void UpdateSettingData()
        {
            (this.DataContext as FilterSettingsWindowViewModel).UpdateSlotSetting();
        }
    }
}
