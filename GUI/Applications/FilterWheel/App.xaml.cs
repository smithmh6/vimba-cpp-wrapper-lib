using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Controls;
using FilterWheel.Infrastructure;
using FilterWheel.Localization;
using FilterWheel.Properties;
using FilterWheel.View;
using FilterWheelShared.Common;
using LocalizationManager = FilterWheelShared.Localization.LocalizationManager;

namespace FilterWheel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        public App()
        {
            CultureInfo.CurrentCulture =
                CultureInfo.CurrentUICulture =
                CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (SingleInstance.AlreadyRunning())
            {
                Current.Shutdown();
                return;
            }

            FilterWheelShared.Logger.ThorLogger.UpdateLogPath(null);
            DispatcherUnhandledException += (s, ee) =>
            {
                FilterWheelShared.Logger.ThorLogger.Log(ee.Exception.Message + "\n" + ee.Exception.StackTrace, ThorLogWrapper.ThorLogLevel.Error);
                MessageBox.Show(ee.Exception.Message, "Error");
            };
            SharedUtils.UpdateAppStartupTimeStamp();
            base.OnStartup(e);
        }

        #region override prism method

        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType =>
            {
                var viewName = viewType.FullName;
                viewName = viewName.Replace(".View.", ".ViewModel.");
                var suffix = viewName.EndsWith("View") ? "Model" : "ViewModel";
                var viewModelName = String.Format(CultureInfo.InvariantCulture, "{0}{1}", viewName, suffix);
                var assembly = viewType.Assembly;
                var type = assembly.GetType(viewModelName, true);
                return type;
            });
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            return new DirectoryModuleCatalog() { ModulePath = @".\Modules" };
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var localizationServiceManager = LocalizationManager.GetInstance();
            localizationServiceManager.AddListener(LocalizationService.GetInstance());
            if (string.IsNullOrEmpty(FilterWheel.Properties.Settings.Default.Language))
            {
                FilterWheel.Properties.Settings.Default.Language = localizationServiceManager.CurrentLanguage;
                FilterWheel.Properties.Settings.Default.Save();
            }
            if (FilterWheel.Properties.Settings.Default.Language != localizationServiceManager.CurrentLanguage)
            {
                try
                {
                    localizationServiceManager.SetLanguage(FilterWheel.Properties.Settings.Default.Language);
                }
                catch
                {
                    localizationServiceManager.SetLanguage(localizationServiceManager.Languages[0]);
                    FilterWheel.Properties.Settings.Default.Language = localizationServiceManager.CurrentLanguage;
                    FilterWheel.Properties.Settings.Default.Save();
                }
            }
            containerRegistry.RegisterInstance(typeof(LocalizationManager), localizationServiceManager);
            FilterWheelShared.DeviceDataService.ReconnectService.InjectConnectWindow(new ReconnectWindow());
            InitTelerikTheme();
        }

        private Thread ThreadSplash(ManualResetEventSlim resetEventSlim)
        {
            Thread thread = new(new ThreadStart(() =>
            {
                var splash = new View.SplashScreen();
                splash.WindowState = WindowState.Normal;
                splash.Show();
                splash.Activate();
                splash.Topmost = true;
                splash.Topmost = false;
                splash.Focus();
                Task.Run(() =>
                {
                    resetEventSlim.Wait();
                    splash.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(splash.Close));
                    splash.Dispatcher.InvokeShutdown();
                });

                System.Windows.Threading.Dispatcher.Run();
            }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return thread;
        }   

        protected override Window CreateShell()
        {
            var capIns = FilterWheelShared.DeviceDataService.CaptureService.Instance;

            ManualResetEventSlim manualEvent = new ManualResetEventSlim(false);
            Thread threadSplash = ThreadSplash(manualEvent);

            var cameraList = FilterWheelShared.DeviceDataService.ThorlabsCamera.ListCameras();
            var cameraInstance = FilterWheelShared.DeviceDataService.ThorlabsCamera.Instance;

            CameraInfo selected_camera = null;
            if (cameraList.Count == 1)
            {
                selected_camera = cameraList[0];
            }
            else
            {
                manualEvent.Set();
                Thread thread = new(new ThreadStart(() =>
                {
                    var window = Container.Resolve<SelectSingleDeviceWindow>();
                    window.Init(cameraList);
                    window.WindowState = WindowState.Normal;
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    window.IsTopmost = true;
                    window.Activated += (s, e) => window.IsTopmost = false;
                    if (window.ShowDialog() == true)
                    {
                        selected_camera = window.SelectedCamera;
                    }
                    else
                    {
                        FilterWheelShared.DeviceDataService.CameraLIbCommand.Release();
                        Environment.Exit(0);
                    }
                    window.Dispatcher.InvokeShutdown();
                }));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
                threadSplash.Join();
                manualEvent = new ManualResetEventSlim(false);
                threadSplash = ThreadSplash(manualEvent);
            }

            if (selected_camera == null)
                return null;

            ThorColorService.Init("ComputeColor", $"{ThorlabsProduct.LocalApplicationDataDir}CustomColor");
            // call this line to create instances for all vms under Output\Modules\MVM           

            var result = cameraInstance.OpenCamera(selected_camera);
            if (!result)
            {
                manualEvent.Set();
                threadSplash.Join();
                cameraInstance.CloseCamera();
                var msg = string.Format(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.CameraOpenFailed), selected_camera.ModelName, selected_camera.SerialNumber);
                PopupUtils.AlertWithoutMain(msg, null, () => Environment.Exit(-1));
            }
             


            var mainwindow = Container.Resolve<MainWindow>();
            mainwindow.Show();
            var main = mainwindow.ParentOfType<Window>();
            Current.MainWindow = main;
            manualEvent.Set();
            ViewModelLocator.SetAutoWireViewModel(main, false);
            threadSplash.Join();
            main.Activate();
            main.Topmost = true;
            main.Topmost = false;
            main.Focus();
            return main;
        }
        protected override void InitializeModules()
        {
            try
            {
                base.InitializeModules();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
                return;
            }

        }
        #endregion
        private void InitTelerikTheme()
        {
            // Set customized palette 
            ApplicationTheme.SetFontFamily();
            ApplicationTheme.SetColorStyle(FilterWheel.Properties.Settings.Default.ColorStyle == ThorlabsProduct.DarkThemeName ? 1 : 0);
        }
    }
}
