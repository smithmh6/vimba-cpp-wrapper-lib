using DrawingTool.Factory.Material;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using FilterWheelShared.Common;
using FilterWheelShared.Converter;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;
using System.Collections.Generic;
using System.ComponentModel;

namespace FilterWheelShared.DeviceDataService
{
    internal class IntervalException : Exception
    {
        public IntervalException(string message) : base(message) { }
    }

    internal class InternalTriggerException : Exception
    {
        public InternalTriggerException(string message) : base(message) { }
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum CaptureSaveFormat
    {
        [Description("OME-TIFF")]
        CaptrueMultiTif = 0,
        [Description("TIFF")]
        CaptureSingleTif = 3,
    }

    public class CaptureService : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private bool _isAutoExposure;
        private CaptureService()
        {
            _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
            ReconnectService.PreReconnect += ReconnectService_PreReconnect;
        }

        private void ReconnectService_PreReconnect(PreReconnectEventParam eventParam)
        {
            if (IsLiving)
                IsLiving = false;
            if (IsCapturing)
                IsCapturing = false;
            CurrentExecutionStatus = ExecutionStatus.None;
        }

        private static readonly CaptureService _instance;
        public static CaptureService Instance => _instance;

        static CaptureService()
        {
            _instance = new CaptureService();
        }

        public event EventHandler<ROIViewModelBase> ROIRectChangedEvent;
        public event EventHandler ROIClearedEvent;

        #region Properties

        private bool _isAcqAverage = false;
        public bool IsAcqAverage
        {
            get => _isAcqAverage;
            set => SetProperty(ref _isAcqAverage, value);
        }

        private uint _averageframes = 2;
        public uint AverageFrames
        {
            get => _averageframes;
            set
            {
                if (value < 1) value = 1;
                SetProperty(ref _averageframes, value);
            }
        }

        private uint _totalAcqCount = 10;
        public uint TotalAcqCount
        {
            get => _totalAcqCount;
            set
            {
                if (value < 1) value = 1;
                SetProperty(ref _totalAcqCount, value);
            }
        }

        private uint _acquisitionDelay = 10;
        public uint AcquisitionDelay
        {
            get => _acquisitionDelay;
            set => SetProperty(ref _acquisitionDelay, value);
        }

        private AcquisitionDelayUnit _acqDelayUnit = AcquisitionDelayUnit.Minute;
        public AcquisitionDelayUnit AcqDelayUnit
        {
            get => _acqDelayUnit;
            set => SetProperty(ref _acqDelayUnit, value);
        }

        private double _currentExposure = double.NaN;
        public double CurrentExposure
        {
            get => _currentExposure;
            set
            {
                SetProperty(ref _currentExposure, value);
            }
        }

        private double _currentGain = double.NaN;
        public double CurrentGain
        {
            get => _currentGain;
            set
            {
                SetProperty(ref _currentGain, value);
            }
        }

        private string _saveFilePath = ThorlabsProduct.DocumentDir;
        public string SaveFilePath
        {
            get => _saveFilePath;
            set
            {
                if (SetProperty(ref _saveFilePath, value))
                {
                    CheckDiskFreeSpace();
                }
            }
        }

        private bool _isJogging = false;
        public bool IsJogging
        {
            get => _isJogging;
            set => SetProperty(ref _isJogging, value);
        }

        public string JoggingTargetName { get; set; }

        //private SingleFrameFormat _singleFormat = SingleFrameFormat.Jpeg;
        //public SingleFrameFormat SingleFormat
        //{
        //    get => _singleFormat;
        //    set
        //    {
        //        if (SetProperty(ref _singleFormat, value))
        //            _fileIndex = 1;
        //    }
        //}

        //private MultiFramesFormat _multiFormat = MultiFramesFormat.MultiFramesTif;
        //public MultiFramesFormat MultiFormat
        //{
        //    get => _multiFormat;
        //    set
        //    {
        //        if (SetProperty(ref _multiFormat, value))
        //        {
        //            _fileIndex = 1;
        //        }
        //    }
        //}

