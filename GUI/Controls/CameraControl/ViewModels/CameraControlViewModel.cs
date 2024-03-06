using Prism.Events;
using Prism.Mvvm;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using FilterWheelShared.DeviceDataService;
using DelegateCommand = Prism.Commands.DelegateCommand;
using Telerik.Windows.Controls;
using FilterWheelShared.Event;
using System.IO;
using System.Linq;
using FilterWheelShared.ImageProcess;
using System.Threading.Tasks;
using FilterWheelShared.Common;

namespace CameraControl.ViewModels
{
    public class CameraControlConfig
    {
        public bool LockXY { get; set; }
        public double ExposureTime { get; set; }
        public double Gain { get; set; }
        public int BinX { get; set; }
        public int BinY { get; set; }
        public bool IsManualObjective { get; set; }
        public decimal ObjectiveValue { get; set; }
    }

    public class CollectedData
    {
        public string Name { get; private set; }
        public string FullPath { get; private set; }
        public CollectedData(string fullPath)
        {
            FullPath = fullPath;
            DirectoryInfo dInfo = new DirectoryInfo(fullPath);
            Name = dInfo.Name;
        }
    }


    public class CameraControlViewModel : BindableBase
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();
        private readonly IEventAggregator eventAggregator;
        private ThorlabsCamera _camera = ThorlabsCamera.Instance;

        private CameraControlConfig _config = new CameraControlConfig();
        #region Properties

        public bool IsColorCamera => _camera.IsColorCamera;
        public bool IsPolarCamera => _camera.IsPolarCamera;
        public bool IsColorOrPolarCamera => (IsPolarCamera || IsColorCamera);

        public Tuple<double, double> ExposureTimeRange
        {
            get => new Tuple<double, double>(_camera.ExposureTimeParams.min_value, _camera.ExposureTimeParams.max_value);
        }

        public double ExposureTimeIncrement => _camera.ExposureTimeParams.increment;

        public double ExposureTime
        {
            get => _currentSlot.CurrentSetting.ExposureTime;
            set
            {
                var target = Math.Clamp(value, ExposureTimeRange.Item1, ExposureTimeRange.Item2);
                if (Math.Abs(target - _camera.ExposureTime) < 1e-3)
                    return;
                target= Math.Floor((target - ExposureTimeRange.Item1) / ExposureTimeIncrement) * ExposureTimeIncrement + ExposureTimeRange.Item1;
                _currentSlot.CurrentSetting.ExposureTime = target;
                CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
                //_camera.SetExposureTime(target);
                _settingWindowCallback?.Invoke();
                RaisePropertyChanged(nameof(ExposureTime));
            }
        }

        public bool SupportGain
        {
            get => _camera.SupportGain;
        }

        public Tuple<double, double> GainRange
        {
            get => _camera.GainRange;
        }

        public double Gain
        {
            get => _currentSlot.CurrentSetting.Gain;
            set
            {
                var target = Math.Clamp(value, GainRange.Item1, GainRange.Item2);
                if (Math.Abs(target - _camera.Gain) < 1e-3)
                    return;
                _currentSlot.CurrentSetting.Gain = target;
                CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
                //_camera.SetGain(target);
                _settingWindowCallback?.Invoke();
                RaisePropertyChanged(nameof(Gain));
            }
        }

        private Slot _currentSlot = null;

        public int CurrentSlotIndex
        {
            get => DisplayService.Instance.CurrentSlotIndex;
            set
            {
                DisplayService.Instance.CurrentSlotIndex = value;
                RaisePropertyChanged(nameof(CurrentSlotIndex));
                Task.Run(() =>
                {
                    CaptureService.Instance.IsJogging = true;
                    JogCWCommand.RaiseCanExecuteChanged();
                    JogCCWCommand.RaiseCanExecuteChanged();
                    CaptureService.Instance.JumpToSlot(CurrentSlotIndex);
                    CaptureService.Instance.IsJogging = false;
                    JogCWCommand.RaiseCanExecuteChanged();
                    JogCCWCommand.RaiseCanExecuteChanged();
                });
                DisplayService.Instance.UpdateInterfaceAfterSlotSlection(CurrentSlotIndex);
            }
        }

