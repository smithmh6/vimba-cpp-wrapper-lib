using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Telerik.Windows.Controls;
using FilterWheelShared.Common;

namespace FilterWheel.View
{
    public class CameraItem
    {
        public CameraInfo Camera { get; set; }
        public bool IsCurrent { get; set; }
    }

    public partial class SelectSingleDeviceWindow : RadWindow
    {
        public CameraInfo SelectedCamera { get; private set; }
        public SelectSingleDeviceWindow()
        {
            InitializeComponent();
        }

        public void Init(IEnumerable<CameraInfo> cameras)
        {
            UpdateSource(cameras);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SelectCamera();
            }
        }
        private void SelectCamera()
        {
            var item = CameraGrid.SelectedItem as CameraItem;
            if (item != null)
            {
                SelectedCamera = item.Camera;
                DialogResult = true;
                Close();
            }
        }

        private void OnOK_Click(object sender, RoutedEventArgs e)
        {
            SelectCamera();
        }

        private void RadListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SelectCamera();
            }
        }

        private void UpdateSource(IEnumerable<CameraInfo> cameras)
        {
            var cameraItems = new List<CameraItem>();
            foreach (var item in cameras)
            {
                cameraItems.Add(new CameraItem { Camera = item, IsCurrent = item == FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance.CurrentCamera });
            }
            CameraGrid.ItemsSource = cameraItems;
            var index = -1;
            if (cameraItems.Count > 1)
            {
                var item = cameraItems.First(c => !c.IsCurrent);
                index = cameraItems.IndexOf(item);
            }
            CameraGrid.SelectedIndex = index;
            BtnRefresh.IsEnabled = true;
            BtnOK.IsEnabled = cameraItems.Any(c => !c.IsCurrent);
        }

        private void OnRefresh_Click(object sender, RoutedEventArgs e)
        {
            StartRefresh();
        }

        public async void StartRefresh()
        {
            var dispather = this.Dispatcher;
            dispather.Invoke(() =>
            {
                BtnOK.IsEnabled = false;
                BtnRefresh.IsEnabled = false;
            });
            await Task.Run(() =>
            {
                var cameras = FilterWheelShared.DeviceDataService.ThorlabsCamera.ListCameras(FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance.CurrentCamera);
                dispather.Invoke(() =>
                {
                    UpdateSource(cameras);
                });
            });
        }
    }
}