        private bool _isFilterWheelEnabled = true;
        public bool IsFilterWheelEnabled
        {
            get => _isFilterWheelEnabled;
            set => SetProperty(ref _isFilterWheelEnabled, value);
        }

        private string _prefixName = "Template";
        public string PrefixName
        {
            get => _prefixName;
            set => SetProperty(ref _prefixName, value);
        }

        private CaptureSaveFormat _saveType = CaptureSaveFormat.CaptureSingleTif;
        public CaptureSaveFormat SaveType
        {
            get => _saveType;
            set => SetProperty(ref _saveType, value);
        }

        private ExecutionStatus _currentExecutionStatus = ExecutionStatus.None;
        public ExecutionStatus CurrentExecutionStatus
        {
            get => _currentExecutionStatus;
            set { SetProperty(ref _currentExecutionStatus, value); }
        }

        private bool _isLiving = false;
        public bool IsLiving
        {
            get => _isLiving;
            set
            {
                if (SetProperty(ref _isLiving, value))
                {
                    RaisePropertyChanged(nameof(IsRunning));
                    var args = new ThorCamStatusEventArgs(_isLiving ? ThorCamStatus.Living : ThorCamStatus.None, null);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
                }
            }
        }

        public bool IsRunning => IsLiving || IsCapturing || IsSnapshoting;
        public bool IsOnPhoto => IsSnapshoting || IsCapturing;


        private bool _isSnapshoting = false;
        public bool IsSnapshoting
        {
            get => _isSnapshoting;
            set
            {
                if (SetProperty(ref _isSnapshoting, value))
                {
                    RaisePropertyChanged(nameof(IsOnPhoto));
                    RaisePropertyChanged(nameof(IsRunning));
                    var args = new ThorCamStatusEventArgs(_isSnapshoting ? ThorCamStatus.Snapshoting : ThorCamStatus.None, null);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
                }
            }
        }

        private bool _isCapturing = false;
        public bool IsCapturing
        {
            get => _isCapturing;
            set
            {
                if (SetProperty(ref _isCapturing, value))
                {
                    RaisePropertyChanged(nameof(IsOnPhoto));
                    RaisePropertyChanged(nameof(IsRunning));
                    var args = new ThorCamStatusEventArgs(_isCapturing ? ThorCamStatus.Capturing : ThorCamStatus.None, null);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
                }
            }
        }

        private bool _isEnableCapturingImageUpdate;
        public bool IsEnableCapturingImageUpdate
        {
            get => _isEnableCapturingImageUpdate;
            set => SetProperty(ref _isEnableCapturingImageUpdate, value);
        }

        private AcquisitionMode _acqMode;
        public AcquisitionMode AcqMode
        {
            get => _acqMode;
            set => SetProperty(ref _acqMode, value);
        }

        #region properties not capture needed
        //This property is not capture needed
        //private bool _isDurationEditable;
        //public bool IsDurationEditable
        //{
        //    get => _isDurationEditable;
        //    set => SetProperty(ref _isDurationEditable, value);
        //}

        public bool IsAutoExposure => _isAutoExposure;

        //private string _currentStackPath = string.Empty;
        //public string CurrentStackPath 
        //{
        //    get => _currentStackPath;
        //    set => SetProperty(ref _currentStackPath, value);
        //}

        #endregion

        #endregion

        #region private functions

