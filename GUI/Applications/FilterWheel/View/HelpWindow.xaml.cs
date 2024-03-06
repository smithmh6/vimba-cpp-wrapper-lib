using System;
using System.Diagnostics;
using System.IO;
using Telerik.Windows.Controls;
using FilterWheel.ViewModel;
using FilterWheelShared.Common;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class HelpWindow : RadWindow
    {
#if DEBUG
        private const string manualFile = @"..\..\SDK\doc\Device SDK Manual.pdf";
        private const string cppSDKFolder = @"..\..\SDK\SampleCode\Thorlabs_Device_C++SDK\";
        private const string labViewSDKFolder = @"..\..\SDK\SampleCode\Thorlabs_Device_LabVIEWSDK\";
        private const string pythonSDKFolder = @"..\..\SDK\Thorlabs_Device_PythonSDK\";
        private const string licenseFolder = @"..\..\License\";
#else
        private const string manualFile = @"..\Sample\Device SDK Manual.pdf";
        private const string cppSDKFolder = @"..\Sample\Thorlabs_Device_C++SDK\";
        private const string labViewSDKFolder = @"..\Sample\Thorlabs_Device_LabVIEWSDK\";
        private const string pythonSDKFolder = @"..\Sample\Thorlabs_Device_PythonSDK\";
        private const string licenseFolder = @"..\License\";
#endif
        public HelpWindow()
        {
            InitializeComponent();
            CopyRightTB.Text = ThorlabsProduct.CopyRight;
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        private void OpenFileFolder(string path)
        {
            Process.Start(new ProcessStartInfo() { UseShellExecute = true, FileName = path });
        }
        private void Hyperlink_OpenFolder(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            string folder = "";
            try
            {
                switch (e.Uri.ToString())
                {
                    case "Manual":
                        OpenFileFolder(AppDomain.CurrentDomain.BaseDirectory + manualFile);
                        break;
                    case "CPPSDK":
                        folder = AppDomain.CurrentDomain.BaseDirectory + cppSDKFolder;
                        if (Directory.Exists(folder))
                            OpenFileFolder(folder);
                        else
                            OpenFileFolder(AppDomain.CurrentDomain.BaseDirectory);
                        break;
                    case "LabViewSDK":
                        folder = AppDomain.CurrentDomain.BaseDirectory + labViewSDKFolder;
                        if (Directory.Exists(folder))
                            OpenFileFolder(folder);
                        else
                            OpenFileFolder(AppDomain.CurrentDomain.BaseDirectory);
                        break;
                    case "PythonSDK":
                        folder = AppDomain.CurrentDomain.BaseDirectory + pythonSDKFolder;
                        if (Directory.Exists(folder))
                            OpenFileFolder(folder);
                        else
                            OpenFileFolder(AppDomain.CurrentDomain.BaseDirectory);
                        break;
                    case "License":
                        folder = AppDomain.CurrentDomain.BaseDirectory + licenseFolder;
                        if (Directory.Exists(folder))
                            OpenFileFolder(folder);
                        else
                            OpenFileFolder(AppDomain.CurrentDomain.BaseDirectory);
                        break;
                }
            }
            catch (Exception)
            {
            }
        }
        public void Refresh()
        {
            (this.DataContext as HelpWindowViewModel).Refresh();
        }
    }
}
