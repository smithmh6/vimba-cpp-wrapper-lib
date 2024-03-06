using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.IO;
using System.Text;
using System.Windows;
using FilterWheelShared.Common;

namespace FilterWheel.ViewModel
{
    public class SupportWindowViewModel : BindableBase
    {
        //public string FirmwareVersion = "Unavailable";
        //public string BootLoaderVersion = "Unavailable";
        //public string HardwareVersion = "Unavailable";
        public string DeviceInfo;

        private string logMessage = "";// = "this a example for showing logs";
        public string LogMessage
        {
            get => logMessage;
            set
            {
                SetProperty(ref logMessage, value);
                CopyCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand CopyCommand { get; private set; }
        public DelegateCommand SaveCommand { get; private set; }

        public SupportWindowViewModel()
        {
            CopyCommand = new DelegateCommand(CopyCommandExecute, CommandCanExecute);
            SaveCommand = new DelegateCommand(SaveCommandExecute, CommandCanExecute);
        }

        public void Refresh()
        {
            LoadDeviceInfo();
            LoadLogContent();
        }

        private void LoadDeviceInfo()
        {
            // Please add your code to get your FirmwareVersion, BootLoaderVersion and HardwareVersion

            var camInstance = FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance;
            var str = new StringBuilder();
            if (camInstance.IsCameraConnected && camInstance.CurrentCamera != null)
            {
                str.AppendLine("Product Name : " + camInstance.CurrentCamera.ModelName);
                str.AppendLine("Serial Number : " + camInstance.CurrentCamera.SerialNumber);
                str.AppendLine("Firmware Version : " + camInstance.FirmwareVersion);
            }
            //str.AppendLine("Bootloader Version : " + BootLoaderVersion);
            //str.AppendLine("Hardware Version : " + HardwareVersion);
            str.AppendLine("Software Version : " + ThorlabsProduct.Version);
            str.AppendLine("------------------------------------------------------------------------------------------------------------------------------");
            DeviceInfo = str.ToString();
            LogMessage = DeviceInfo;
        }

        // Read log file and fill in content to the flowdocument control
        private void LoadLogContent()
        {
            var camInstance = FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance;
            string name = (camInstance.IsCameraConnected && camInstance.CurrentCamera != null) ? $"{camInstance.CurrentCamera.ModelName}-{camInstance.CurrentCamera.SerialNumber}" : "Common";
            string txtLogPath = $"{ThorlabsProduct.LocalApplicationDataDir}{name}-log.txt";

            try
            {
                if (File.Exists(txtLogPath))
                {
                    var content = string.Empty;
                    using (var stream = new FileStream(txtLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader streamReader = new StreamReader(stream, Encoding.Default))
                        {
                            try
                            {
                                content = streamReader.ReadToEnd();
                            }
                            catch
                            { }
                        }
                    }

                    if (string.IsNullOrEmpty(content))
                        return;

                    // Get UTF-8 bytes by reading each byte with ANSI encoding
                    byte[] utf8Bytes = Encoding.Default.GetBytes(content);

                    // Convert UTF-8 bytes to UTF-16 bytes
                    byte[] unicodeBytes = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, utf8Bytes);

                    // Return UTF-16 bytes as UTF-16 string
                    var encodingString = Encoding.Unicode.GetString(unicodeBytes);

                    LogMessage += encodingString;
                }
            }
            catch (Exception e)
            {
            }
        }

        private void CopyCommandExecute()
        {
            Clipboard.SetDataObject(logMessage);
        }

        private void SaveCommandExecute()
        {
            var dialog = new SaveFileDialog() { Filter = "Normal text file|*.txt", FileName = "log" };
            var result = dialog.ShowDialog();
            if (result == true)
            {
                using (var fs = File.OpenWrite(dialog.FileName))
                {
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(logMessage);
                    }
                }
            }
        }

        private bool CommandCanExecute()
        {
            return !string.IsNullOrEmpty(logMessage);
        }


    }
}