        private int _currentSettingIndex;
        public int CurrentSettingIndex
        {
            get => _currentSettingIndex;
            set
            {
                SetProperty(ref _currentSettingIndex, value);
                DisplayService.Instance.CurrentSettingIndex = _currentSettingIndex;
                _currentSlot.SlotParameters.CurrentSettingIndex = _currentSettingIndex;
            }
        }

        public bool IsAutoExposure
        {
            get => _currentSlot.CurrentSetting.IsAutoExposure;
            set
            {
                _currentSlot.CurrentSetting.IsAutoExposure = value;
                RaisePropertyChanged(nameof(IsAutoExposure));
                CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
                _settingWindowCallback?.Invoke();
                RaisePropertyChanged(nameof(ExposureTime));
            }
        }

        public bool IsAutoGain
        {
            get => _currentSlot.CurrentSetting.IsAutoGain;
            set
            {
                _currentSlot.CurrentSetting.IsAutoGain = value;
                RaisePropertyChanged(nameof(IsAutoGain));
                CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
                _settingWindowCallback?.Invoke();
                RaisePropertyChanged(nameof(Gain));
            }
        }

        private readonly ObservableCollection<CollectedData> _dataCollected = new ObservableCollection<CollectedData>();
        public ObservableCollection<CollectedData> DataCollected => _dataCollected;

        private CollectedData _selectedData;
        public CollectedData SelectedData
        {
            get => _selectedData;
            set
            {
                if (SetProperty(ref _selectedData, value))
                {
                    LoadStack();
                    //CaptureService.Instance.CurrentStackPath = _selectedData == null ? string.Empty : _selectedData.FullPath;
                }
            }
        }

