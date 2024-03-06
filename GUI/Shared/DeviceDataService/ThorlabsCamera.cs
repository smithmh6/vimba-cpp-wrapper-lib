using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FilterWheelShared.Common;
using FilterWheelShared.Event;
using FilterWheelShared.Logger;
using ThorLogWrapper;
using FilterWheelShared.ImageProcess;

namespace FilterWheelShared.DeviceDataService
{
    public struct ROIRange
    {
        public int TopLeftXMin;
        public int TopLeftXMax;
        public int TopLeftYMin;
        public int TopLeftYMax;
        public int BottomRightXMin;
        public int BottomRightXMax;
        public int BottomRightYMin;
        public int BottomRightYMax;

        public ROIRange(int tlxmin, int tlxmax, int tlymin, int tlymax, int brxmin, int brxmax, int brymin, int brymax)
        {
            TopLeftXMin = tlxmin;
            TopLeftXMax = tlxmax;
            TopLeftYMin = tlymin;
            TopLeftYMax = tlymax;
            BottomRightXMin = brxmin;
            BottomRightXMax = brxmax;
            BottomRightYMin = brymin;
            BottomRightYMax = brymax;
        }

    }

    public class SlotChangingEventArg : EventArgs
    {
        public bool IsJogging { get; set; }
        public int SlotIndex { get; set; }

        public SlotChangingEventArg(bool isJogging, int slotIndex)
        {
            IsJogging = isJogging;
            SlotIndex = slotIndex;
        }
    }

    public class ThorlabsCamera : BindableBase
    {
        private static readonly IEventAggregator _eventAggregator = ContainerLocator.Current.Resolve<IEventAggregator>();

        #region constructor
        private ThorlabsCamera()
        {
            _eventAggregator.GetEvent<CloseApplicationEvent>().Subscribe(OnApplicationShutDown);
            _frameRateTimer = new System.Timers.Timer(500) { Enabled = false };
            _frameRateTimer.Elapsed += OnFrameRateTimerElapsed;
        }
        #endregion

        #region Singleton

        static ThorlabsCamera()
        {
            _instance = new ThorlabsCamera();
        }

        private static readonly ThorlabsCamera _instance;
        public static ThorlabsCamera Instance => _instance;

        #endregion

        #region Properties
        public CameraInfo CurrentCamera { get; private set; } = null;
        public string FirmwareVersion { get; private set; }
        public int BitDepth { get; private set; }

        public double ExposureTime { get; private set; }
        public DoubleParams ExposureTimeParams { get; private set; }

        public bool SupportBlackLevel { get; private set; } = true;
        public Tuple<int, int> BlackLevelRange { get; private set; }
        public int BlackLevel { get; private set; }

        public bool SupportGain { get; private set; } = true;
        public Tuple<double, double> GainRange { get; private set; }
        public double Gain { get; private set; }

        public bool IsColorCamera { get; private set; } = false;
        public bool IsPolarCamera { get; private set; } = false;

        public Tuple<int, int> BinXRange { get; private set; }
        public int BinX { get; private set; }
        public Tuple<int, int> BinYRange { get; private set; }
        public int BinY { get; private set; }

        public ROIRange CameraROIRange { get; private set; }
        public Int32Rect CameraROI { get; private set; }

        //This property is for Button "Original"
        public Int32Rect CameraOrignalROI { get; set; }

        //This property is to judge whether clear ROIS
        public Int32Rect PreviousROI { get; set; } = Int32Rect.Empty;

        public bool IsLEDSupport { get; private set; }
        public bool IsLEDOn { get; private set; }

        public bool IsFrameRateControlSupport { get; private set; }
        public Tuple<double, double> InternalTriggerIntervalRange { get; private set; }

        public bool IsHotPixelCorrectionSupport { get; private set; }
        public bool HotPixelCorrectionEnabled { get; private set; }
        public Tuple<int, int> HotPixelCorrectionThresholdRange { get; private set; }
        public int HotPixelCorrectionThreshold { get; private set; }
        public PolarImageTypes PolarImageType { get; private set; }

        public bool IsCorrectionModeEnabled { get; private set; }
        public CorrectionMode CorrectionMode { get; private set; }
        public bool IsReverseXEnabled { get; private set; }
        public bool IsReverseYEnabled { get; private set; }
        public bool IsSerialHubEnabled { get; private set; }
        public bool IsMoving { get; private set; }

        //camera is connected status
        public bool IsCameraConnected => CurrentCamera != null;

        #endregion

        //public event EventHandler<uint> CallbackFrameEvent;
        public event EventHandler<double?> CameraFPSEvent;
        public event EventHandler ROIChangedEvent;
        public event EventHandler MemoryOverflowEvent;
        public event EventHandler<SlotChangingEventArg> SlotChangingEvent;

