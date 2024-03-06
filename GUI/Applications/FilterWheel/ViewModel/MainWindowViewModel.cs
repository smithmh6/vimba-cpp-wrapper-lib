using Microsoft.Win32;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FilterWheel.Infrastructure;
using FilterWheel.Localization;
using FilterWheel.View;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using FilterWheelShared.Localization;
using FilterWheelShared.Logger;
using ThorLogWrapper;
using DelegateCommand = Prism.Commands.DelegateCommand;
using System.Threading.Tasks;

namespace FilterWheel.ViewModel
{
    public class MainWindowViewModel : BindableBase
    {
        public ICommand ConnectionCommand { get; private set; }
        public ICommand LoadedCommand { get; private set; }
        public DelegateCommand SWOptionsCommand { get; private set; }
        public DelegateCommand SWUpdateCommand { get; private set; }
        public DelegateCommand SupportCommand { get; private set; }
        public DelegateCommand HelpCommand { get; private set; }
        public ICommand ClosingCommand { get; private set; }
        public DelegateCommand LoadConfigCommand { get; private set; }
        public DelegateCommand LoadImageCommand { get; private set; }
        public DelegateCommand SaveConfigCommand { get; private set; }


        private readonly IEventAggregator eventAggregator;
        private readonly IContainerExtension container;

        private string headerstring;
        public string HeaderString
        {
            get { return headerstring; }
            set { SetProperty(ref headerstring, value); }
        }

        private object buttonimagesource;
        public object ButtonImageSource
        {
            get { return buttonimagesource; }
            set { SetProperty(ref buttonimagesource, value); }
        }

        private ProgramStatus _status = new ProgramStatus();
        public ProgramStatus ProgStatus
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private int _zoom = 100;
        public int Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        private string _location;
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private PixelInfo _pixelColorInfo = new PixelInfo() { IsMono = false, ColorInfo = string.Empty };
        public PixelInfo PixelColorInfo
        {
            get => _pixelColorInfo;
            set => SetProperty(ref _pixelColorInfo, value);
        }

        public double SensorPixelWidth
        {
            get { return (double)(DisplayService.Instance.PhysicalWidthPerPixel / DisplayService.Instance.TargetObjective); }
        }

        public double SensorPixelHeight
        {
            get { return (double)(DisplayService.Instance.PhysicalHeightPerPixel / DisplayService.Instance.TargetObjective); }
        }

        private readonly ObservableCollection<RecentItem> _recentFiles = new ObservableCollection<RecentItem>();
        public ObservableCollection<RecentItem> RecentFiles => _recentFiles;

        public MainWindowViewModel(IContainerExtension container, IEventAggregator eventAggregator)
        {
            this.container = container;
            this.eventAggregator = eventAggregator;

            ConnectionCommand = new DelegateCommand(ConnectionCommandExecute);
            SWOptionsCommand = new DelegateCommand(SWOptionsCommandExecute, GenralCanExecute);
            SWUpdateCommand = new DelegateCommand(SWUpdateCommandExecute);
            SupportCommand = new DelegateCommand(SupportCommandExecute);
            HelpCommand = new DelegateCommand(HelpCommandExecute);
            ClosingCommand = new DelegateCommand(ClosingCommandExecute);
            LoadedCommand = new DelegateCommand(LoadedExecute);

            LoadConfigCommand = new DelegateCommand(LoadConfigExecute, GenralCanExecute);
            LoadImageCommand = new DelegateCommand(LoadImageExecute, LoadImageCanExecute);

            SaveConfigCommand = new DelegateCommand(SaveConfigExecute, GenralCanExecute);

            eventAggregator.GetEvent<SavedImageEvent>().Subscribe(OnImageSaved, ThreadOption.UIThread);
            eventAggregator.GetEvent<PopupWindowEvent>().Subscribe(OnPopupWindow, ThreadOption.UIThread);
            eventAggregator.GetEvent<ThorCamStatusEvent>().Subscribe(OnThorCamStatusChanged);
            eventAggregator.GetEvent<UpdatePixelDataEvent>().Subscribe(OnUpdatePixelData);
            eventAggregator.GetEvent<CoordinateZoomChangedEvent>().Subscribe(OnCoordinateZoomChanged);
            //eventAggregator.GetEvent<AutoExposureEvent>().Subscribe(OnAutoExposure);

            //ThorlabsCamera.Instance.CallbackFrameEvent += Instance_CallbackFrameEvent;
            ThorlabsCamera.Instance.CameraFPSEvent += Instance_CameraFPSEvent;
            ThorlabsCamera.Instance.MemoryOverflowEvent += Instance_MemoryRunoutEvent;
            ThorlabsCamera.Instance.SlotChangingEvent += Instance_SlotChangingEvent;
            ReconnectService.PreReconnect += ReconnectService_PreReconnect;
            ReconnectService.PostReconnect += ReconnectService_PostReconnect;
            LocalizationManager.GetInstance().LanguageChangedEvent += LanguageChangedEvent;
            DisplayService.Instance.UpdateSizeRatio += Instance_UpdateSizeRatio;            
            //LoadRecent();
            LoadDefaultConfig();
        }