        private SnapshotInfo GenerateSnapshotInfo()
        {
            double[] min = { 0, 0, 0 };
            double[] max = { 255, 255, 255 };
            if (ThorlabsCamera.Instance.IsColorCamera)
            {
                min[0] = DisplayService.Instance.MinR;
                min[1] = DisplayService.Instance.MinG;
                min[2] = DisplayService.Instance.MinB;
                max[0] = DisplayService.Instance.MaxR;
                max[1] = DisplayService.Instance.MaxG;
                max[2] = DisplayService.Instance.MaxB;
            }
            else
            {
                min[0] = DisplayService.Instance.MinMono;
                max[0] = DisplayService.Instance.MaxMono;
            }

            var camString = $"{ThorlabsCamera.Instance.CurrentCamera.ModelName}({ThorlabsCamera.Instance.CurrentCamera.SerialNumber})";
            var folderPath = Path.Combine(SaveFilePath, camString);
            var iinfo = Directory.CreateDirectory(folderPath);
            if (!iinfo.Exists)
                throw new Exception($"Create directory : {folderPath} failed.");

            var filePath = string.Empty;
            int snapFileIndex = 1;
            var isExist = true;

            while (isExist)
            {
                var fileName = $"{PrefixName}_{snapFileIndex.ToString().PadLeft(4, '0')}.tif";
                filePath = Path.Combine(folderPath, fileName);
                isExist = File.Exists(filePath);
                snapFileIndex++;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception($"Saved file path is null or empty.");
            }           

            SnapshotInfo info = new SnapshotInfo(DisplayService.Instance.CurrentSlotIndex, filePath, IsAcqAverage ? AverageFrames : 1, min, max);
            return info;
        }

        private int _captureStackIndex = 1;
        private CaptureInfo GenerateCaptureInfo(int slotIndex, out string folderName)
        {
            _eventAggregator.GetEvent<PreviewCaptureEvent>().Publish();
            double[] min = { 0, 0, 0 };
            double[] max = { 255, 255, 255 };
            if (ThorlabsCamera.Instance.IsColorCamera)
            {
                min[0] = DisplayService.Instance.MinR;
                min[1] = DisplayService.Instance.MinG;
                min[2] = DisplayService.Instance.MinB;
                max[0] = DisplayService.Instance.MaxR;
                max[1] = DisplayService.Instance.MaxG;
                max[2] = DisplayService.Instance.MaxB;
            }
            else
            {
                min[0] = DisplayService.Instance.MinMono;
                max[0] = DisplayService.Instance.MaxMono;
            }

            var folderPath = SaveFilePath;

            var isExist = true;
            while (isExist)
            {
                var path = $"stack_{PrefixName}_{_captureStackIndex.ToString().PadLeft(4, '0')}";
                var camString = $"{ThorlabsCamera.Instance.CurrentCamera.ModelName}({ThorlabsCamera.Instance.CurrentCamera.SerialNumber})";
                folderPath = Path.Combine(SaveFilePath, camString, path);
                isExist = Directory.Exists(folderPath);
                _captureStackIndex++;
            }
            var iinfo = Directory.CreateDirectory(folderPath);
            if (!iinfo.Exists)
                throw new Exception($"Create directory : {folderPath} failed.");

            folderName = folderPath;

            List<CaptureSlotSetting> list_settings = new List<CaptureSlotSetting>();
            for(int i = 0; i < DisplayService.Instance.Slots.Count; i++)
            {
                var setting = DisplayService.Instance.Slots[i].CurrentSetting;
                list_settings.Add(new CaptureSlotSetting(setting));
            }

            CaptureInfo info = new CaptureInfo(folderPath, PrefixName, (CaptureSaveType)SaveType, IsAcqAverage ? AverageFrames : 1, min, max, list_settings, slotIndex);

            return info;
        }

        private void UpdateCurrentPara()
        {

        }