        private DisconnectCallback _disconnectCallback;
        private ImageCallback _imageCallback;
        private AutoExposureCallback _autoExposureCallback;

        private MEMORY_INFO MemInfo = new MEMORY_INFO();
        private bool _preventMemoryOverflowEvent = false;

        private void OnFrameRateTimerElapsed(object Sender, EventArgs e)
        {
            if (_currentFrameNumber > _previousFrameNumber && _currentClock > _previousClock)
            {
                var frameRate = Math.Round((_currentFrameNumber - _previousFrameNumber) * 1e6 / (_currentClock - _previousClock), 1, MidpointRounding.AwayFromZero);
                _previousFrameNumber = _currentFrameNumber;
                _previousClock = _currentClock;

                //Task.Run(() =>
                //{
                CameraFPSEvent?.Invoke(this, frameRate);
                //});

                //Task.Run(() =>
                //{
                //    //var rate = Math.Round((double)(frameNum - _previousFrameNumber) * 1000 / _swFrameRate.ElapsedMilliseconds, 1, MidpointRounding.AwayFromZero);
                //    CameraFPSEvent?.Invoke(this, new Tuple<double?, uint>(rate, _currentFrameNumber));
                //});
            }

            if (_preventMemoryOverflowEvent) return;
            UnsafeNativeMethods.GlobalMemoryStatus(ref MemInfo);
            if ((double)MemInfo.dwAvailPhys / MemInfo.dwTotalPhys < 0.2)
            {
                _preventMemoryOverflowEvent = true;
                MemoryOverflowEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool _callbackRegistered = false;
        private void RegistryCallback()
        {
            if (_callbackRegistered) return;
            _callbackRegistered = true;
            _disconnectCallback = DisconnectCallbackProcess;
            _imageCallback = ImageCallbackProcess;
            _autoExposureCallback = AutoExposureCallbackProcess;
            var result = CameraLIbCommand.SetCallback(_imageCallback, _disconnectCallback, _autoExposureCallback);
            if (result != 0)
            {
                throw new Exception($"Set callbacks failed! Error code is {result}.");
            }
        }

        private void AutoExposureCallbackProcess(byte isStable, double exposure)
        {
            ExposureTime = exposure;
            if (isStable != 1)
            {
                _exposureChanged = true;
                _skipFrameCount = 0;
            }
            _eventAggregator.GetEvent<AutoExposureEvent>().Publish(new AutoExposureEventArgs(isStable == 1, exposure));
        }

        private void DisconnectCallbackProcess()
        {
            var info = new CameraInfo(CurrentCamera.SerialNumber, CurrentCamera.ModelName);
            CloseCamera(false);
            _preventMemoryOverflowEvent = true;
            _ = ReconnectService.Reconnect(() =>
            {
                var res = OpenCameraOnly(info);
                if (res == 0)
                    InitCamera(false);
                return res == 0;
            });
        }

        //For calculate FrameRate
        private int _skipFrameCount = 0;
        private bool _exposureChanged = false;
        private System.Timers.Timer _frameRateTimer;
        //private readonly Stopwatch _swFrameRate = new Stopwatch();
        private uint _previousFrameNumber = 0;
        private long _previousClock = 0;
        private uint _currentFrameNumber = 0;
        private long _currentClock = 0;

        private void ImageCallbackProcess(int p2dImgHdl, uint frameNum, long frameClock, int CorrespondingSlotIndex, byte IsThumbnailNeed)
        {
            if (_exposureChanged)
            {
                _skipFrameCount++;
                if (_skipFrameCount >= 5)
                {
                    _exposureChanged = false;
                    _skipFrameCount = 0;
                }
            }

            if (frameNum <= 5 || _exposureChanged)
            {
                _previousFrameNumber = frameNum;
                _previousClock = frameClock;
            }
            else
            {
                _currentClock = frameClock;
                _currentFrameNumber = frameNum;
            }

            Task.Run(() =>
            {
                //CallbackFrameEvent?.Invoke(this, frameNum);
                if (_isCapturing)
                {
                    SlotChangingEvent?.Invoke(this, new SlotChangingEventArg(IsThumbnailNeed == 1, CorrespondingSlotIndex));
                }
            });

            if (p2dImgHdl < 0) return;
            //ThorLogger.Log($"Slot Index in C# callback : {CorrespondingSlotIndex}", ThorLogLevel.Error);
            DisplayService.Instance.UpdateCircleImage(p2dImgHdl, CorrespondingSlotIndex, IsThumbnailNeed == 1);
        }

        public int UsedValidBits { get; private set; }
        private ThorCamStatus _currentStatus = ThorCamStatus.Error;
        public void ResetMinMax(bool isColor, int validBits, ThorCamStatus status)
        {
            var needReset = false;
            if (status == ThorCamStatus.Loaded || status == ThorCamStatus.None)
            {
                needReset = true;
            }
            if (status == ThorCamStatus.Living || status == ThorCamStatus.Capturing)
            {
                if (_currentStatus == ThorCamStatus.Loaded)
                {
                    needReset = true;
                }
            }
            _currentStatus = status;
            if (!needReset) return;

            _eventAggregator.GetEvent<ResetMainPanelEvent>().Publish(isColor);

            if (!isColor)
            {
                if (validBits < 8)
                    validBits = 8;
                //DisplayService.Instance.UpdateGamma(ChannelType.Mono, 2.2, false);
                DisplayService.Instance.UpdateGamma(ChannelType.Mono, 1.0, false);
                DisplayService.Instance.UpdateMinMax(0, (1 << validBits) - 1, ChannelType.Mono, false);
                UsedValidBits = validBits;
            }
            else
            {
                DisplayService.Instance.UpdateGamma(ChannelType.R, 1.0, false);
                DisplayService.Instance.UpdateGamma(ChannelType.G, 1.0, false);
                DisplayService.Instance.UpdateGamma(ChannelType.B, 1.0, false);
                DisplayService.Instance.UpdateGammaCombine(1.0, false);
                DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.R, false);
                DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.G, false);
                DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.B, false);
                DisplayService.Instance.UpdateMinMaxCombine(0, 255, false);
                UsedValidBits = 8;
            }
            _eventAggregator.GetEvent<ImageValidbitsChanged>().Publish(UsedValidBits);
        }