        private void Instance_SlotChangingEvent(object sender, SlotChangingEventArg arg)
        {
            if (arg.IsJogging)
            {
                var count = DisplayService.Instance.Slots.Count;
                var slotIndex = arg.SlotIndex + 1;
                if (slotIndex >= count)
                    slotIndex -= count;

                ProgStatus.StatusMessage = $"Jogging to {DisplayService.Instance.Slots[slotIndex].SlotName}...";
                ProgStatus.StatusEnum = Status.Busy;
            }
            else
            {
                ProgStatus.StatusMessage = DisplayService.Instance.Slots[arg.SlotIndex].SlotName;
                ProgStatus.StatusEnum = Status.Busy;
            }
        }

        private void LanguageChangedEvent(object sender, LanguageChangedEventArgs e)
        {
            ProgStatus.StatusMessage = string.Empty;
            ProgStatus.StatusEnum = Status.Ready;
        }

        private void Instance_MemoryRunoutEvent(object sender, EventArgs e)
        {
            PopupUtils.Confirm(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.MemOverflow), OnConfirmClosed);
        }

        private async void OnConfirmClosed(object sender, Telerik.Windows.Controls.WindowClosedEventArgs e)
        {
            if (e.DialogResult == true)
            {
                if (CaptureService.Instance.IsCapturing)
                    await Task.Run(() => CaptureService.Instance.StopCapture());
            }
        }

        private void ReconnectService_PostReconnect(PostReconnectEventParam eventParam)
        {
            if (eventParam.IsSuccessful)
            {
                ThorLogger.Log("Camera reconnect successfully.", ThorLogLevel.Info);
            }
            UpdateHeader();
        }

        private void ReconnectService_PreReconnect(PreReconnectEventParam eventParam)
        {
            UpdateHeader();
        }

        private bool GenralCanExecute()
        {
            return ThorlabsCamera.Instance.IsCameraConnected && !CaptureService.Instance.IsCapturing;
        }

