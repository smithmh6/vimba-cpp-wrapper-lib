using Prism.Mvvm;
using System.Text;
using FilterWheelShared.Common;

namespace FilterWheel.ViewModel
{
    public class HelpWindowViewModel : BindableBase
    {
        public string CurrentVersion
        {
            get
            {
                return ThorlabsProduct.Version;
            }
        }
        public string ProductLongDisplayName
        {
            get
            {
                return ThorlabsProduct.ProductLongDisplayName;
            }
        }
        public string ProductInfoUrl
        {
            get
            {
                return ThorlabsProduct.ProductInfoUrl;
            }
        }

        private string _additionalInfo;
        public string AdditionalInfo
        {
            get => _additionalInfo;
            set => SetProperty(ref _additionalInfo, value);
        }

        public void Refresh()
        {
            var camInstance = FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance;
            var str = new StringBuilder();
            if (camInstance.IsCameraConnected && camInstance.CurrentCamera != null)
            {
                str.AppendLine("Name : " + camInstance.CurrentCamera.ModelName);
                str.AppendLine("S/N : " + camInstance.CurrentCamera.SerialNumber);
                str.AppendLine("Model : " + camInstance.CurrentCamera.ModelName);
                str.AppendLine("Firmware : " + camInstance.FirmwareVersion);
            }
            AdditionalInfo = str.ToString();
        }
    }
}