        public void OnLoadImage(int p2dImgHdl)
        {
            DisplayService.Instance.UpdateTempImage(p2dImgHdl);
        }

        private void OnApplicationShutDown(int _)
        {
            CloseCamera();
            CameraLIbCommand.Release();
        }

        public void CloseCamera(bool stopUpdate = true)
        {
            CameraLIbCommand.Close();
            ResetCurrentCamera();
            if (stopUpdate)
                DisplayService.Instance.StopUpdate();

            ThorLogger.UpdateLogPath(null);
        }

        public void ResetCurrentCamera()
        {
            _preventMemoryOverflowEvent = false;
            CurrentCamera = null;
            RaisePropertyChanged(nameof(IsCameraConnected));
        }

        public static List<CameraInfo> ListCameras(CameraInfo currentCamera = null)
        {
            var list = new List<CameraInfo>();
            var sb = new StringBuilder(4096);
            int result = CameraLIbCommand.List(sb, 4096);
            if (result < 0)
                return list;
            var cameras = sb.ToString().Split(';').Where(p => !string.IsNullOrEmpty(p.Trim()));
            foreach (var cam in cameras)
            {
                var camSNModel = cam.Split(',');
                if (camSNModel.Length != 2) continue;

                if (currentCamera != null && currentCamera.SerialNumber == camSNModel[0])
                {
                    list.Add(currentCamera);
                    continue;
                }

                list.Add(new CameraInfo(camSNModel[0], camSNModel[1]));
            }
            return list;
        }

        private int OpenCameraOnly(CameraInfo info)
        {
            var result = CameraLIbCommand.Open(info.SerialNumber);
            if (result != 0)
            {
                CurrentCamera = null;
                return result;
            }

            //Store current opend camera
            CurrentCamera = info;
            //Save Serial number to ThorlabsProduct
            ThorlabsProduct.SerialNo = CurrentCamera.ToString();
            ThorLogger.UpdateLogPath($"{info.ModelName}-{info.SerialNumber}");
            return 0;
        }