        private void WritePermissionCheck()
        {
            DirectorySecurity s = new DirectorySecurity(_saveFilePath, AccessControlSections.Access);
            AuthorizationRuleCollection rules = s.GetAccessRules(true, true, typeof(NTAccount));
            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(currentUser);
            bool hasWriteAccess = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.FileSystemRights.HasFlag(FileSystemRights.WriteData))
                {
                    NTAccount ntAccount = rule.IdentityReference as NTAccount;
                    if (ntAccount == null)
                    {
                        continue;
                    }

                    if (principal.IsInRole(ntAccount.Value))
                    {
                        hasWriteAccess = true;
                        break;
                    }
                }
            }
            if (!hasWriteAccess)
                throw new PrivilegeNotHeldException();
        }

        private long _diskAvailableFreeSpace = long.MaxValue;
        private void CheckDiskFreeSpace()
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(_saveFilePath);
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (string.Compare(dirInfo.Root.FullName, d.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        _diskAvailableFreeSpace = d.AvailableFreeSpace;
                    }
                }
            }
            catch (Exception e)
            {
                var args = new ThorCamStatusEventArgs(_saveFilePath, ErrorType.SaveDirectoryError);
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
            }
        }
        #endregion

        #region public functions

        public void Reset()
        {
            _captureStackIndex = 1;
            StopLive();
        }

        public static void SetCurrentSlotParasIntoCam(int currentSlotIndex, bool force = false)
        {
            var slots = DisplayService.Instance.Slots;
            var slotsettingIndex = slots[currentSlotIndex].SlotParameters.CurrentSettingIndex;
            if (slots != null && slots.Count > 0 && slots.Count > currentSlotIndex)
            {
                SetCurrentSlotParasIntoCam(currentSlotIndex, slotsettingIndex, force);
            }
        }

        private static void SetCurrentSlotParasIntoCam(int currentSlotIndex, int currentSlotSettingIndex, bool force)
        {
            var slots = DisplayService.Instance.Slots;
            if (slots != null && slots.Count > 0 && slots.Count > currentSlotIndex)
            {
                var currentSlot = slots[currentSlotIndex];
                var currentSetting = currentSlot[currentSlotSettingIndex];

                ThorlabsCamera.Instance.SetExposureAutoEnabled(currentSetting.IsAutoExposure);
                if (!currentSetting.IsAutoExposure)
                {
                    var currentExposure = currentSetting.ExposureTime;
                    bool forceExp = force;
                    if (!forceExp)
                    {
                        var preExposure = ThorlabsCamera.Instance.ExposureTime;
                        forceExp = Math.Abs(currentExposure - preExposure) > 1e-6;
                    }
                    if (forceExp)
                    {
                        ThorlabsCamera.Instance.SetExposureTime(currentExposure);
                    }
                    ThorlabsCamera.Instance.GetExposureTime();
                    currentSetting.ExposureTime = ThorlabsCamera.Instance.ExposureTime;
                }

                ThorlabsCamera.Instance.SetAutoGainEnabled(currentSetting.IsAutoGain);
                if (!currentSetting.IsAutoGain)
                {
                    var currentGain = currentSetting.Gain;
                    bool forceGain = force;
                    if (!forceGain)
                    {
                        var preGain = ThorlabsCamera.Instance.Gain;
                        forceGain = Math.Abs(currentGain - preGain) > 1e-6;
                    }
                    if (forceGain)
                    {
                        ThorlabsCamera.Instance.SetGain(currentGain);
                    }
                    ThorlabsCamera.Instance.GetGain();
                    currentSetting.Gain = ThorlabsCamera.Instance.Gain;
                }
            }
        }

        public void SetAutoExposure(bool isAutoExposure)
        {
            Task.Run(() =>
            {
                try
                {
                    ThorlabsCamera.Instance.SetExposureAutoEnabled(isAutoExposure);
                    _isAutoExposure = isAutoExposure;
                    RaisePropertyChanged(nameof(IsAutoExposure));

                }
                catch (Exception e)
                {
                    Logger.ThorLogger.Log("SetAutoExposure failed.", e, ThorLogWrapper.ThorLogLevel.Error);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs("", ErrorType.SetAutoExposureFailed));
                }
            }).Wait();
        }

        private bool _isStoppingLive = false;
        public void StopLive()
        {
            if (_isStoppingLive || !IsLiving || !DisplayService.Instance.IsRunning)
                return;
            try
            {
                _isStoppingLive = true;
                ThorlabsCamera.Instance.StopPreview();
                IsLiving = false;
                CurrentExecutionStatus = ExecutionStatus.None;
            }
            catch (Exception e)
            {
                IsLiving = true;
                Logger.ThorLogger.Log("Stop preview failed.", e, ThorLogWrapper.ThorLogLevel.Error);
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs("", ErrorType.StopPreviewFailed));
            }
            finally 
            { 
                _isStoppingLive = false; 
            }
            //update status
            //_eventAggregator.GetEvent<CaptureStatusChangedEvent>().Publish(CaptureStatus.Stop);
        }


        public void StopCapture()
        {
            if (IsLiving)
            {
                StopLive();
            }

            if (IsCapturing)
            {
                _captureSource.Cancel();
                ThorlabsCamera.Instance.StopCapture();
                _stopCaptureEvent.WaitOne();
                IsCapturing = false;
                CurrentExecutionStatus = ExecutionStatus.None;
                //Logger.ThorLogger.Log($"Stopped Capture.", ThorLogWrapper.ThorLogLevel.Error);
            }
        }

        private System.Threading.AutoResetEvent _stopCaptureEvent = new System.Threading.AutoResetEvent(false);
        private System.Threading.ManualResetEventSlim _waitEvent = new System.Threading.ManualResetEventSlim(false);
        private System.Threading.CancellationTokenSource _captureSource = new System.Threading.CancellationTokenSource();
        public async void StartCapture(PreparedCallback preparedCallback)
        {
            if (IsCapturing)
                return;

            if (TotalAcqCount <= 0)
                return;

            P2dInfo info = new P2dInfo();
            info.x_size = ThorlabsCamera.Instance.CameraROI.Width;
            info.y_size = ThorlabsCamera.Instance.CameraROI.Height;
            info.channels = ThorlabsCamera.Instance.IsColorCamera ? P2dChannels.P2D_CHANNELS_3 : P2dChannels.P2D_CHANNELS_1;
            info.pix_type = ThorlabsCamera.Instance.BitDepth > 8 ? P2dDataFormat.P2D_16U : P2dDataFormat.P2D_8U;
            info.valid_bits = ThorlabsCamera.Instance.BitDepth;
            DisplayService.Instance.PrepareForCapture(info);

            IsCapturing = true;
            CurrentExecutionStatus = ExecutionStatus.Capturing;
            _captureSource = new System.Threading.CancellationTokenSource();
            var token = _captureSource.Token;

            await Task.Run(() =>
            {
                try
                {
                    ImageLoadService.Instance.Close();
                    var dirInfo = Directory.CreateDirectory(_saveFilePath);
                    if (!dirInfo.Exists)
                    {
                        throw new Exception($"Directory {_saveFilePath} is not exists.");
                    }
                    WritePermissionCheck();
                    var cycleCount = TotalAcqCount;
                    var slotIndex = DisplayService.Instance.CurrentSlotIndex;

                    ThorlabsCamera.Instance.ResetMinMax(ThorlabsCamera.Instance.IsColorCamera, ThorlabsCamera.Instance.BitDepth, ThorCamStatus.Capturing);

                    while (true)
                    {
                        var info = GenerateCaptureInfo(slotIndex, out string folderName);
                        _eventAggregator.GetEvent<StackSavedEvent>().Publish(folderName);
                        var result = ThorlabsCamera.Instance.StartCapture(info, preparedCallback);
                        //result : 0-success; 101-stop manual
                        if (result == 0)
                        {
                            JumpToSlot(slotIndex);
                        }
                        info.Dispose();

                        if (token.IsCancellationRequested)
                            break;

                        if (AcqMode == AcquisitionMode.Times)
                        {
                            cycleCount--;
                            if (cycleCount <= 0)
                                break;

                            var multiplier = 1000;
                            switch (AcqDelayUnit)
                            {
                                case AcquisitionDelayUnit.Minute:
                                    multiplier = 60 * 1000;
                                    break;
                                case AcquisitionDelayUnit.Hour:
                                    multiplier = 60 * 60 * 1000;
                                    break;
                                default:
                                    break;
                            }
                            if (AcquisitionDelay > 0)
                            {
                                var timeMs = (int)(AcquisitionDelay * multiplier);
                                _waitEvent.Wait(timeMs, token);
                            }
                        }
                    }

                    //if (cycleCount <= 0)
                    //    DisplayService.Instance.UpdateInterfaceAfterSlotSlection(slotIndex);
                    //Logger.ThorLogger.Log("Start Capture.", ThorLogWrapper.ThorLogLevel.Error);

                    //Logger.ThorLogger.Log($"Capture done. {IsCapturing}", ThorLogWrapper.ThorLogLevel.Error);
                }
                catch (Exception e)
                {
                    var args = new ThorCamStatusEventArgs(ThorCamStatus.Error, e);
                    if (e is IntervalException ie)
                    {
                        args = new ThorCamStatusEventArgs(ie.Message, ErrorType.IntervalError);
                    }
                    else if (e is InternalTriggerException te)
                    {
                        args = new ThorCamStatusEventArgs(te.Message, ErrorType.InternalTriggerError);
                    }
                    else if (e is PrivilegeNotHeldException pe)
                    {
                        args = new ThorCamStatusEventArgs(_saveFilePath, ErrorType.SaveDirectoryAccess);
                    }
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);

                    preparedCallback?.Invoke();
                }
                finally
                {
                    _stopCaptureEvent.Set();
                    IsCapturing = false;
                    CurrentExecutionStatus = ExecutionStatus.None;
                }

            }, token).ConfigureAwait(false);
        }

        public void LiveImages()
        {
            if (IsLiving) return;
            CurrentExecutionStatus = ExecutionStatus.Living;
            IsLiving = true;
            ImageLoadService.Instance.Close();

            P2dInfo info = new P2dInfo();
            info.x_size = ThorlabsCamera.Instance.CameraROI.Width;
            info.y_size = ThorlabsCamera.Instance.CameraROI.Height;
            info.channels = ThorlabsCamera.Instance.IsColorCamera ? P2dChannels.P2D_CHANNELS_3 : P2dChannels.P2D_CHANNELS_1;
            info.pix_type = ThorlabsCamera.Instance.BitDepth > 8 ? P2dDataFormat.P2D_16U : P2dDataFormat.P2D_8U;
            info.valid_bits = ThorlabsCamera.Instance.BitDepth;
            DisplayService.Instance.PrapareForLive(info);

            var task = Task.Run(() =>
            {
                try
                {
                    ThorlabsCamera.Instance.StartPreview();
                }
                catch (Exception e)
                {
                    IsLiving = false;
                    Logger.ThorLogger.Log("Start preview failed.", e, ThorLogWrapper.ThorLogLevel.Error);
                    var args = new ThorCamStatusEventArgs(e.Message, ErrorType.InternalTriggerError);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
                }
            });
            task.Wait();
        }

        private System.Threading.CancellationTokenSource _snapshotSource = new System.Threading.CancellationTokenSource();
        public void Snapshot()
        {
            P2dInfo info = new P2dInfo();
            info.x_size = ThorlabsCamera.Instance.CameraROI.Width;
            info.y_size = ThorlabsCamera.Instance.CameraROI.Height;
            info.channels = ThorlabsCamera.Instance.IsColorCamera ? P2dChannels.P2D_CHANNELS_3 : P2dChannels.P2D_CHANNELS_1;
            info.pix_type = ThorlabsCamera.Instance.BitDepth > 8 ? P2dDataFormat.P2D_16U : P2dDataFormat.P2D_8U;
            info.valid_bits = ThorlabsCamera.Instance.BitDepth;
            DisplayService.Instance.PrapareForSnapshot(info);

            Task.Run(() =>
            {
                try
                {
                    IsSnapshoting = true;
                    CurrentExecutionStatus = ExecutionStatus.Snapshoting;
                    ImageLoadService.Instance.Close();

                    var info = GenerateSnapshotInfo();
                    if (IsSnapshoting)
                        ThorlabsCamera.Instance.StartSnapshot(info);
                }
                catch (Exception e)
                {
                    Logger.ThorLogger.Log("Start snapshot failed.", e, ThorLogWrapper.ThorLogLevel.Error);
                    var args = new ThorCamStatusEventArgs(e.Message, ErrorType.InternalTriggerError);
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(args);
                }
                finally
                {
                    IsSnapshoting = false;
                    CurrentExecutionStatus = ExecutionStatus.None;
                }
            });
        }
        public void StopSnapshot()
        {
            if (IsSnapshoting)
            {
                IsSnapshoting = false;
                ThorlabsCamera.Instance.StopSnapshot();
                CurrentExecutionStatus = ExecutionStatus.None;
            }
        }

        public void FindHomePosition()
        {
            int slotIndex = 0;
            JoggingTargetName = DisplayService.Instance.Slots[slotIndex].SlotName;
            _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Jogging, null));
            ThorlabsCamera.Instance.FindHomePosition();

            DisplayService.Instance.CurrentSlotIndex = slotIndex;
            SetCurrentSlotParasIntoCam(slotIndex, true);

            if (IsLiving)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Living, new JoggingToLivingException()));
            }
            else if (IsCapturing)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Capturing, null));
            }
            else
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.None, null));
            }
            _eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(slotIndex);
        }

        public void JogClockwise()
        {
            var count = DisplayService.Instance.Slots.Count;
            var slotIndex = DisplayService.Instance.CurrentSlotIndex;
            slotIndex++;
            if (slotIndex >= count)
                slotIndex -= count;
            ThorlabsCamera.Instance.JogClockwise();
            DisplayService.Instance.CurrentSlotIndex = slotIndex;
            SetCurrentSlotParasIntoCam(slotIndex, true);
            if (IsLiving)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Living, new JoggingToLivingException()));
            }
            else if (IsCapturing)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Capturing, null));
            }
            else
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.None, null));
            }
            _eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(slotIndex);
        }

        public void JogCounterclockwise()
        {
            var count = DisplayService.Instance.Slots.Count;
            var slotIndex = DisplayService.Instance.CurrentSlotIndex;
            slotIndex--;
            if (slotIndex < 0)
                slotIndex = count - 1;
            ThorlabsCamera.Instance.JogCounterClockwise();
            DisplayService.Instance.CurrentSlotIndex = slotIndex;
            SetCurrentSlotParasIntoCam(slotIndex, true);
            if (IsLiving)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Living, new JoggingToLivingException()));
            }
            else if (IsCapturing)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Capturing, null));
            }
            else
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.None, null));
            }
            _eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(slotIndex);
        }

        public void JumpToSlot(int slotIndex)
        {
            JoggingTargetName = DisplayService.Instance.Slots[slotIndex].SlotName;
            _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Jogging, null));
            // execute jog to slot
            ThorlabsCamera.Instance.JogToSlot(slotIndex);
            DisplayService.Instance.CurrentSlotIndex = slotIndex;
            SetCurrentSlotParasIntoCam(slotIndex, true);
            if (IsLiving)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Living, new JoggingToLivingException()));
            }
            else if(IsCapturing)
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Capturing, null));
            }
            else
            {
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.None, null));
            }
            _eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(slotIndex);
        }

        public void MoveConstant()
        {
            IsJogging = true;
            ThorlabsCamera.Instance.MoveConstant();
        }

        public void StopMotion()
        {
            IsJogging = false;
            ThorlabsCamera.Instance.StopMotion();
        }

        public void OnROISelectedStatusChanged(ROIViewModelBase roi)
        {
            ROIRectChangedEvent?.Invoke(this, roi);
        }

        public void OnROICleared()
        {
            ROIClearedEvent?.Invoke(null, EventArgs.Empty);
        }

        #endregion
    }
}