        private bool _isJogCW;
        public bool IsJogCW
        {
            get => _isJogCW;
            set
            {
                SetProperty(ref _isJogCW, value);
                CaptureService.Instance.IsJogging = IsJogCW || IsJogCCW;
                JogCCWCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isJogCCW;
        public bool IsJogCCW
        {
            get => _isJogCCW;
            set
            {
                SetProperty(ref _isJogCCW, value);
                CaptureService.Instance.IsJogging = IsJogCW || IsJogCCW;
                JogCWCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        public DelegateCommand LiveCommand { get; private set; }
        public DelegateCommand SnapshotCommand { get; private set; }
        public DelegateCommand CaptureCommand { get; private set; }
        public DelegateCommand ResetCommand { get; private set; }
        public DelegateCommand SaveCommand { get; private set; }
        public DelegateCommand JogCWCommand { get; private set; }
        public DelegateCommand JogCCWCommand { get; private set; }

        public CameraControlViewModel()
        {
            _preparedCallback = PreparedCallback;
            LiveCommand = new DelegateCommand(OnLiveExecute, CanLiveExecute);
            SnapshotCommand = new DelegateCommand(OnSnapshotExecute, CanSnapshotExecute);
            CaptureCommand = new DelegateCommand(OnCaptureExecute, CanCaptureExecute);
            ResetCommand = new DelegateCommand(OnResetExecute, CanResetExecute);
            SaveCommand = new DelegateCommand(OnSaveExecute, CanSaveExecute);
            JogCWCommand = new DelegateCommand(OnJogCWExecute, CanJogCWExecute);
            JogCCWCommand = new DelegateCommand(OnJogCCWExecute, CanJogCCWExecute);

            ConfigService.Instance.GetConfigEvent += GetConfig;
            ConfigService.Instance.SaveConfigEvent += SaveConfig;

            UpdateCurrentSlot();

            eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<FilterWheelShared.Event.ThorCamStatusEvent>().Subscribe(OnThorCamStatusChanged, ThreadOption.UIThread);
            eventAggregator.GetEvent<FilterWheelShared.Event.AutoExposureEvent>().Subscribe((e) => { ExposureTime = e.Exposure; });
            //eventAggregator.GetEvent<FilterWheelShared.Event.OrginCommandCallbackEvent>().Subscribe(ROIRecovery);
            eventAggregator.GetEvent<FilterWheelShared.Event.UpdateSlotSelectedIndexEvent>().Subscribe((e) => UpdateCurrentSlot());
            eventAggregator.GetEvent<FilterWheelShared.Event.StackSavedEvent>().Subscribe(UpdateSavedStack, ThreadOption.UIThread);
        }


        private void UpdateSavedStack(string stackName)
        {
            var data = new CollectedData(stackName);
            _dataCollected.Add(data);
            _selectedData = data;
            RaisePropertyChanged(nameof(SelectedData));
        }

        private void UpdateCurrentSlot()
        {
            RaisePropertyChanged(nameof(CurrentSlotIndex));
            _currentSlot = DisplayService.Instance.Slots[CurrentSlotIndex];
            _currentSettingIndex = _currentSlot.SlotParameters.CurrentSettingIndex;
            RaisePropertyChanged(nameof(CurrentSettingIndex));
            RaisePropertyChanged(nameof(ExposureTime));
            RaisePropertyChanged(nameof(IsAutoExposure));
            RaisePropertyChanged(nameof(Gain));
            RaisePropertyChanged(nameof(IsAutoGain));
        }

        private void LoadStack()
        {
            if (_selectedData == null) return;

            var files = Directory.GetFiles(_selectedData.FullPath).Where(f =>
            {
                FileInfo finfo = new FileInfo(f);
                var exten = finfo.Extension.ToLower();
                return exten == ".tif" || exten == ".tiff";
            }).ToList();

            var slotCount = DisplayService.Instance.Slots.Count;
            bool isSingle = true;
            bool isMetValid = false;

            ImageData[] imgs = new ImageData[slotCount];
            Array.ForEach(imgs, i => i = null);

            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
            {
                int fileHandle = -1;
                uint imageCount = 0;
                int result = ImageData.LoadTiffFile(files[fileIndex], ref fileHandle, ref imageCount);
                if (result != 0)
                {
                    //TODO : show error messagebox load file failed
                    ImageData.CloseTiffFile(fileHandle);
                    continue;
                }

                //judge with first valid file
                if (!isMetValid)
                {
                    isMetValid = true;
                    isSingle = imageCount == 1;
                }
                else
                {
                    if (isSingle && imageCount > 1)
                    {
                        ImageData.CloseTiffFile(fileHandle);
                        continue;
                    }
                }

                if (imageCount > 1)
                {
                    do
                    {
                        for (uint imageIndex = 0; imageIndex < imageCount; imageIndex++)
                        {
                            result = ImageData.LoadTiffImage(fileHandle, imageIndex, out TiffImageSimpleInfo info);
                            if (result != 0)
                            {
                                //TODO : show error messagebox load image failed
                                continue;
                            }

                            if (imgs[info.slot_index] != null)
                            {
                                imgs[info.slot_index].Dispose();
                            }
                            ImageData image = new ImageData(info.p2d_img_hdl);
                            imgs[info.slot_index] = image;
                        }
                    } while (false);
                    ImageData.CloseTiffFile(fileHandle);
                    //if first loaded file is multi-image files, other files won't load, just break the cycle
                    break;
                }
                else
                {
                    result = ImageData.LoadTiffImage(fileHandle, 0, out TiffImageSimpleInfo info);
                    ImageData.CloseTiffFile(fileHandle);
                    if (result != 0)
                    {
                        //TODO : show error messagebox load image failed
                        continue;
                    }

                    if (imgs[info.slot_index] != null)
                    {
                        imgs[info.slot_index].Dispose();
                    }
                    ImageData image = new ImageData(info.p2d_img_hdl);
                    imgs[info.slot_index] = image;
                }
            }

            DisplayService.Instance.UpdateDisplayImgAndThumbnailsAsLogSelection(imgs.ToList());
        }

        private void OnUpdateCameraProperties()
        {
            RaisePropertyChanged(nameof(IsColorCamera));
            RaisePropertyChanged(nameof(IsPolarCamera));
            RaisePropertyChanged(nameof(IsColorOrPolarCamera));

            RaisePropertyChanged(nameof(SupportGain));
            RaisePropertyChanged(nameof(GainRange));
            RaisePropertyChanged(nameof(Gain));
            RaisePropertyChanged(nameof(ExposureTimeRange));
            RaisePropertyChanged(nameof(ExposureTimeIncrement));
            RaisePropertyChanged(nameof(ExposureTime));
            IsAutoExposure = false;
            CaptureCommand.RaiseCanExecuteChanged();
        }

        private void OnThorCamStatusChanged(FilterWheelShared.Event.ThorCamStatusEventArgs args)
        {
            if(args.Status == ThorCamStatus.Capturing)
            {
                SelectedData = null;
                return;
            }
        }

        private bool _isLiveCommandExecuting = false;
        public bool IsLiveCommandExecuting
        {
            get => _isLiveCommandExecuting;
            private set
            {
                if (SetProperty(ref _isLiveCommandExecuting, value))
                    Application.Current?.Dispatcher.Invoke(() => LiveCommand.RaiseCanExecuteChanged());
            }
        }

        private void OnLiveExecute()
        {
            IsLiveCommandExecuting = true;
            if (CaptureService.Instance.IsLiving)
                CaptureService.Instance.StopLive();
            else
            {
                RadWindow.Confirm(new DialogParameters()
                {
                    Header = "Confirm",
                    Content = "Do you want to collect dark before living?",
                    DialogStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow,
                    OkButtonContent = "Yes",
                    CancelButtonContent = "No",
                    Closed = (s, e) =>
                    {
                        if (e.DialogResult == true)
                        {
                            RadWindow.Confirm(new DialogParameters()
                            {
                                Header = "Confirm",
                                Content = "Please add the lens cap and press OK.",
                                DialogStartupLocation = WindowStartupLocation.CenterOwner,
                                Owner = Application.Current.MainWindow,
                                Closed = (s, e) =>
                                {
                                    if (e.DialogResult == true)
                                    {
                                        RadWindow.Alert(new DialogParameters()
                                        {
                                            Header = "Confirm",
                                            Content = "Please remove the lens cap and press OK.",
                                            DialogStartupLocation = WindowStartupLocation.CenterOwner,
                                            Owner = Application.Current.MainWindow,
                                            Closed = (s, e) =>
                                            {
                                                if (e.DialogResult == true)
                                                {
                                                    CaptureService.Instance.LiveImages();
                                                }
                                            }
                                        });
                                    }
                                }
                            });
                        }
                    }
                });
            }
            IsLiveCommandExecuting = false;
        }

        private bool CanLiveExecute()
        {
            return !CaptureService.Instance.IsCapturing && !IsLiveCommandExecuting;
        }

        private void OnSnapshotExecute()
        {
            if (!CaptureService.Instance.IsSnapshoting)
            {
                CaptureService.Instance.Snapshot();
            }
            else
            {
                CaptureService.Instance.StopSnapshot();
                Thread.Sleep(300);
            }
        }

        private bool CanSnapshotExecute()
        {
            return true;
        }

        private bool _isCaptureCommandExecuting = false;
        public bool IsCaptureCommandExecuting
        {
            get => _isCaptureCommandExecuting;
            private set
            {
                if (SetProperty(ref _isCaptureCommandExecuting, value))
                    Application.Current?.Dispatcher.Invoke(() => CaptureCommand.RaiseCanExecuteChanged());
            }
        }

        private bool _isPreparing;
        public bool IsPreparing
        {
            get => _isPreparing;
            private set
            {
                if (SetProperty(ref _isPreparing, value))
                    Application.Current?.Dispatcher.Invoke(() => CaptureCommand.RaiseCanExecuteChanged());
            }
        }

        private readonly PreparedCallback _preparedCallback;
        private void PreparedCallback()
        {
            IsPreparing = false;
        }

        private async void OnCaptureExecute()
        {
            IsCaptureCommandExecuting = true;
            if (CaptureService.Instance.IsCapturing)
                await Task.Run(()=> CaptureService.Instance.StopCapture());
            else
            {
                CaptureService.Instance.StopLive();
                IsPreparing = true;
                CaptureService.Instance.StartCapture(_preparedCallback);
            }
            IsCaptureCommandExecuting = false;
        }

        private bool CanCaptureExecute()
        {
            return !IsCaptureCommandExecuting && !IsPreparing;
        }

        private void OnResetExecute()
        {
            var slot = DisplayService.Instance.Slots[CurrentSlotIndex];
            CurrentSettingIndex = 0;
            slot.SlotParameters.CurrentSettingIndex = CurrentSettingIndex;
            slot[CurrentSettingIndex].IsAutoExposure = false;
            slot[CurrentSettingIndex].ExposureTime = 10 * 1000; // us
            slot[CurrentSettingIndex].IsAutoGain = false;
            slot[CurrentSettingIndex].Gain = 0.0;
            CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex, true);
            RaisePropertyChanged(nameof(ExposureTime));
            RaisePropertyChanged(nameof(IsAutoExposure));
            RaisePropertyChanged(nameof(Gain));
            RaisePropertyChanged(nameof(IsAutoGain));
        }

        private bool CanResetExecute()
        {
            return true;
        }

        private void OnSaveExecute()
        {
            DisplayService.Instance.UpdateBackupSlots();
            var slotsBackup = DisplayService.Instance.SlotsBackup;
            ConfigService.Instance.SaveJsonFile(ThorlabsProduct.ThorImageCamSttingsPath, slotsBackup);
        }

        private bool CanSaveExecute()
        {
            return true;
        }

        private void OnJogCWExecute()
        {
            IsJogCW = true;
            Task.Run(() =>
            {
                CaptureService.Instance.JogClockwise();
                IsJogCW = false;
            });
        }

        private bool CanJogCWExecute()
        {
            return !CaptureService.Instance.IsJogging;
        }

        private void OnJogCCWExecute()
        {
            IsJogCCW = true;
            Task.Run(() =>
            {
                CaptureService.Instance.JogCounterclockwise();
                IsJogCCW = false;
            });
        }

        private bool CanJogCCWExecute()
        {
            return !CaptureService.Instance.IsJogging;
        }

        // Below functions should be kept to fit for ThorImage

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(CameraControlViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        private const string Name = "CameraControls";
        private void GetConfig(object sender, EventArgs e)
        {
            var obj = ConfigService.Instance.GetCorrespondingConfig<CameraControlConfig>(Name);

            this.ExposureTime = obj.ExposureTime;
            if (this.SupportGain)
                this.Gain = obj.Gain;
        }

        private void SaveConfig(object sender, EventArgs e)
        {
            ConfigService.Instance.UpdateCorrespondingConfig(Name, _config);
        }

        //public void LoadJsonSettings(List<JsonObject> jsonDatas)
        //{
        //    var target = jsonDatas.FirstOrDefault(item => item.Name == Name);
        //    if (target != null)
        //    {
        //        var obj = JsonSerializer.Deserialize<CameraControlViewModelBase>(target.Setting.ToString());
        //        this.ExposureTime = obj.ExposureTime;
        //        if (this.SupportGain)
        //            this.Gain = obj.Gain;
        //        this.LockXY = obj.LockXY;
        //        this.BinX = obj.BinX;
        //        this.BinY = obj.BinY;
        //        this.IsManualObjective = obj.IsManualObjective;

        //        if (!obj.IsManualObjective)
        //        {
        //            if (ObjectiveSource.Contains(obj.ObjectiveValue))
        //                SelectedObjective = obj.ObjectiveValue;
        //            else
        //                SelectedObjective = ObjectiveSource.First();
        //        }
        //        else
        //        {
        //            ManualObjective = obj.ObjectiveValue;
        //        }

        //        jsonDatas.Remove(target);
        //    }
        //}

        //public void SaveJsonSettings(List<JsonObject> jsonDatas)
        //{

        //    jsonDatas.Add(new JsonObject() { Name = Name, Setting = (CameraControlViewModelBase)this });
        //}

        //private void ROIRecovery()
        //{
        //    if (_RegionROI == null || !_RegionCameraRect.HasValue)
        //    {
        //        _RegionROI = null;
        //        _RegionCameraRect = null;
        //        return;
        //    }

        //    ROIViewModelBase roi = CreatNewROI(_RegionCameraRect, _RegionROI);
        //    if (roi == null) return;

        //    eventAggregator.GetEvent<FilterWheelShared.Event.AddROIEvent>().Publish(roi);

        //    _RegionROI = null;
        //    _RegionCameraRect = null;
        //    //DisplayService.Instance.IsOriginCommandExcute = false;
        //}

        //private ROIViewModelBase CreatNewROI(Int32Rect? nullableRect, ROIViewModelBase orgionROI)
        //{
        //    ROIViewModelBase roi = null;
        //    if (!nullableRect.HasValue || orgionROI == null) return roi;

        //    Int32Rect cameraROIRect = nullableRect.Value;
        //    Int32Rect originCameraROIRect = ThorlabsCamera.Instance.CameraROI;
        //    Int32Rect drawingROIRect = nullableRect.Value;
        //    if (DisplayService.Instance.IsFlipH)
        //        drawingROIRect.X = originCameraROIRect.X * 2 + originCameraROIRect.Width - cameraROIRect.X - cameraROIRect.Width;
        //    if (DisplayService.Instance.IsFlipV)
        //        drawingROIRect.Y = originCameraROIRect.Y * 2 + originCameraROIRect.Height - cameraROIRect.Y - cameraROIRect.Height;

        //    if (orgionROI is RectangleViewModel)
        //    {
        //        RectangleViewModel recROI = orgionROI as RectangleViewModel;
        //        roi = new RectangleViewModel()
        //        {
        //            Rect = new Rect(drawingROIRect.X, drawingROIRect.Y, drawingROIRect.Width, drawingROIRect.Height),
        //            Stroke = recROI.Stroke,
        //            Id = 0,
        //            IsRenderShow = recROI.IsRenderShow,
        //        };
        //    }
        //    else if (orgionROI is EllipseViewModel)
        //    {
        //        EllipseViewModel ellipseROI = orgionROI as EllipseViewModel;
        //        roi = new EllipseViewModel()
        //        {
        //            Rect = new Rect(drawingROIRect.X, drawingROIRect.Y, drawingROIRect.Width, drawingROIRect.Height),
        //            Stroke = ellipseROI.Stroke,
        //            Id = 0,
        //            IsRenderShow = ellipseROI.IsRenderShow,
        //        };
        //    }
        //    return roi;
        //}

        public void UpdateFilterSettings(int slotSettingIndex)
        {
            CurrentSettingIndex = slotSettingIndex;
            _settingWindowCallback?.Invoke();
            UpdateFilterSettings();
        }

        public void UpdateFilterSettings()
        {
            if (ThorlabsCamera.Instance.CurrentCamera != null)
                CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
            RaisePropertyChanged(nameof(ExposureTime));
            RaisePropertyChanged(nameof(IsAutoExposure));
            RaisePropertyChanged(nameof(Gain));
            RaisePropertyChanged(nameof(IsAutoGain));
        }

        private Action _settingWindowCallback = null;
        public void SetSettingWindowCallback(Action func)
        {
            _settingWindowCallback = func;
        }
    }
}