        private int InitCamera(bool isOpenNewOne = true)
        {
            try
            {
                //_eventAggregator.GetEvent<ResetMainPanelEvent>().Publish();

                GetBitDepth();

                var firmwareSB = new StringBuilder(4096);
                CameraLIbCommand.GetFirmwareVersion(firmwareSB, 4096);
                FirmwareVersion = firmwareSB.ToString();

                //Exposure
                GetExposureTimeParams();
                var max = Properties.Settings.Default.ExposureMax;
                if (ExposureTimeParams.max_value > max)
                    ExposureTimeParams = new DoubleParams(ExposureTimeParams.min_value, max, ExposureTimeParams.increment);

                GetExposureTime();

                //BlackLevel
                BlackLevelRange = GetBlackLevelRange();
                SupportBlackLevel = BlackLevelRange.Item2 > 0;
                if (SupportBlackLevel)
                {
                    GetBlackLevel();
                }

                //Gain
                GainRange = GetGainRange();
                if (GainRange.Item1 == GainRange.Item2 && GainRange.Item2 == 0)
                {
                    SupportGain = false;
                }
                else
                {
                    SupportGain = true;
                    GetGain();
                }

                IsColorCamera = GetColorMode();
                IsPolarCamera = GetPolarMode();
                DisplayService.Instance.IsColorCamera = IsColorCamera;

                //Bin
                BinXRange = GetBinXRange();
                BinYRange = GetBinYRange();
                GetBinX();
                GetBinY();

                //if is color camera and bin not 1, the output image will have wired color
                if (IsColorCamera)
                {
                    //TODO Enable binning if in Unprocessed Mode
                    if (BinX != 1)
                        SetBinX(1);
                    if (BinY != 1)
                        SetBinY(1);
                }

                if (IsPolarCamera)
                {
                    //TODO Enable binning if in Unprocessed Mode
                    _ = GetPolarizationImageType();
                    if (BinX != 1)
                        SetBinX(1);
                    if (BinY != 1)
                        SetBinY(1);
                }

                //ROI
                CameraROIRange = GetROIRange();
                GetROI();
                CameraOrignalROI = CameraROI;

                //LED
                IsLEDSupport = GetIsLEDSupported();
                GetLEDStatus();

                //HotPixelCorrection
                HotPixelCorrectionThresholdRange = GetHotPixelCorrectionThresholdRange();
                IsHotPixelCorrectionSupport = HotPixelCorrectionThresholdRange.Item2 > 0;
                if (IsHotPixelCorrectionSupport)
                {
                    GetHotPixelCorrectionEnabled();
                    GetHotPixelCorrectionThreshold();
                }

                //SensorPixel
                GetSensorPixelWidth();
                GetSensorPixelHeight();

                GetCorrectionModeEnabled();
                GetCorrectionMode();
                GetReverseXEnabled();
                GetReverseYEnabled();

                var frameRateControlValueRange = GetFrameRateControlValueRange();
                IsFrameRateControlSupport = frameRateControlValueRange.Item2 > 0;

                InternalTriggerIntervalRange = new Tuple<double, double>(
                    Math.Round(1000 / frameRateControlValueRange.Item2, 3, MidpointRounding.ToPositiveInfinity),
                    Math.Round(1000 / frameRateControlValueRange.Item1, 3, MidpointRounding.ToNegativeInfinity));


                P2dInfo info = new P2dInfo();
                info.channels = IsColorCamera ? P2dChannels.P2D_CHANNELS_3 : P2dChannels.P2D_CHANNELS_1;
                info.pix_type = BitDepth > 8 ? P2dDataFormat.P2D_16U : P2dDataFormat.P2D_8U;
                info.valid_bits = BitDepth;
                info.x_size = CameraROI.Width;
                info.y_size = CameraROI.Height;
                DisplayService.Instance.InitSlots(10, info, ExposureTimeParams.min_value, GainRange.Item1);
                DisplayService.Instance.PrapareForLive(info);
                DisplayService.Instance.ResumeUpdate(info);

                RegistryCallback();

                if (isOpenNewOne)
                {
                    ResetMinMax(IsColorCamera, BitDepth, ThorCamStatus.None);
                }
                RaisePropertyChanged(nameof(IsCameraConnected));
                return 0;
            }
            catch (Exception e)
            {
                ThorLogger.Log($"Open camera {CurrentCamera.ModelName}(SN:{CurrentCamera.SerialNumber}) failed.", e, ThorLogLevel.Error);
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs($"{CurrentCamera.ModelName},{CurrentCamera.SerialNumber}", ErrorType.OpenCameraFailed));
            }
            return -1;
        }

        public bool OpenCamera(CameraInfo info)
        {
            var result = OpenCameraOnly(info);
            if (result != 0)
            {
                ThorLogger.Log($"Open camera {info.ModelName}(SN:{info.SerialNumber}) failed. Exception message: Error code is {result}.", ThorLogLevel.Error);
                _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs($"{info.ModelName},{info.SerialNumber}", ErrorType.OpenCameraFailed));
                return false;
            }
            if (InitCamera() != 0)
                return false;
            return true;
        }

        public void SetExposureTime(double exposure)
        {
            int result = CameraLIbCommand.SetExposureTime(exposure);
            if (result != 0)
            {
                var msg = $"Set ExposureTime failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            ExposureTime = exposure;
            _exposureChanged = true;
            _skipFrameCount = 0;
        }

        public void GetExposureTime()
        {
            int result = CameraLIbCommand.GetExposureTime(out double exposure);
            if (result != 0)
            {
                throw new Exception($"Get ExposureTime failed! Error code is {result}.");
            }
            ExposureTime = exposure;
        }

        public void SwitchAutoExposureStatus(bool isAutoExposure)
        {
            int result = CameraLIbCommand.SwitchAutoExposureStatus(isAutoExposure);
            if (result != 0)
            {
                throw new Exception($"Switch AutoExposure failed! Error code is {result}.");
            }
            if (!isAutoExposure)
            {
                _exposureChanged = true;
                _skipFrameCount = 0;
            }
        }

        private void GetExposureTimeParams()
        {
            DoubleParams exposureParams = new DoubleParams();
            int result = CameraLIbCommand.GetExposureTimeRange(ref exposureParams);
            if (result != 0)
            {
                throw new Exception($"Get ExposureTime range failed! Error code is {result}.");
            }
            ExposureTimeParams = new DoubleParams(
                Math.Round(exposureParams.min_value, 3, MidpointRounding.ToPositiveInfinity),
                Math.Round(exposureParams.max_value, 3, MidpointRounding.ToNegativeInfinity),
                exposureParams.increment
                );
        }

        public void SetGain(double gain)
        {
            if (!SupportGain) return;
            int result = CameraLIbCommand.SetGain(gain);
            if (result != 0)
            {
                var msg = $"Set Gain failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            Gain = gain;
        }

        public void GetGain()
        {
            if (!SupportGain) return;
            int result = CameraLIbCommand.GetGain(out double gain);
            if (result != 0)
            {
                throw new Exception($"Get Gain failed! Error code is {result}.");
            }
            Gain = gain;
        }

        private Tuple<double, double> GetGainRange()
        {
            int result = CameraLIbCommand.GetGainRange(out double minGain, out double maxGain);
            if (result != 0)
            {
                throw new Exception($"Get Gain range failed! Error code is {result}.");
            }
            return new Tuple<double, double>(Math.Round(minGain, 1, MidpointRounding.ToPositiveInfinity),
                Math.Round(maxGain, 1, MidpointRounding.ToNegativeInfinity));
        }

        public void SetBlackLevel(int blackLevel)
        {
            if (!SupportBlackLevel) return;
            int result = CameraLIbCommand.SetBlackLevel(blackLevel);
            if (result != 0)
            {
                var msg = $"Set BlackLevel level failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            BlackLevel = blackLevel;
        }

        public void GetBlackLevel()
        {
            if (!SupportBlackLevel) return;
            int result = CameraLIbCommand.GetBlackLevel(out int blackLevel);
            if (result != 0)
            {
                throw new Exception($"Get BlackLevel level failed! Error code is {result}.");
            }
            BlackLevel = blackLevel;
        }

        private Tuple<int, int> GetBlackLevelRange()
        {
            int result = CameraLIbCommand.GetBlackLevelRange(out int minBlackLevel, out int maxBlackLevel);
            if (result != 0)
            {
                throw new Exception($"Get BlackLevel range failed! Error code is {result}.");
            }
            return new Tuple<int, int>(minBlackLevel, maxBlackLevel);
        }

        public void SetBinX(int binx)
        {
            int result = CameraLIbCommand.SetBinX(binx);
            if (result != 0)
            {
                var msg = $"Set BinX failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            BinX = binx;
            DisplayService.Instance.BinX = binx;
        }

        public void GetBinX()
        {
            int result = CameraLIbCommand.GetBinX(out int binx);
            if (result != 0)
            {
                throw new Exception($"Get BinX failed! Error code is {result}.");
            }
            BinX = binx;
            DisplayService.Instance.BinX = binx;
        }

        private Tuple<int, int> GetBinXRange()
        {
            int result = CameraLIbCommand.GetBinXRange(out int minBinx, out int maxBinx);
            if (result != 0)
            {
                throw new Exception($"Get BinX range failed! Error code is {result}.");
            }
            return new Tuple<int, int>(minBinx, maxBinx);
        }

        public void SetBinY(int biny)
        {
            int result = CameraLIbCommand.SetBinY(biny);
            if (result != 0)
            {
                var msg = $"Set BinY failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            BinY = biny;
            DisplayService.Instance.BinY = biny;
        }

        public void GetBinY()
        {
            int result = CameraLIbCommand.GetBinY(out int biny);
            if (result != 0)
            {
                throw new Exception($"Get BinY failed! Error code is {result}.");
            }
            BinY = biny;
            DisplayService.Instance.BinY = biny;
        }

        private Tuple<int, int> GetBinYRange()
        {
            int result = CameraLIbCommand.GetBinYRange(out int minBiny, out int maxBiny);
            if (result != 0)
            {
                throw new Exception($"Get BinX range failed! Error code is {result}.");
            }
            return new Tuple<int, int>(minBiny, maxBiny);
        }

        public void SetROI(Int32Rect roi)
        {
            int result = CameraLIbCommand.SetROI(roi.X, roi.Y, roi.Width, roi.Height);
            if (result != 0)
            {
                var msg = $"Set ROI failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            if (PreviousROI == Int32Rect.Empty)
                PreviousROI = CameraROI;

            GetROI();
            ROIChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        public void GetROI()
        {
            int result = CameraLIbCommand.GetROI(out int x, out int y, out int w, out int h);
            if (result != 0)
            {
                var msg = $"Get ROI failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            CameraROI = new Int32Rect(x, y, w, h);
        }

        private ROIRange GetROIRange()
        {
            int result = CameraLIbCommand.GetROIRange(out int tlxmin, out int tlymin, out int brxmin, out int brymin,
                out int tlxmax, out int tlymax, out int brxmax, out int brymax);
            if (result != 0)
            {
                throw new Exception($"Get ROI range failed! Error code is {result}.");
            }
            return new ROIRange(tlxmin, tlxmax, tlymin, tlymax, brxmin, brxmax, brymin, brymax);
        }

        public void StartPreview()
        {
            _frameRateTimer.Enabled = true;
            _frameRateTimer.Start();
            int result = CameraLIbCommand.StartPreview();
            if (result != 0)
            {
                throw new Exception($"Start preview failed! Error code is {result}.");
            }
            ResetMinMax(IsColorCamera, BitDepth, ThorCamStatus.Living);
            DisplayService.Instance.IsRunning = true;
            _preventMemoryOverflowEvent = true;
        }

        public void StopPreview()
        {
            _frameRateTimer.Stop();
            _frameRateTimer.Enabled = false;
            int result = CameraLIbCommand.StopPreview();
            if (result != 0)
            {
                throw new Exception($"Stop preview failed! Error code is {result}.");
            }
            DisplayService.Instance.IsRunning = false;
            _preventMemoryOverflowEvent = false;
        }

        public void StartSnapshot(SnapshotInfo info)
        {
            ResetMinMax(IsColorCamera, BitDepth, ThorCamStatus.Living);
            DisplayService.Instance.IsRunning = true;
            int result = CameraLIbCommand.StartSnapshot(info);
            if (result != 0)
            {
                throw new Exception($"Start preview failed! Error code is {result}.");
            }
            DisplayService.Instance.IsRunning = false;
        }

        public void StopSnapshot()
        {
            int result = CameraLIbCommand.StopSnapshot();
            if (result != 0)
            {
                var msg = $"Stop preview failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            DisplayService.Instance.IsRunning = false;
        }

        private bool _isCapturing = false;
        public int StartCapture(CaptureInfo info, PreparedCallback callback)
        {
            _isCapturing = true;
            DisplayService.Instance.IsRunning = true;
            int result = CameraLIbCommand.StartCapture(info, callback);
            if (result < 0)
            {
                throw new Exception($"Start capture failed! Error code is {result}.");
            }
            DisplayService.Instance.IsRunning = false;
            _preventMemoryOverflowEvent = false;
            _isCapturing = false;
            return result;
        }

        public void StopCapture()
        {
            int result = CameraLIbCommand.StopCapture();
            if (result != 0)
            {
                var msg = $"Stop capture failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            DisplayService.Instance.IsRunning = false;
            _preventMemoryOverflowEvent = false;
            _isCapturing = false;
        }

        private void GetBitDepth()
        {
            int result = CameraLIbCommand.GetBitDepth(out int bitDepth);
            if (result != 0)
            {
                throw new Exception($"Get BitDepth failed! Error code is {result}.");
            }
            BitDepth = bitDepth;
        }


        public Tuple<int, int> GetImageWidthAndHeight()
        {
            int result = CameraLIbCommand.GetImageWidthAndHeight(out int w, out int h);
            if (result != 0)
            {
                throw new Exception($"Get image width and height failed! Error code is {result}.");
            }
            return new Tuple<int, int>(w, h);
        }

        private bool GetColorMode()
        {
            int result = CameraLIbCommand.GetColorMode(out int colorType);
            if (result != 0)
            {
                throw new Exception($"Get color mode failed! Error code is {result}.");
            }
            return colorType == 1;
        }

        private bool GetPolarMode()
        {
            int result = CameraLIbCommand.GetColorMode(out int colorType);
            if (result != 0)
            {
                throw new Exception($"Get color mode failed! Error code is {result}.");
            }
            return colorType == 2;
        }

        public Tuple<double, double> GetFrameRateControlValueRange()
        {
            int result = CameraLIbCommand.GetFrameRateControlValueRange(out double min, out double max);
            if (result != 0)
            {
                throw new Exception($"Get FrameRate Control Value Range failed! Error code is {result}.");
            }
            return new Tuple<double, double>(min, max);
        }

        public void SetFrameRateControlValue(double framerateFps)
        {
            int result = CameraLIbCommand.SetFrameRateControlValue(framerateFps);
            if (result != 0)
            {
                throw new Exception($"Set FrameRate Control Value failed! Error code is {result}.");
            }
        }

        public bool GetIsOperationModeSupported(CameraTriggerMode mode)
        {
            int result = CameraLIbCommand.GetIsOperationModeSupported(mode, out int is_supported);
            if (result != 0)
            {
                throw new Exception($"Get Is Operation Mode Supported failed! Error code is {result}.");
            }
            return is_supported == 1;
        }

        public bool GetIsLEDSupported()
        {
            int result = CameraLIbCommand.GetIsLEDSupported(out int is_supported);
            if (result != 0)
            {
                throw new Exception($"Get Is LED Supported failed! Error code is {result}.");
            }
            return is_supported == 1;
        }

        public void SetLEDStatus(bool enable)
        {
            int result = CameraLIbCommand.SetLEDStatus(enable ? 1 : 0);
            if (result != 0)
            {
                var msg = $"Set LED status failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            IsLEDOn = enable;
        }

        public void GetLEDStatus()
        {
            int result = CameraLIbCommand.GetLEDStatus(out int enable);
            if (result != 0)
            {
                throw new Exception($"Get LED status failed! Error code is {result}.");
            }
            IsLEDOn = enable == 1;
        }

        public void GetHotPixelCorrectionEnabled()
        {
            int result = CameraLIbCommand.GetHotPixelCorrectionEnabled(out int is_enabled);
            if (result != 0)
            {
                throw new Exception($"Get Hot Pixel Correction Enable status failed! Error code is {result}.");
            }
            HotPixelCorrectionEnabled = is_enabled == 1;
        }

        public void SetHotPixelCorrectionEnabled(bool enable)
        {
            int result = CameraLIbCommand.SetHotPixelCorrectionEnabled(enable ? 1 : 0);
            if (result != 0)
            {
                var msg = $"Set Hot Pixel Correction Enable status failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            HotPixelCorrectionEnabled = enable;
        }

        public Tuple<int, int> GetHotPixelCorrectionThresholdRange()
        {
            int result = CameraLIbCommand.GetHotPixelCorrectionThresholdRange(out int min, out int max);
            if (result != 0)
            {
                throw new Exception($"Get Hot Pixel Correction Threshold Range failed! Error code is {result}.");
            }
            return new Tuple<int, int>(min, max);
        }

        public void GetHotPixelCorrectionThreshold()
        {
            int result = CameraLIbCommand.GetHotPixelCorrectionThreshold(out int threshold);
            if (result != 0)
            {
                throw new Exception($"Get Hot Pixel Correction Threshold failed! Error code is {result}.");
            }
            HotPixelCorrectionThreshold = threshold;
        }

        public void SetHotPixelCorrectionThreshold(int threshold)
        {
            int result = CameraLIbCommand.SetHotPixelCorrectionThreshold(threshold);
            if (result != 0)
            {
                var msg = $"Set Hot Pixel Correction Threshold Enable status failed! Error code is {result}.";
                ThorLogger.Log(msg, ThorLogLevel.Error);
                throw new Exception(msg);
            }
            HotPixelCorrectionThreshold = threshold;
        }

        public void GetSensorPixelWidth()
        {
            int result = CameraLIbCommand.GetSensorPixelWidth(out double sensorPixelWidth);
            if (result != 0)
            {
                throw new Exception($"Get Sensor Pixel Width failed! Error code is {result}.");
            }
            //SensorPixelWidth = sensorPixelWidth/ (double)(DisplayService.Instance.TargetObjective);
            DisplayService.Instance.PhysicalWidthPerPixel = (decimal)sensorPixelWidth;
        }

        public void GetSensorPixelHeight()
        {
            int result = CameraLIbCommand.GetSensorPixelHeight(out double sensorPixelHeight);
            if (result != 0)
            {
                throw new Exception($"Get Sensor Pixel Height failed! Error code is {result}.");
            }
            //SensorPixelHeight = sensorPixelHeight/ (double)(DisplayService.Instance.TargetObjective);
            DisplayService.Instance.PhysicalHeightPerPixel = (decimal)sensorPixelHeight;
        }

        public void SetPolarizationImageType(PolarImageTypes polarImageType)
        {
            int result = CameraLIbCommand.SetCurrentPolarImageType((int)polarImageType);
            if (result != 0)
            {
                throw new Exception($"Set Polarization Image Type failed! Error code is {result}.");
            }
            PolarImageType = polarImageType;
        }

        public PolarImageTypes GetPolarizationImageType()
        {
            int result = CameraLIbCommand.GetCurrentPolarImageType(out int polarImageType);
            if (result != 0)
            {
                throw new Exception($"Get Polarization Image Type failed! Error code is {result}.");
            }
            PolarImageType = (PolarImageTypes)polarImageType;
            return (PolarImageTypes)polarImageType;
        }

        public void GetCorrectionModeEnabled()
        {
            int result = CameraLIbCommand.GetCorrectionModeEnabled(out bool isEnabled);
            if (result != 0)
            {
                throw new Exception($"Get CorrectionMode Enabled failed! Error code is {result}.");
            }
            IsCorrectionModeEnabled = isEnabled;
        }

        public void SetCorrectionModeEnabled(bool isEnabled)
        {
            int result = CameraLIbCommand.SetCorrectionModeEnabled(isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set CorrectionMode Enabled failed! Error code is {result}.");
            }
            IsCorrectionModeEnabled = isEnabled;
        }

        public void GetCorrectionMode()
        {
            int result = CameraLIbCommand.GetCorrectionMode(out int mode);
            if (result != 0)
            {
                throw new Exception($"Get CorrectionMode failed! Error code is {result}.");
            }
            CorrectionMode = (CorrectionMode)mode;
        }

        public void SetCorrectionMode(CorrectionMode mode)
        {
            int result = CameraLIbCommand.SetCorrectionMode((int)mode);
            if (result != 0)
            {
                throw new Exception($"Set CorrectionMode failed! Error code is {result}.");
            }
            CorrectionMode = mode;
        }

        public void SetAutoGainEnabled(bool isEnabled)
        {
            int result = CameraLIbCommand.SetGainAutoEnabled(isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set AutoGain Enabled failed! Error code is {result}.");
            }
        }

        public void SetExposureAutoEnabled(bool isEnabled)
        {
            //0 means off; 1 means once; 2 means continue
            int result = CameraLIbCommand.SetExposureAutoEnabled(isEnabled ? 2 : 0);
            if (result != 0)
            {
                throw new Exception($"Set ExposureAuto Enabled failed! Error code is {result}.");
            }
        }

        public void SetReverseXEnabled(bool isEnabled)
        {
            int result = CameraLIbCommand.SetReverseXEnabled(isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set ReverseX Enabled failed! Error code is {result}.");
            }
            IsReverseXEnabled = isEnabled;
        }

        public void GetReverseXEnabled()
        {
            int result = CameraLIbCommand.GetReverseXEnabled(out bool isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set ReverseX Enabled failed! Error code is {result}.");
            }
            IsReverseXEnabled = isEnabled;
        }

        public void SetReverseYEnabled(bool isEnabled)
        {
            int result = CameraLIbCommand.SetReverseYEnabled(isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set ReverseY Enabled failed! Error code is {result}.");
            }
            IsReverseYEnabled = isEnabled;
        }

        public void GetReverseYEnabled()
        {
            int result = CameraLIbCommand.GetReverseYEnabled(out bool isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set ReverseY Enabled failed! Error code is {result}.");
            }
            IsReverseYEnabled = isEnabled;
        }

        public void SetSerialHubEnabled(bool isEnabled)
        {
            int result = CameraLIbCommand.SetSerialHubEnabled(isEnabled);
            if (result != 0)
            {
                throw new Exception($"Set Serial Hub Enabled Failed! Error code is {result}.");
            }
            IsSerialHubEnabled = isEnabled;
        }

        public void GetSerialHubEnabled()
        {
            int result = CameraLIbCommand.GetSerialHubEnabled(out bool isEnabled);
            if (result != 0)
            {
                throw new Exception($"Get Serial Hub Enabled Failed! Error code is {result}.");
            }
            IsSerialHubEnabled = isEnabled;
        }

        public void FindHomePosition()
        {
            int result = CameraLIbCommand.FindHomePosition();
            if (result != 0)
            {
                throw new Exception($"Find Home Position Failed! Error code is {result}.");
            }
        }

        public void JogClockwise()
        {
            int result = CameraLIbCommand.JogClockwise();
            if (result != 0)
            {
                throw new Exception($"Jog Clockwise Failed! Error code is {result}.");
            }
        }

        public void JogCounterClockwise()
        {
            int result = CameraLIbCommand.JogCounterClockwise();
            if (result != 0)
            {
                throw new Exception($"Jog Counter Clockwise Failed! Error code is {result}.");
            }
        }

        public void JogToSlot(int targetSlotIndex)
        {
            int result = CameraLIbCommand.JogToSlot(targetSlotIndex);
            if (result != 0)
            {
                throw new Exception($"Jog To Slot Failed! Error code is {result}.");
            }
        }

        public void MoveConstant()
        {
            int result = CameraLIbCommand.MoveConstant();
            if (result != 0)
            {
                throw new Exception($"Move Constant Failed! Error code is {result}.");
            }
        }

        public void StopMotion()
        {
            int result = CameraLIbCommand.StopMotion();
            if (result != 0)
            {
                throw new Exception($"Stop Motion Failed! Error code is {result}.");
            }
        }

        //slotIndex = -1 means save image without slotindex
        public static void SaveImage(string fileName, int p2dHdl, int slotIndex = -1)
        {
            int result = CameraLIbCommand.SaveImageData(fileName, p2dHdl, slotIndex);
            if (result != 0)
            {
                throw new Exception($"Save image data to file {fileName} failed! Error code is {result}.");
            }
        }
    }
}