        private void SaveConfigExecute()
        {
            var localizationInstance = LocalizationService.GetInstance();
            var dialog = new SaveFileDialog()
            {
                Title = LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.SaveConfig),
                Filter = "JSON(*.json)| *.json"
            };
            if (dialog.ShowDialog() == true)
            {
                var file = dialog.FileName;

                var msg = localizationInstance.GetLocalizationString(ShellLocalizationKey.SaveProfileSucceeded);
                var isSucceed = false;
                try
                {
                    var slots = DisplayService.Instance.Slots;
                    ConfigService.Instance.SaveJsonFile(file, slots);
                    isSucceed = true;
                }
                catch (Exception e)
                {
                    msg = localizationInstance.GetLocalizationString(ShellLocalizationKey.SaveProfileFailed);
                }
                finally
                {
                    ProgStatus.StatusMessage = msg;
                    ProgStatus.StatusEnum = isSucceed ? Status.Normal : Status.Warning;
                    if (!isSucceed)
                    {
                        PopupUtils.Alert(msg, localizationInstance.GetLocalizationString(ShellLocalizationKey.Warning));
                    }
                }
            }
        }

        //private void CombineExecute()
        //{
        //    var files = RecentFiles.Select(r => r.FilePath).ToList();
        //    var combineWindowViewModel = new CombineWindowViewModel(files);
        //    var combineWindow = new CombineWindow
        //    {
        //        Owner = Application.Current?.MainWindow,
        //        DataContext = combineWindowViewModel,
        //        WindowStartupLocation = WindowStartupLocation.CenterOwner
        //    };
        //    if (combineWindow.ShowDialog() == true)
        //    {
        //        var selectedImages = combineWindowViewModel.Images.Where(item => item.IsSelected).Select(item => item.SavedImage);

        //        string combineFilePath = CaptureService.Instance.SaveFilePath;

        //        ImageData resultImageData16u = null;

        //        var combineCount = selectedImages.Count();
        //        double combine_m = 1.0 / combineCount;
        //        double[] m_values = new double[3] { combine_m, combine_m, combine_m };

        //        sbyte status = 0;

        //        try
        //        {
        //            foreach (var image in selectedImages)
        //            {
        //                uint imageCount = 0;
        //                uint validBits = 0;
        //                int fileHandle = -1;
        //                var p2dImgHdl = ImageData.LoadImage(image.FileFullName, ref fileHandle, ref imageCount, ref validBits, true);
        //                if (p2dImgHdl < 0)
        //                    throw new Exception($"Load image data failed! File path : {image.FileFullName}.");

        //                var imageData = new ImageData(p2dImgHdl);
        //                var imageInfo = imageData.DataInfo;

        //                try
        //                {
        //                    if (resultImageData16u == null)
        //                    {
        //                        resultImageData16u = new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_16U, 16, imageInfo.channels);
        //                    }

        //                    if (imageInfo.pix_type != P2dDataFormat.P2D_16U)
        //                    {
        //                        ImageData temp16u = null;
        //                        try
        //                        {
        //                            status = imageData.ConvertTo(P2dDataFormat.P2D_16U, 16, out temp16u);
        //                            if (status != 0)
        //                                throw new Exception("Convert from 8-bit to 16-bit failed.");
        //                            status = P2DWrapper.AddToImage(temp16u.Hdl, resultImageData16u.Hdl);
        //                            if (status != 0)
        //                                throw new Exception("Add image to combine failed.");
        //                        }
        //                        finally
        //                        {
        //                            temp16u?.Dispose();
        //                        }
        //                    }
        //                    else
        //                    {
        //                        status = P2DWrapper.AddToImage(imageData.Hdl, resultImageData16u.Hdl);
        //                        if (status != 0)
        //                            throw new Exception("Add image to combine failed.");
        //                    }
        //                }
        //                finally
        //                {
        //                    imageData.Dispose();
        //                }
        //            }

        //            status = P2DWrapper.Multiply(resultImageData16u.Hdl, m_values);
        //            if (status != 0)
        //                throw new Exception("Myltiply failed.");

        //            status = resultImageData16u.ConvertTo(P2dDataFormat.P2D_8U, 8, out ImageData resultImageData);
        //            if (status != 0)
        //                throw new Exception("Convert from 16-bit to 8-bit failed.");

        //            ThorlabsCamera.Instance.OnLoadImage(resultImageData.Hdl);
        //        }
        //        catch (Exception e)
        //        {
        //            //show warning messagebox
        //        }
        //        finally
        //        {
        //            resultImageData16u?.Dispose();
        //        }
        //    }
        //}

        private void OnCoordinateZoomChanged(double zoom)
        {
            if (zoom < 0 || double.IsNaN(zoom)) return;
            Zoom = (int)(zoom * 100);
        }

        //private void OnAutoExposure(AutoExposureEventArgs e)
        //{
        //    if (!e.IsStable)
        //    {
        //        var message = ProgStatus.StatusMessage;
        //        ProgStatus = new ProgramStatus
        //        {
        //            StatusMessage = message,
        //            StatusEnum = Status.Warning,
        //        };
        //    }
        //}

        private void OnUpdatePixelData(PixelData data)
        {
            Application.Current?.Dispatcher.InvokeAsync(new Action(() =>
            {
                Location = $"X : {data.Location.XValue}    Y : {data.Location.YValue}";

                if (data is PixelDataMono dataMono)
                {
                    PixelColorInfo = new PixelInfo() { IsMono = true, ColorInfo = $"{dataMono.Mono}" }; //$"{localizationService.GetLocalizationString(ShellLocalizationKey.Intensity)} : {dataMono.Mono}";
                    return;
                }
                if (data is PixelDataRGB dataRGB)
                {
                    var colorStr = string.Empty;
                    if (dataRGB.R != null)
                        colorStr += $"R : {dataRGB.R.Value}";
                    if (dataRGB.G != null)
                        colorStr += $"    G : {dataRGB.G.Value}";
                    if (dataRGB.B != null)
                        colorStr += $"    B : {dataRGB.B.Value}";

                    PixelColorInfo = new PixelInfo() { IsMono = false, ColorInfo = colorStr };
                    return;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnThorCamStatusChanged(ThorCamStatusEventArgs args)
        {
            switch (args.Status)
            {
                case ThorCamStatus.None:
                    ProgStatus.StatusMessage = string.Empty;
                    ProgStatus.StatusEnum = Status.Ready;
                    break;
                case ThorCamStatus.Error:
                    {
                        var localizationService = LocalizationService.GetInstance();
                        var msg = args.Except.Message;
                        switch (args.ErrorType)
                        {
                            case ErrorType.None:
                                break;
                            case ErrorType.LoadImageFailed:
                                msg = $"{localizationService.GetLocalizationString(ShellLocalizationKey.LoadImage)} {args.Except.Message} {localizationService.GetLocalizationString(ShellLocalizationKey.Failed)}.";
                                break;
                            case ErrorType.SaveDirectoryError:
                                msg = $"{localizationService.GetLocalizationString(ShellLocalizationKey.SavePath)} {args.Except.Message} {localizationService.GetLocalizationString(ShellLocalizationKey.Invalid)}.";
                                break;
                            case ErrorType.IntervalError:
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.IntervalError), args.Except.Message);
                                break;
                            case ErrorType.InternalTriggerError:
                                var splitsInternal = args.Except.Message.Split(',');
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.InternalTriggerError), splitsInternal[0], splitsInternal[1]);
                                break;
                            case ErrorType.OpenCameraFailed:
                                var splitsOpen = args.Except.Message.Split(',');
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.CameraOpenFailed), splitsOpen[0], splitsOpen[1]);
                                break;
                            case ErrorType.SetAutoExposureFailed:
                                msg = localizationService.GetLocalizationString(ShellLocalizationKey.SetAutoExpFailed);
                                break;
                            case ErrorType.StartPreviewFailed:
                                msg = localizationService.GetLocalizationString(ShellLocalizationKey.StartPreFailed);
                                break;
                            case ErrorType.StopPreviewFailed:
                                msg = localizationService.GetLocalizationString(ShellLocalizationKey.StopPreFailed);
                                break;
                            case ErrorType.SaveDirectoryAccess:
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.SavePathError), args.Except.Message);
                                break;
                            case ErrorType.ExportFileFailed:
                                var splitsExport = args.Except.Message.Split(',');
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.ExportFailed), splitsExport[0], splitsExport[1]);
                                break;
                            case ErrorType.FileOccupied:
                                msg = string.Format(localizationService.GetLocalizationString(ShellLocalizationKey.FileOccupied), args.Except.Message);
                                break;
                        }
                        ProgStatus.StatusMessage = msg;
                        ProgStatus.StatusEnum = Status.Error;
                        PopupUtils.Alert(msg);
                    }
                    return;
                case ThorCamStatus.Living:
                    if (args.Except is not JoggingToLivingException)
                        ProgStatus.StatusMessage = "0.0";
                    ProgStatus.StatusEnum = Status.Busy;
                    break;
                case ThorCamStatus.Capturing:
                    ProgStatus.StatusMessage = DisplayService.Instance.Slots[DisplayService.Instance.CurrentSlotIndex].SlotName;
                    ProgStatus.StatusEnum = Status.Busy;
                    break;
                case ThorCamStatus.Loaded:
                    ProgStatus.StatusMessage = string.Format(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.LoadImageSuccess), args.Except.Message);
                    ProgStatus.StatusEnum = Status.Normal;
                    AddRecent(args.Except.Message);
                    break;
                case ThorCamStatus.Saved:
                    ProgStatus.StatusMessage = args.Except.Message;
                    ProgStatus.StatusEnum = Status.Normal;
                    break;
                case ThorCamStatus.Export:
                    var exports = args.Except.Message.Split(',');
                    ProgStatus.StatusMessage = string.Format(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.ExportSuccess), exports[0], exports[1]);
                    ProgStatus.StatusEnum = Status.Normal;
                    break;
                case ThorCamStatus.Jogging:
                    ProgStatus.StatusMessage = $"Jogging to {CaptureService.Instance.JoggingTargetName}...";
                    ProgStatus.StatusEnum = Status.Busy;
                    break;
                default:
                    break;
            }
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                LoadConfigCommand.RaiseCanExecuteChanged();
                LoadImageCommand.RaiseCanExecuteChanged();
                //CombineCommand.RaiseCanExecuteChanged();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Instance_CameraFPSEvent(object sender, double? e)
        {
            if (e == null) return;
            if (CaptureService.Instance.IsLiving)
            {
                if (CaptureService.Instance.IsJogging)
                {
                    ProgStatus.StatusMessage = $"{e.Value.ToString("f1")} Jogging to {CaptureService.Instance.JoggingTargetName}...";
                    ProgStatus.StatusEnum = Status.Busy;
                    return;
                }
                ProgStatus.StatusMessage = e.Value.ToString("f1");
                ProgStatus.StatusEnum = Status.Busy;
            }
        }

        //private void Instance_CallbackFrameEvent(object sender, uint e)
        //{
        //    Application.Current?.Dispatcher.InvokeAsync(() =>
        //    {
        //        if (CaptureService.Instance.IsCapturing)
        //        {
        //            ProgStatus = new ProgramStatus
        //            {
        //                //StatusMessage = $"{e} / {((!CaptureService.Instance.IsAutoExposure && CaptureService.Instance.IsTimeSeries) ? CaptureService.Instance.Frames : 1)}",
        //                StatusEnum = Status.Normal
        //            };
        //        }
        //    }, System.Windows.Threading.DispatcherPriority.Input);
        //}

        private void Instance_UpdateSizeRatio(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(SensorPixelHeight));
            RaisePropertyChanged(nameof(SensorPixelWidth));
        }

        private Viewport.RulerConfigurationWindow _rulerConfigWindow;
        private void OnPopupWindow(PopupWindowKey key)
        {
            if (key == PopupWindowKey.RulerConfigWindowKey)
            {
                if (_rulerConfigWindow == null)
                {
                    _rulerConfigWindow = new Viewport.RulerConfigurationWindow()
                    {
                        Owner = Application.Current?.MainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                }
                _rulerConfigWindow.ShowDialog();
            }
        }

        private void AddRecent(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath);
            if (ext != ".jpg" && ext != ".tif" && ext != ".tiff")
                return;

            var found = RecentFiles.FirstOrDefault(r => r.FilePath == filePath);
            if (found != null)
            {
                RecentFiles.Remove(found);
            }

            var length = RecentFiles.Count;
            while (length >= 10)
            {
                RecentFiles.RemoveAt(length - 1);
                length = RecentFiles.Count;
            }
            RecentFiles.Insert(0, new RecentItem(filePath));
        }

        private void OnImageSaved(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return;

            ProgStatus.StatusMessage = string.Format(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.SaveImageSuccess), filePath);
            ProgStatus.StatusEnum = Status.Normal;

            AddRecent(filePath);
        }

        private void LoadRecent()
        {
            //var settingFolder = ThorlabsProduct.ApplitaionSettingDir;
            //if (!System.IO.Directory.Exists(settingFolder))
            //{
            //    System.IO.Directory.CreateDirectory(settingFolder);
            //}
            //if (!System.IO.File.Exists(ThorlabsProduct.ThorImageCamHistoriesPath))
            //    return;

            //try
            //{
            //    var json = System.IO.File.ReadAllText(ThorlabsProduct.ThorImageCamHistoriesPath);
            //    var items = JsonSerializer.Deserialize<List<RecentItem>>(json);
            //    foreach (var item in items)
            //    {
            //        if (System.IO.File.Exists(item.FilePath))
            //        {
            //            RecentFiles.Add(item);
            //        }
            //    }
            //}
            //catch (Exception e)
            //{
            //    ;
            //}

            ConfigService.Instance.LoadRecentFiles(ThorlabsProduct.ThorImageCamHistoriesPath, RecentFiles);
        }

        private void SaveRecent()
        {
            //try
            //{
            //    string json = JsonSerializer.Serialize(RecentFiles);
            //    System.IO.File.WriteAllText(ThorlabsProduct.ThorImageCamHistoriesPath, json);
            //}
            //catch (Exception e)
            //{
            //    ;
            //}

            ConfigService.Instance.SaveRecentFiles(ThorlabsProduct.ThorImageCamHistoriesPath, RecentFiles);
        }

        private void UpdateHeader()
        {
            if (!ThorlabsCamera.Instance.IsCameraConnected)
            {
                HeaderString = ThorlabsProduct.ProductLongDisplayName;
                return;
            }
            //var splits = ThorlabsCamera.Instance.FirmwareVersion.Split("\r\n");
            //HeaderString = $"{ThorlabsCamera.Instance.CurrentCamera} FW {splits[0]}";
            HeaderString = $"{ThorlabsCamera.Instance.CurrentCamera}";
        }

        private void LoadedExecute()
        {
            UpdateHeader();
            ButtonImageSource = Application.Current.FindResource("Link_OutlineDrawingImage");
        }

        private void ClosingCommandExecute()
        {
            try
            {
                var slotsBackup = DisplayService.Instance.SlotsBackup;
                ConfigService.Instance.SaveJsonFile(ThorlabsProduct.ThorImageCamSttingsPath, slotsBackup);
            }
            catch (Exception e)
            {

            }

            SaveRecent();
        }

        private void LoadDefaultConfig()
        {
            try
            {
                LoadAndParseConfig(ThorlabsProduct.ThorImageCamSttingsPath);                
                DisplayService.Instance.InitBackupSlots();
            }
            catch (Exception e)
            {

            }
        }

        private async void ConnectionCommandExecute()
        {
            var window = container.Resolve<SelectSingleDeviceWindow>();
            window.Owner = Application.Current.MainWindow;
            window.StartRefresh();
            var result = window.ShowDialog();
            if (result == true)
            {
                //ThorlabsCamera.Instance.ResetCurrentCamera();
                ImageLoadService.Instance.Close();

                //await Task.Run(() =>
                //{
                //    //ImageLoadService.Instance.Close();
                //    //ThorLogger.Log("Stop C#", ThorLogLevel.Error);
                //    //CaptureService.Instance.StopLive();
                //    //ThorLogger.Log("Close C#", ThorLogLevel.Error);
                //    //ThorlabsCamera.Instance.CloseCamera();
                //    //ThorLogger.Log("Open C#", ThorLogLevel.Error);
                //    //ThorlabsCamera.Instance.OpenCamera(window.SelectedCamera);
                //    //UpdateHeader();
                //}).ConfigureAwait(false);

                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    //System.Threading.Thread.Sleep(100);
                    //ThorLogger.Log("Stop C#", ThorLogLevel.Error);
                    CaptureService.Instance.Reset();

                    //ThorlabsCamera.Instance.ResetCurrentCamera();
                    //ImageLoadService.Instance.Close();

                    //ThorLogger.Log("Close C#", ThorLogLevel.Error);
                    ThorlabsCamera.Instance.CloseCamera();
                    //ThorLogger.Log("Open C#", ThorLogLevel.Error);
                    ThorlabsCamera.Instance.OpenCamera(window.SelectedCamera);
                    CaptureService.SetCurrentSlotParasIntoCam(DisplayService.Instance.CurrentSlotIndex, true);
                    UpdateHeader();
                }));
            }
        }

        private OptionWindow _optionWindow = null;
        private void SWOptionsCommandExecute()
        {
            if (_optionWindow == null)
                _optionWindow = container.Resolve<OptionWindow>();
            _optionWindow.Owner = Application.Current.MainWindow;
            _optionWindow.Update();
            _optionWindow.ShowDialog();
        }

        private UpdateWindow _updateWindow = null;
        private void SWUpdateCommandExecute()
        {
            if (_updateWindow == null)
                _updateWindow = container.Resolve<UpdateWindow>();
            _updateWindow.Owner = Application.Current.MainWindow;
            _updateWindow.ShowDialog();
        }

        private SupportWindow _supportWindow = null;
        private void SupportCommandExecute()
        {
            var viewIsNull = _supportWindow == null;
            if (viewIsNull)
                _supportWindow = container.Resolve<SupportWindow>();
            _supportWindow.Owner = Application.Current.MainWindow;
            _supportWindow.Refresh();
            _supportWindow.Show();
            if (!viewIsNull)
            {
                _supportWindow.IsTopmost = true;
                _supportWindow.IsTopmost = false;
            }
        }

        private HelpWindow _helpWindow = null;
        private void HelpCommandExecute()
        {
            if (_helpWindow == null)
                _helpWindow = container.Resolve<HelpWindow>();
            _helpWindow.Owner = Application.Current.MainWindow;
            _helpWindow.Refresh();
            _helpWindow.ShowDialog();
        }

        private void LoadConfigExecute()
        {
            if (CaptureService.Instance.IsLiving)
            {
                CaptureService.Instance.StopLive();
            }
            var filter = "JSON(*.json)| *.json";
            var dialog = new OpenFileDialog()
            {
                Title = LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.LoadConfig),
                Multiselect = false,
                Filter = filter
            };
            if (dialog.ShowDialog() == true)
            {
                var file = dialog.FileName;
                LoadConfig(file);
            }
        }

        private bool LoadConfigCanExecute()
        {
            return ThorlabsCamera.Instance.IsCameraConnected;
        }

        private void LoadImageExecute()
        {
            if (CaptureService.Instance.IsLiving)
            {
                CaptureService.Instance.StopLive();
            }
            var filter = "TIF(*.tiff; *.tif)| *.tiff; *.tif|JPEG(*.jpg)|*.jpg";
            var dialog = new OpenFileDialog()
            {
                Title = LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.LoadImage),
                Multiselect = false,
                Filter = filter
            };
            if (dialog.ShowDialog() == true)
            {
                var file = dialog.FileName;
                ImageLoadService.Instance.LoadImage(file);
            }
        }

        private bool LoadImageCanExecute()
        {
            return !CaptureService.Instance.IsCapturing;
        }

        private void LoadAndParseConfig(string file)
        {
            var slots = ConfigService.Instance.LoadJsonFile<ObservableCollection<Slot>>(file);
            DisplayService.Instance.ParseConfiguration(slots);
            CaptureService.SetCurrentSlotParasIntoCam(DisplayService.Instance.CurrentSlotIndex);
            eventAggregator.GetEvent<FilterWheelShared.Event.UpdateSlotSelectedIndexEvent>().Publish(DisplayService.Instance.CurrentSlotIndex);
        }

        private void LoadConfig(string file)
        {
            var localizationInstance = LocalizationService.GetInstance();
            var msg = localizationInstance.GetLocalizationString(ShellLocalizationKey.LoadProfileSucceeded);
            var isSucceed = false;
            try
            {
                LoadAndParseConfig(file);

                //TO DO
                //for capture service to set current exposure and gain

                isSucceed = true;
            }
            catch (Exception e)
            {
                msg = localizationInstance.GetLocalizationString(ShellLocalizationKey.LoadProfileFailed);
            }
            finally
            {
                var statusMsg = isSucceed ? localizationInstance.GetLocalizationString(ShellLocalizationKey.LoadProfileSucceeded) :
                    localizationInstance.GetLocalizationString(ShellLocalizationKey.LoadProfileFailed);

                ProgStatus = new ProgramStatus
                {
                    StatusMessage = statusMsg,
                    StatusEnum = isSucceed ? Status.Normal : Status.Warning
                };
                if (!isSucceed)
                {
                    PopupUtils.Alert(msg, localizationInstance.GetLocalizationString(ShellLocalizationKey.Warning));
                }
            }
        }
    }
}
