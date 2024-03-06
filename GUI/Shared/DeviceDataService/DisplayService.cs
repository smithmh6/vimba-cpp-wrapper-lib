using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using FilterWheelShared.Common;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;
using FilterWheelShared.Logger;
using Thorlabs.CustomControls.TelerikAndSciChart.Controls.ColorMapEditor;
using System.Threading;
using System.Text.Json.Serialization;
using System.Windows.Navigation;

namespace FilterWheelShared.DeviceDataService
{

    public class SlotSetting : BindableBase
    {
        public virtual double ExposureTime { get; set; } = double.NaN;
        public virtual bool IsAutoExposure { get; set; } = false;
        public virtual double Gain { get; set; } = double.NaN;
        public virtual bool IsAutoGain { get; set; } = false;
        public int Index { get; }
        public SlotSetting(int index)
        {
            Index = index;
        }

        public SlotSetting Clone()
        {
            var clone = new SlotSetting(Index)
            {
                ExposureTime = ExposureTime,
                IsAutoExposure = IsAutoExposure,
                Gain = Gain,
                IsAutoGain = IsAutoGain
            };
            return clone;
        }
    }

    public class SlotParas : BindableBase
    {
        private int _currentSettingIndex = 0;
        public int CurrentSettingIndex
        {
            get => _currentSettingIndex;
            set => SetProperty(ref _currentSettingIndex, value);
        }

        private ThorColor _slotColor = ThorColorService.GetInstance().GrayColor;
        [JsonIgnore]
        public ThorColor SlotColor
        {
            get => _slotColor;
            set
            {
                _slotColor = value;
                Color = _slotColor.Name;
            }
        }

        public string Color { get; set; }
        public SlotSetting[] Settings { get; set; } = new SlotSetting[4];

        public SlotParas Clone()
        {
            var clone = new SlotParas
            {
                CurrentSettingIndex = CurrentSettingIndex,
                SlotColor = SlotColor,
                Color = Color,
                Settings = new SlotSetting[4],
            };
            for (int i = 0; i < Settings.Length; i++)
            {
                clone.Settings[i] = Settings[i].Clone();
            }

            return clone;
        }
    }

    public class Slot : BindableBase
    {
        private string _slotName;
        public string SlotName
        {
            get => _slotName;
            set => SetProperty(ref _slotName, value);
        }

        [JsonIgnore]
        public ImageSource _slotThumbnail;

        [JsonIgnore]
        public ImageSource SlotThumbnail
        {
            get => _slotThumbnail;
            set
            {
                SetProperty(ref _slotThumbnail, value);
            }
        }

        [JsonIgnore]
        public ImageData SlotImage { get; set; }
        public SlotParas SlotParameters { get; set; }

        public Slot Clone()
        {
            Slot clone = new Slot
            {
                SlotName = SlotName,
                SlotThumbnail = null,
                SlotImage = null,
                SlotParameters = SlotParameters.Clone()
            };
            return clone;
        }

        public SlotSetting this[int settingIndex]
        {
            get { return SlotParameters.Settings[settingIndex]; }
        }

        public SlotSetting CurrentSetting => this[SlotParameters.CurrentSettingIndex];
    }

    public class SimpleSlotForColorSettings : BindableBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ThorColor _color;
        public ThorColor Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }
    }

    public enum PipelineBasicFrameType
    {
        ForTemp,
        ForCircle,
    }
    public class PipelineBasicFrame
    {
        public PipelineBasicFrameType FrameType { get; set; }
        public ImageData ImageData { get; set; }
        public int CorrespondingSlotIndex { get; set; }
        public bool IsThumbnailNeed { get; set; }
    }

    public class DisplayRefreshModule
    {
        public static DisplayRefreshModule Instance { get; private set; } = null;
        static DisplayRefreshModule()
        {
            Instance = new DisplayRefreshModule();
        }
        private PipelineBasicFrame _multiplexFrame = null;
        private ImageData _tempImage = null;
        private ImageData _cycleBuf1 = null;
        private ImageData _cycleBuf2 = null;

        private Queue<PipelineBasicFrame> _cycleBackupQueue = new Queue<PipelineBasicFrame>();
        private Queue<PipelineBasicFrame> _updateDisplayQueue = new Queue<PipelineBasicFrame>();
        private bool _isAllowTempFrameEnqueue = false;
        private bool _isAllowCircleFrameEnqueue = false;

        private bool IsSameType(P2dInfo info1, P2dInfo info2)
        {
            if (info1.x_size == info2.x_size
                && info1.y_size == info2.y_size
                && info1.pix_type == info2.pix_type
                && info1.channels == info2.channels)
                return true;
            return false;
        }
        private void UpdateImage(ref ImageData img, P2dInfo info)
        {
            if (img == null)
            {
                img = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, info.channels);
            }
            else
            {
                if (!IsSameType(info, img.DataInfo))
                {
                    img.Dispose();
                    img = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, info.channels);
                }
            }
        }

        private void UpdateMultiplexFrame(P2dInfo info, int slotIndex = -1, ImageData img = null)
        {
            if (_multiplexFrame?.FrameType == PipelineBasicFrameType.ForTemp && _multiplexFrame?.CorrespondingSlotIndex < 0)
                _tempImage.Dispose();

            if (img == null)
                _tempImage = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, info.channels);
            else
                _tempImage = img;

            InitFrame(ref _multiplexFrame, PipelineBasicFrameType.ForTemp, slotIndex, _tempImage);
        }

        private void InitFrame(ref PipelineBasicFrame frame, PipelineBasicFrameType type, int index, ImageData img)
        {
            frame = new PipelineBasicFrame();
            frame.FrameType = PipelineBasicFrameType.ForTemp;
            frame.CorrespondingSlotIndex = index;
            frame.ImageData = img;
        }
        private void PrapareCore(P2dInfo info)
        {
            while (_updateDisplayQueue.Count > 0)
            {
                var tempFrame = _updateDisplayQueue.Dequeue();
                if (tempFrame.FrameType == PipelineBasicFrameType.ForCircle)
                    _cycleBackupQueue.Enqueue(tempFrame);
            }

            if (_cycleBuf1 != null && _cycleBuf2 != null)
            {
                while (true)
                {
                    if (_cycleBackupQueue.Count >= 2)
                        break;
                }
            }

            _cycleBackupQueue.Clear();


            UpdateImage(ref _cycleBuf1, info);
            UpdateImage(ref _cycleBuf2, info);


            var PipelineBasicFrame1 = new PipelineBasicFrame();
            PipelineBasicFrame1.FrameType = PipelineBasicFrameType.ForCircle;
            PipelineBasicFrame1.CorrespondingSlotIndex = -1;
            PipelineBasicFrame1.ImageData = _cycleBuf1;
            _cycleBackupQueue.Enqueue(PipelineBasicFrame1);

            var PipelineBasicFrame2 = new PipelineBasicFrame();
            PipelineBasicFrame2.FrameType = PipelineBasicFrameType.ForCircle;
            PipelineBasicFrame2.CorrespondingSlotIndex = -1;
            PipelineBasicFrame2.ImageData = _cycleBuf2;
            _cycleBackupQueue.Enqueue(PipelineBasicFrame2);
        }


        public void Release()
        {
            if (_cycleBackupQueue?.Count > 0)
            {
                while (_cycleBackupQueue.Count > 0)
                {
                    var PipelineBasicFrame = _cycleBackupQueue.Dequeue();
                    var img = PipelineBasicFrame.ImageData;
                    img?.Dispose();
                }
            }
            _cycleBuf1?.Dispose();
            _cycleBuf1 = null;
            _cycleBuf2?.Dispose();
            _cycleBuf2 = null;
        }

        public void EnableRefresh()
        {
            _isAllowCircleFrameEnqueue = true;
            _isAllowTempFrameEnqueue = true;
        }

        public void DisableRefresh()
        {
            _isAllowCircleFrameEnqueue = false;
            _isAllowTempFrameEnqueue = false;
        }

        public void PrapareForLive(P2dInfo info)
        {
            DisableRefresh();

            PrapareCore(info);
            UpdateMultiplexFrame(info);

            EnableRefresh();
        }
        public void PrapareForCapture(P2dInfo info, ObservableCollection<Slot> slots)
        {
            DisableRefresh();

            PrapareCore(info);

            EnableRefresh();
        }
        public void PrapareForSnapshot(P2dInfo info, ObservableCollection<Slot> slots, int slotIndex)
        {
            DisableRefresh();

            PrapareCore(info);

            EnableRefresh();
        }

        #region For Refresh
        //just use external image
        public void TempFrameEqueueDisplayQueue(ImageData img, int slotIndex = -1)
        {
            if (!_isAllowTempFrameEnqueue) return;

            UpdateMultiplexFrame(img.DataInfo, slotIndex, img);

            _updateDisplayQueue.Enqueue(_multiplexFrame);
        }
        //re use cycle queue buffer , copy
        public void CircleFrameEqueueDisplayQueue(int correspondingSlotIndex, ImageData img)
        {
            if (!_isAllowCircleFrameEnqueue) return;
            if (_cycleBackupQueue == null || _cycleBackupQueue.Count == 0) return;
            var firstFrame = _cycleBackupQueue?.First();
            if (firstFrame != null && firstFrame.ImageData != null)
            {
                firstFrame.CorrespondingSlotIndex = correspondingSlotIndex;
                firstFrame.IsThumbnailNeed = true;

                img.CopyTo(firstFrame.ImageData);

                if (_cycleBackupQueue?.Count > 1)
                {
                    _isAllowTempFrameEnqueue = false;
                    _cycleBackupQueue.Dequeue();
                    _updateDisplayQueue.Enqueue(firstFrame);
                }
            }
        }

        public void MultiplexFrameEqueueDisplayQueue()
        {
            if (_isAllowTempFrameEnqueue && _multiplexFrame?.ImageData != null)
            {
                _updateDisplayQueue.Enqueue(_multiplexFrame);
            }
        }
        public PipelineBasicFrame DequeueDisplayQueue()
        {
            if (!_isAllowCircleFrameEnqueue) return null;

            PipelineBasicFrame frame;
            if (_updateDisplayQueue.Count >= 1)
            {
                frame = _updateDisplayQueue.Dequeue();
                var loops = _updateDisplayQueue.Count;
                for (int i = 1; i < loops; i++)
                {
                    frame = _updateDisplayQueue.Dequeue();
                    if (frame.CorrespondingSlotIndex > 0 && frame.FrameType == PipelineBasicFrameType.ForCircle)
                        break;
                }
                return frame;
            }
            return null;
        }
        public void FrameRecycling(PipelineBasicFrame frame, ObservableCollection<Slot> slots)
        {
            switch (frame.FrameType)
            {
                case PipelineBasicFrameType.ForTemp:
                    break;
                case PipelineBasicFrameType.ForCircle:
                    {
                        if (_isAllowCircleFrameEnqueue)
                        {
                            var index = frame.CorrespondingSlotIndex;
                            var img = frame.ImageData;

                            var tempImg = index >= 0 ? slots[index].SlotImage : _tempImage;
                            img.CopyTo(tempImg);
                            InitFrame(ref _multiplexFrame, PipelineBasicFrameType.ForTemp, index, tempImg);

                            _isAllowTempFrameEnqueue = true;
                        }
                        _cycleBackupQueue.Enqueue(frame);
                        break;
                    }
                default:
                    break;
            }
        }
        #endregion
    }

    public class DisplayService
    {
        //private long totalTime = 0;
        //private int totalIndex = 0;

        public List<Slot> SlotsBackup { get; private set; }

        #region ForUpdate
        private bool _allowTempImageInQueue = false;
        private ImageData _tempImage = null;
        private bool _allowUpdate = true;

        public event EventHandler<ImageData> UpdateWriteableBitmapCallBack;
        public event EventHandler<ImageData> PrepareForUpdateDisplayImageCallBack;
        public event EventHandler<bool> StopResumeCallback;
        public event EventHandler UpdateSizeRatio;

        private readonly Dictionary<ThorColor, byte[]> _slotColorDic = new Dictionary<ThorColor, byte[]>();
        #endregion

        public IEventAggregator EventAggregator;
        public static DisplayService Instance { get; private set; } = null;

        private DisplayService()
        {
            InitColorParameters();
        }
        static DisplayService()
        {
            Instance = new DisplayService();
        }

        #region Properties

        //#region FOROBJECTIVE
        //private ObservableCollection<decimal> _objectiveSource = new ObservableCollection<decimal>() { 1, 4, 10, 20, 40, 60, 100 };
        //public ObservableCollection<decimal> ObjectiveSource => _objectiveSource;

        //private bool _isManual = false;
        //public bool IsManual
        //{
        //    get => _isManual;
        //    set
        //    {
        //        if (_isManual == value) return;
        //        _isManual = value;
        //        TargetObjective = _isManual ? _manualObjective : _selectedObjective;
        //    }
        //}

        //private decimal _selectedObjective = 1;
        //public decimal SelectedObjective
        //{
        //    get => _selectedObjective;
        //    set
        //    {
        //        if (_selectedObjective == value) return;
        //        _selectedObjective = value;
        //        TargetObjective = _selectedObjective;
        //    }
        //}

        //private decimal _manualObjective = 1;
        //public decimal ManualObjective
        //{
        //    get => _manualObjective;
        //    set
        //    {
        //        var target = Math.Clamp(value, (decimal)0.1, 1000);
        //        if (_manualObjective == target) return;
        //        _manualObjective = target;
        //        TargetObjective = _manualObjective;
        //    }
        //}
        //#endregion

        private int _currentSettingIndex = 0;
        public int CurrentSettingIndex
        {
            get => _currentSettingIndex;
            set
            {
                _currentSettingIndex = value;
            }
        }

        private int _currentSlotIndex = 0;
        public int CurrentSlotIndex
        {
            get => _currentSlotIndex;
            set
            {
                _currentSlotIndex = value;
            }
        }


        #region FORRULERANDSCALER
        //For ruler and scaler
        private decimal _targetObjective = 1;
        public decimal TargetObjective
        {
            get => _targetObjective;
            set
            {
                if (_targetObjective == value) return;
                _targetObjective = value;
                UpdateSizeRatio?.Invoke(this, EventArgs.Empty);
                if (_isStatisticsShown || _isProfileShown)
                    UpdateExistingImage();
            }
        }

        private decimal _physicalHeightPerPixel = 1;
        public decimal PhysicalHeightPerPixel
        {
            get => _physicalHeightPerPixel;
            set
            {
                if (_physicalHeightPerPixel == value) return;
                _physicalHeightPerPixel = value;
                UpdateSizeRatio?.Invoke(this, EventArgs.Empty);
                if (_isStatisticsShown || _isProfileShown)
                    UpdateExistingImage();
            }
        }

        private decimal _physicalWidthPerPixel = 1;
        public decimal PhysicalWidthPerPixel
        {
            get => _physicalWidthPerPixel;
            set
            {
                if (_physicalWidthPerPixel == value) return;
                _physicalWidthPerPixel = value;
                UpdateSizeRatio?.Invoke(this, EventArgs.Empty);
                if (_isStatisticsShown || _isProfileShown)
                    UpdateExistingImage();
            }
        }
        #endregion

        //Public properties
        public int BinX { get; set; }
        public int BinY { get; set; }

        private bool _isColorCamera = false;
        public bool IsColorCamera
        {
            get => _isColorCamera;
            set
            {
                _isColorCamera = value;
                _isColorImage = _isColorCamera;
            }
        }
        public bool IsRunning { get; internal set; } = false;

        private bool _isColorImage = false;
        public bool IsColor
        {
            get
            {
                var isColor = false;
                if (IsRunning)
                    isColor = IsColorCamera;
                else
                    isColor = _isColorImage;
                return isColor;
            }
        }

        private PointEx _cursorLocation = null;
        public PointEx CusorLocation
        {
            get => _cursorLocation;
            set
            {
                if (_cursorLocation == value) return;
                _cursorLocation = value;
                UpdateExistingImage();
            }
        }

        private bool _isFlipH = false;
        public bool IsFlipH
        {
            get => _isFlipH;
            set
            {
                _isFlipH = value;
                UpdateExistingImage();
            }
        }
        private bool _isFlipV = false;
        public bool IsFlipV
        {
            get => _isFlipV;
            set
            {
                _isFlipV = value;
                UpdateExistingImage();
            }
        }

        private bool _isProfileShown = false;
        public bool IsProfileShown
        {
            get => _isProfileShown;
            set
            {
                _isProfileShown = value;
                if (_isProfileShown)
                    UpdateExistingImage();
            }
        }
        private bool _isStatisticsShown = false;
        public bool IsStatisticsShown
        {
            get => _isStatisticsShown;
            set
            {
                _isStatisticsShown = value;
                if (_isStatisticsShown)
                    UpdateExistingImage();
            }
        }

        private bool _isPhysicalProfile = true;
        public bool IsPhysicalProfile
        {
            get => _isPhysicalProfile;
            set
            {
                if (_isPhysicalProfile == value) return;
                _isPhysicalProfile = value;
                UpdateExistingImage();
            }
        }

        private int _profileLineWidth = 1;
        public int ProfileLineWidth
        {
            get => _profileLineWidth;
            set
            {
                _profileLineWidth = value;
                UpdateExistingImage();
            }
        }

        //Pixel
        private IntPoint _profileStartPoint = new IntPoint(-1, -1);
        public IntPoint ProfileStartPoint
        {
            get => _profileStartPoint;
            set
            {
                _profileStartPoint = value;
                UpdateExistingImage();
            }
        }
        private IntPoint _profileEndPoint = new IntPoint(-1, -1);
        public IntPoint ProfileEndPoint
        {
            get => _profileEndPoint;
            set
            {
                _profileEndPoint = value;
                UpdateExistingImage();
            }
        }

        private ObservableCollection<Slot> _slots = new ObservableCollection<Slot>();
        public ObservableCollection<Slot> Slots
        {
            get => _slots;
            set
            {
                _slots = value;
            }
        }

        #region --------------------------------------------Mono Camera   
        private ImageData monoTempImage = null;
        private ImageData monoTempImage8u = null;
        private ImageData monoTempGammaImage = null;
        private ImageData monoColorImage = null;


        private byte[] _gammaMonoTable;
        private double _gammaMono = 1;
        public double GammaMono
        {
            get => _gammaMono;
            set
            {
                _gammaMono = value;
                UpdateExistingImage();
            }
        }
        public bool _isAutoScalEnable = false;
        public bool IsAutoScalEnable
        {
            get => _isAutoScalEnable;
            set
            {
                if (_isAutoScalEnable == value) return;
                _isAutoScalEnable = value;
                UpdateExistingImage();
            }
        }

        private int _minMono = 0;
        public int MinMono
        {
            get => _minMono;
            set
            {
                _minMono = value;
            }
        }
        private int _maxMono = 255;
        public int MaxMono
        {
            get => _maxMono;
            set
            {
                _maxMono = value;
            }
        }

        private bool _isHistogramShown = false;
        public bool IsHistogramShown
        {
            get => _isHistogramShown;
            set
            {
                _isHistogramShown = value;
                if (_isHistogramShown)
                    UpdateExistingImage();
            }
        }

        private ThorColor _colorMono = ThorColorService.GetInstance().GrayColor;
        private byte[] _colorTableMono;
        public ThorColor ColorMono
        {
            get => _colorMono;
            set
            {
                _colorMono = value;
                UpdateColorLookUpTable(_colorMono, ChannelType.Mono);
                UpdateExistingImage();
            }
        }

        public ObservableCollection<DoublePoint> ProfileMono;
        public List<ChartPoint> HistogramMono = new List<ChartPoint>();

        public List<StatisticItem> StatisticMono = new List<StatisticItem>();


        public P2dChannels CurrentProcessImgChannels = P2dChannels.P2D_CHANNELS_1;
        #endregion
        #region --------------------------------------------Color Camera
        private ImageData rChannelImg = null;
        private ImageData gChannelImg = null;
        private ImageData bChannelImg = null;
        private ImageData tempChannelImg = null;
        private ImageData rChannelColorImg = null;
        private ImageData gChannelColorImg = null;
        private ImageData bChannelColorImg = null;
        private ImageData CombineColorImage = null;
        //private int totalCount = 0;
        //private long totalTime = 0;

        public List<StatisticItem> StatisticRGB = new List<StatisticItem>();

        //Combine
        private bool _isHistogramCombine = true;
        public bool IsHistogramCombine
        {
            get => _isHistogramCombine;
            set
            {
                _isHistogramCombine = value;
            }
        }

        private double _gammaCombine = 1.0;
        public double GammaCombine
        {
            get => _gammaCombine;
            set
            {
                _gammaCombine = value;
                UpdateExistingImage();
            }
        }

        private int _minCombine = 0;
        public int MinCombine
        {
            get => _minCombine;
            set
            {
                _minCombine = value;
            }
        }

        private int _maxCombine = 255;
        public int MaxCombine
        {
            get => _maxCombine;
            set
            {
                _maxCombine = value;
            }
        }

        private bool _isAutoCombieScaleEnable = false;
        public bool IsAutoCombineScaleEnable
        {
            get => _isAutoCombieScaleEnable;
            set
            {
                if (_isAutoCombieScaleEnable == value) return;
                _isAutoCombieScaleEnable = value;
                UpdateExistingImage();
            }
        }

        //R channel
        private bool _isCheckedR = true;
        public bool IsCheckedR
        {
            get => _isCheckedR;
            set
            {
                _isCheckedR = value;
                ROIStatisticModifyAsChannelEnable(0, _isCheckedR);
                UpdateExistingImage();
            }
        }
        private double _gammaR = 1.0;
        public double GammaR
        {
            get => _gammaR;
            set
            {
                _gammaR = value;
                UpdateExistingImage();
            }
        }

        private bool _isAutoRScalEnable = false;
        public bool IsAutoRScalEnable
        {
            get => _isAutoRScalEnable;
            set
            {
                if (_isAutoRScalEnable == value) return;
                _isAutoRScalEnable = value;
                UpdateExistingImage();
            }
        }

        private int _minR = 0;
        public int MinR
        {
            get => _minR;
            set
            {
                _minR = value;
            }
        }

        private int _maxR = 255;
        public int MaxR
        {
            get => _maxR;
            set
            {
                _maxR = value;
            }
        }

        private byte[] _colorTableR;
        private ThorColor _colorR = ThorColorService.GetInstance().RedColor;
        public ThorColor ColorR
        {
            get => _colorR;
            set
            {
                _colorR = value;
                UpdateColorLookUpTable(_colorR, ChannelType.R);
                UpdateExistingImage();
            }
        }

        public ObservableCollection<DoublePoint> ProfileR;
        public List<ChartPoint> HistogramR = new List<ChartPoint>();

        //G channel
        private bool _isCheckedG = true;
        public bool IsCheckedG
        {
            get => _isCheckedG;
            set
            {
                _isCheckedG = value;
                ROIStatisticModifyAsChannelEnable(1, _isCheckedG);
                UpdateExistingImage();
            }
        }
        private double _gammaG = 1.0;
        public double GammaG
        {
            get => _gammaG;
            set
            {
                _gammaG = value;
                UpdateExistingImage();
            }
        }

        private bool _isAutoGScalEnable = false;
        public bool IsAutoGScalEnable
        {
            get => _isAutoGScalEnable;
            set
            {
                if (_isAutoGScalEnable == value) return;
                _isAutoGScalEnable = value;
                UpdateExistingImage();
            }
        }

        private int _minG = 0;
        public int MinG
        {
            get => _minG;
            set
            {
                _minG = value;
            }
        }

        private int _maxG = 255;
        public int MaxG
        {
            get => _maxG;
            set
            {
                _maxG = value;
            }
        }

        private byte[] _colorTableG;
        private ThorColor _colorG = ThorColorService.GetInstance().GreenColor;
        public ThorColor ColorG
        {
            get => _colorG;
            set
            {
                _colorG = value;
                UpdateColorLookUpTable(_colorG, ChannelType.G);
                UpdateExistingImage();
            }
        }

        public ObservableCollection<DoublePoint> ProfileG;
        public List<ChartPoint> HistogramG = new List<ChartPoint>();

        //B channel
        private bool _isCheckedB = true;
        public bool IsCheckedB
        {
            get => _isCheckedB;
            set
            {
                _isCheckedB = value;
                ROIStatisticModifyAsChannelEnable(2, _isCheckedB);
                UpdateExistingImage();
            }
        }

        private bool _isAutoBScalEnable = false;
        public bool IsAutoBScalEnable
        {
            get => _isAutoBScalEnable;
            set
            {
                if (_isAutoBScalEnable == value) return;
                _isAutoBScalEnable = value;
                UpdateExistingImage();
            }
        }

        private double _gammaB = 1.0;
        public double GammaB
        {
            get => _gammaB;
            set
            {
                _gammaB = value;
                UpdateExistingImage();
            }
        }

        private int _minB = 0;
        public int MinB
        {
            get => _minB;
            set
            {
                _minB = value;
            }
        }

        private int _maxB = 255;
        public int MaxB
        {
            get => _maxB;
            set
            {
                _maxB = value;
            }
        }

        private byte[] _colorTableB;
        public ThorColor _colorB = ThorColorService.GetInstance().BlueColor;
        public ThorColor ColorB
        {
            get => _colorB;
            set
            {
                _colorB = value;
                UpdateColorLookUpTable(_colorB, ChannelType.B);
                UpdateExistingImage();
            }
        }

        public ObservableCollection<DoublePoint> ProfileB;
        public List<ChartPoint> HistogramB = new List<ChartPoint>();


        private bool _isOriginCommandExcute = false;
        public bool IsOriginCommandExcute
        {
            get { return _isOriginCommandExcute; }
            set { _isOriginCommandExcute = value; }
        }

        #endregion
        #endregion

        #region private functions

        private bool IsValidLine(IntPoint startP, IntPoint endP)
        {
            if (startP?.XValue != -1 && startP?.YValue != -1 && endP?.XValue != -1 && endP?.YValue != -1 &&
                !(endP?.YValue - startP?.YValue == 0 && endP?.XValue - startP?.XValue == 0))
            {
                return true;
            }
            return false;
        }
        private void GetHistogramMono(ImageData img, ref List<ChartPoint> histogram)
        {
            int validbit = img.DataInfo.valid_bits;
            int maxV = (1 << validbit);
            //uint[] hist = new uint[maxV + 1];
            uint[] hist = new uint[64];

            img.GetHistogram(ref hist, maxV);

            var chartList = new List<ChartPoint>();
            for (int i = 0; i < 64; i++)
            {
                chartList.Add(new ChartPoint(i, hist[i]));
            }
            histogram = chartList;
        }
        private void GetHistogramColor(ImageData img, int index)
        {
            switch (index)
            {
                case 0:
                    GetHistogramMono(img, ref HistogramR);
                    break;
                case 1:
                    GetHistogramMono(img, ref HistogramG);
                    break;
                case 2:
                    GetHistogramMono(img, ref HistogramB);
                    break;
            }
        }

        private int GetNumDigits(int num)
        {
            try
            {
                return Math.Abs(num).ToString().Length;
            }
            catch (Exception ex)
            {
                throw new Exception($"GetNumDigits function, input num error. Error message:\r\n{ex.ToString()}");
            }
        }
        private ObservableCollection<DoublePoint> GetProfileMono(ImageData img)
        {
            var width = img.DataInfo.x_size;
            var height = img.DataInfo.y_size;

            int sx = ProfileStartPoint.XValue;
            int sy = ProfileStartPoint.YValue;
            int ex = ProfileEndPoint.XValue;
            int ey = ProfileEndPoint.YValue;

            P2dPoint start, end;
            start.x = sx >= width ? width - 1 : sx;
            start.y = sy >= height ? height - 1 : sy;
            end.x = ex >= width ? width - 1 : ex;
            end.y = ey >= height ? height - 1 : ey;

            double xOffset = Math.Abs(end.x - start.x) + 1;
            double yOffset = Math.Abs(end.y - start.y) + 1;
            int length = (int)Math.Sqrt(xOffset * xOffset + yOffset * yOffset);

            var temp = new ushort[length];
            int realLength = 0;
            var status = img.GeProfile(start, end, ProfileLineWidth, temp, ref realLength);
            if (status < 0)
            {
                realLength = 0;
            }

            ObservableCollection<DoublePoint> profile = new ObservableCollection<DoublePoint>();
            if (!_isPhysicalProfile)
            {
                for (int i = 0; i < realLength; i++)
                {
                    profile.Add(new DoublePoint(i, temp[i]));
                }
                return profile;
            }

            var physicalXComponent = xOffset / (double)TargetObjective;
            var physicalYComponent = yOffset / (double)TargetObjective;

            var physicalLength = Math.Sqrt(physicalXComponent * physicalXComponent + physicalYComponent * physicalYComponent);
            var physicalPerPixel = physicalLength / (realLength - 1);
            int realDigits = GetNumDigits((int)TargetObjective);
            int digits = realDigits <= 2 ? 2 : realDigits;

            for (int i = 0; i < realLength; i++)
            {
                var pointX = Math.Round((i * physicalPerPixel), digits, MidpointRounding.AwayFromZero);
                profile.Add(new DoublePoint(pointX, temp[i]));
            }
            return profile;
        }
        private void GetProfileColor(ImageData img, int index)
        {
            switch (index)
            {
                case 0:
                    ProfileR = GetProfileMono(img);
                    break;
                case 1:
                    ProfileG = GetProfileMono(img);
                    break;
                case 2:
                    ProfileB = GetProfileMono(img);
                    break;
            }
        }

        private sbyte GetStatisticColor(ImageData img, ChannelType type)
        {
            var ratio = new Tuple<decimal, decimal>(TargetObjective, TargetObjective);

            if (type == ChannelType.Mono)
            {
                return img.GetROIStatistic(StatisticMono, type, ratio);
            }
            return img.GetROIStatistic(StatisticRGB, type, ratio);
        }

        private void GetMonoMinMaxForAuto(ImageData img)
        {
            if (img.DataInfo.channels != P2dChannels.P2D_CHANNELS_1) return;

            ushort min = 0, max = 0;
            img.GetMinMaxMono(out min, out max);

            var isConvert = img.DataInfo.valid_bits > 8;
            var convertPara = 1 << (img.DataInfo.valid_bits - 8);

            //MinMono = isConvert ? (int)((double)min / convertPara) : min;
            //MaxMono = isConvert ? (int)((double)max / convertPara) : max;
            if (max > min)
            {
                MinMono = min;
                MaxMono = max;
            }
        }

        private void GetColorMinMaxForAuto(ImageData img)
        {
            if (img.DataInfo.channels != P2dChannels.P2D_CHANNELS_3) return;

            byte[] minArr = new byte[3];
            byte[] maxArr = new byte[3];
            var ret = img.GetMinMaxColor(out minArr, out maxArr);

            if (ret == 0)
            {
                if (IsAutoCombineScaleEnable)
                {
                    byte? min = null;
                    byte? max = null;
                    if (IsCheckedR)
                    {
                        min = minArr[0];
                        max = maxArr[0];
                    }
                    if (IsCheckedG)
                    {
                        if (min.HasValue && max.HasValue)
                        {
                            min = Math.Min(min.Value, minArr[1]);
                            max = Math.Max(max.Value, maxArr[1]);
                        }
                        else
                        {
                            min = minArr[1];
                            max = maxArr[1];
                        }
                    }
                    if (IsCheckedB)
                    {
                        if (min.HasValue && max.HasValue)
                        {
                            min = Math.Min(min.Value, minArr[2]);
                            max = Math.Max(max.Value, maxArr[2]);
                        }
                        else
                        {
                            min = minArr[2];
                            max = maxArr[2];
                        }
                    }

                    if (min.HasValue && max.HasValue)
                    {
                        _minCombine = min.Value;
                        _maxCombine = max.Value;
                    }
                    return;
                }

                if (IsAutoRScalEnable)
                {
                    _minR = minArr[0];
                    _maxR = maxArr[0];
                }
                if (IsAutoGScalEnable)
                {
                    _minG = minArr[1];
                    _maxG = maxArr[1];
                }
                if (IsAutoBScalEnable)
                {
                    _minB = minArr[2];
                    _maxB = maxArr[2];
                }
            }
        }

        private void ROIStatisticModifyAsChannelEnable(int channelIndex, bool enable)
        {
            EventAggregator?.GetEvent<ROISelectedEvent>().Publish();

            var items = StatisticRGB.Where(i => i.ChannelType == (ChannelType)channelIndex).ToList();
            foreach (var item in items)
            {
                item.IsChannelEnable = enable;
            }
            if (!enable)
                EventAggregator?.GetEvent<UpdatePopupStatisticEvent>().Publish(0);
        }

        public void InitSlots(int SlotCount, P2dInfo info, double exposureMinLimit = 0, double gainMinLimit = 0)
        {
            if (SlotCount <= 0) return;
            try
            {
                if (Slots.Count == 0)
                {
                    for (int i = 0; i < SlotCount; i++)
                    {
                        ImageData slotImage = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, info.channels);
                        SlotParas SParameters = new SlotParas
                        {
                            Settings = new SlotSetting[4]
                        };
                        for (int si = 0; si < 4; si++)
                        {
                            SlotSetting ss = new SlotSetting(si)
                            {
                                ExposureTime = exposureMinLimit,
                                Gain = gainMinLimit
                            };
                            SParameters.Settings[si] = ss;
                        }
                        Slots.Add(new Slot { SlotName = "Slot" + i.ToString(), SlotImage = slotImage, SlotParameters = SParameters });
                        AddColorIntoLUT(SParameters.SlotColor);
                    }
                }
                else
                {
                    var width = Slots.First().SlotImage.DataInfo.x_size;
                    var height = Slots.First().SlotImage.DataInfo.y_size;
                    if (width != info.x_size || height != info.y_size)
                    {
                        foreach (var slot in Slots)
                        {
                            ImageData slotImage = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, info.channels);
                            if (slot.SlotImage != null)
                            {
                                slot.SlotImage.Dispose();
                            }
                            slot.SlotImage = slotImage;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ;
            }
        }


        private void UpdateExistingImage()
        {
            DisplayRefreshModule.Instance.MultiplexFrameEqueueDisplayQueue();
        }
        public void UpdateCircleImage(int hdl, int CorrespondingSlotIndex = 0, bool IsThumbnailNeed = false)
        {
            var img = new ImageData(hdl);
            DisplayRefreshModule.Instance.CircleFrameEqueueDisplayQueue(CorrespondingSlotIndex, img);
        }
        public void UpdateTempImage(int hdl)
        {
            var img = new ImageData(hdl);
            DisplayRefreshModule.Instance.TempFrameEqueueDisplayQueue(img, -1);
        }


        public void PrapareForLive(P2dInfo info)
        {
            DisplayRefreshModule.Instance.PrapareForLive(info);
        }
        public void PrapareForSnapshot(P2dInfo info)
        {
            DisplayRefreshModule.Instance.PrapareForSnapshot(info, Slots, CurrentSlotIndex);
        }
        public void PrepareForCapture(P2dInfo info)
        {
            DisplayRefreshModule.Instance.PrapareForCapture(info, Slots);
        }


        public void UpdateInterfaceAfterSlotSlection(int slotIndex)
        {
            if (!IsRunning)
            {
                var img = Slots[slotIndex].SlotImage;
                DisplayRefreshModule.Instance.TempFrameEqueueDisplayQueue(img, slotIndex);
            }

            EventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(slotIndex);
        }

        public void UpdateDisplayImgAndThumbnailsAsColorConfig(ObservableCollection<SimpleSlotForColorSettings> settings)
        {
            if (settings.Count != Slots.Count) return;

            DisplayRefreshModule.Instance.DisableRefresh();

            var changeColorList = new List<int>();
            for (int i = 0; i < settings.Count; i++)
            {
                Slots[i].SlotName = settings[i].Name;
                if (Slots[i].SlotParameters.SlotColor != settings[i].Color)
                {
                    Slots[i].SlotParameters.SlotColor = settings[i].Color;
                    AddColorIntoLUT(settings[i].Color);
                    changeColorList.Add(i);
                }
            }

            if (changeColorList.Count > 0)
            {
                foreach (var index in changeColorList)
                {
                    Slots[index].SlotThumbnail = GenerateSlotThumbnail(index);
                }
            }

            DisplayRefreshModule.Instance.EnableRefresh();
            UpdateExistingImage();
        }

        public void UpdateDisplayImgAndThumbnailsAsLogSelection(List<ImageData> slotImgs)
        {
            if (slotImgs.Count != Slots.Count) return;

            DisplayRefreshModule.Instance.DisableRefresh();

            for (int i = 0; i < slotImgs.Count; i++)
            {
                if (slotImgs[i] == null)
                {
                    Slots[i].SlotImage.Reset();
                }
                else
                {
                    if (Slots[i].SlotImage != null)
                    {
                        Slots[i].SlotImage.Dispose();
                    }
                    Slots[i].SlotImage = slotImgs[i];
                }                
                Slots[i].SlotThumbnail = GenerateSlotThumbnail(i);
            }

            DisplayRefreshModule.Instance.EnableRefresh();

            UpdateInterfaceAfterSlotSlection(CurrentSlotIndex);
        }

        public void ParseConfiguration(ObservableCollection<Slot> slotSettings)
        {
            if (slotSettings == null || slotSettings.Count != Slots.Count) return;
            for (int i = 0; i < slotSettings.Count; i++)
            {
                Slots[i].SlotName = slotSettings[i].SlotName;
                Slots[i].SlotParameters.CurrentSettingIndex = slotSettings[i].SlotParameters.CurrentSettingIndex;
                Slots[i].SlotParameters.Settings = slotSettings[i].SlotParameters.Settings;

                var color = ThorColorService.GetInstance().GetCorrespondingColor(slotSettings[i].SlotParameters.Color);
                Slots[i].SlotParameters.SlotColor = color;
                AddColorIntoLUT(color);
            }
        }

        public void InitBackupSlots()
        {
            var backup = new List<Slot>();
            foreach (var slot in Slots)
            {
                backup.Add(slot.Clone());
            }
            SlotsBackup = backup;
        }

        public void UpdateBackupSlots(int? slotIndex = null, int? settingIndex = null)
        {
            var slotIndex1 = slotIndex == null ? CurrentSlotIndex : slotIndex.Value;
            var settingIndex1 = settingIndex == null ? CurrentSettingIndex : settingIndex.Value;
            SlotsBackup[slotIndex1].SlotParameters.CurrentSettingIndex = settingIndex1;
            var setting = Slots[slotIndex1][settingIndex1];
            var backupSetting = SlotsBackup[slotIndex1][settingIndex1];
            backupSetting.ExposureTime = setting.ExposureTime;
            backupSetting.IsAutoExposure = setting.IsAutoExposure;
            backupSetting.Gain = setting.Gain;
            backupSetting.IsAutoGain = setting.IsAutoGain;
        }

        public byte[] GetCurrentColorLUT(ThorColor currentColor)
        {
            if (_slotColorDic.ContainsKey(currentColor))
            {
                return _slotColorDic[currentColor];
            }
            else
            {
                byte[] currentColorLUT = currentColor.GetData(CustomPixelFormat.RGB).ToArray();
                _slotColorDic.Add(currentColor, currentColorLUT);
                return currentColorLUT;
            }
        }

        private void AddColorIntoLUT(ThorColor currentColor)
        {
            if (!_slotColorDic.ContainsKey(currentColor))
            {
                byte[] currentColorLUT = currentColor.GetData(CustomPixelFormat.RGB).ToArray();
                _slotColorDic.Add(currentColor, currentColorLUT);
            }
        }

        private ImageSource GenerateThumbnail(ImageData data)
        {
            //ImageSource img = null;
            //using (ImageData thumbImg = new ImageData(100, 100, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3))
            //{
            //    ImageData.ResizeImg(data, thumbImg);
            //    img = thumbImg.ToBitmapSource();
            //}
            ImageSource img = data.ToBitmapSource(100);
            return img;
        }

        private ImageData monoThumbnailTempImg;
        private ImageData monoThumbnailTempImg2;
        private ImageData monoThumbnailTempImg8U;
        private ImageData monoThumbnailTempColorImg;

        private void PrepareSlotThumbnailTransitionImg(P2dInfo info)
        {
            var tempInfo = new P2dInfo();

            var dstMaxXY = 100;
            int imgX = info.x_size;
            int imgY = info.y_size;
            if (imgX > imgY)
            {
                tempInfo.y_size = (int)(dstMaxXY * ((double)imgY / imgX));
                tempInfo.x_size = dstMaxXY;
            }
            else
            {
                tempInfo.x_size = (int)(dstMaxXY * ((double)imgX / imgY));
                tempInfo.y_size = dstMaxXY;
            }

            tempInfo.pix_type = info.pix_type;
            tempInfo.valid_bits = info.valid_bits;
            GenerateTempImage(ref monoThumbnailTempImg, tempInfo, info.channels);
            GenerateTempImage(ref monoThumbnailTempImg2, tempInfo, info.channels);

            tempInfo.pix_type = P2dDataFormat.P2D_8U;
            tempInfo.valid_bits = 8;
            GenerateTempImage(ref monoThumbnailTempImg8U, tempInfo, P2dChannels.P2D_CHANNELS_1);

            tempInfo.pix_type = P2dDataFormat.P2D_8U;
            tempInfo.valid_bits = 8;
            GenerateTempImage(ref monoThumbnailTempColorImg, tempInfo, P2dChannels.P2D_CHANNELS_3);
        }

        private ImageSource GenerateSlotThumbnail(int slotIndex)
        {
            var data = Slots[slotIndex].SlotImage;
            PrepareSlotThumbnailTransitionImg(data.DataInfo);

            ImageSource img = null;
            ImageData.ResizeImg(data, monoThumbnailTempImg);
            ImageData.ProcessWithMinMaxNew(monoThumbnailTempImg, monoThumbnailTempImg2, MinMono, MaxMono);

            var slotcolor = Slots[slotIndex].SlotParameters.SlotColor;
            var slotcolorlut = GetCurrentColorLUT(slotcolor);

            monoThumbnailTempColorImg.Reset();
            switch (data.DataInfo.pix_type)
            {
                case P2dDataFormat.P2D_8U:
                    ImageData.ProcessColorWithLUTNew(monoThumbnailTempImg2, monoThumbnailTempColorImg, slotcolor, slotcolorlut);
                    break;
                case P2dDataFormat.P2D_16U:
                    monoThumbnailTempImg2.ImageScale16uTo8u(monoThumbnailTempImg8U, 0, 255);
                    ImageData.ProcessColorWithLUTNew(monoThumbnailTempImg8U, monoThumbnailTempColorImg, slotcolor, slotcolorlut);
                    break;
            }
            img = monoThumbnailTempColorImg.ToBitmapSource();
            return img;
        }




        private void GenerateTempImage(ref ImageData img, P2dInfo info, P2dChannels channels)
        {
            if (img == null)
                img = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, channels);
            else
            {
                if (img.DataInfo.x_size != info.x_size || img.DataInfo.y_size != info.y_size
                    || img.DataInfo.pix_type != info.pix_type || img.DataInfo.valid_bits != info.valid_bits
                    || img.DataInfo.channels != channels)
                {
                    img.Dispose();
                    img = new ImageData(info.x_size, info.y_size, info.pix_type, info.valid_bits, channels);
                }
            }
        }


        private void UpdateColorLookUpTable(ThorColor color, ChannelType ctype)
        {
            switch (ctype)
            {
                case ChannelType.R:
                    _colorTableR = color.GetData(CustomPixelFormat.RGB).ToArray();
                    break;
                case ChannelType.G:
                    _colorTableG = color.GetData(CustomPixelFormat.RGB).ToArray();
                    break;
                case ChannelType.B:
                    _colorTableB = color.GetData(CustomPixelFormat.RGB).ToArray();
                    break;
                case ChannelType.Mono:
                    _colorTableMono = color.GetData(CustomPixelFormat.RGB).ToArray();
                    break;
            }
        }

        private byte[] UpdateGammaLookUpTable(double gamma)
        {
            var gammaTable = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double unification = (double)i / 255;
                double cal = 255.0 * Math.Pow(unification, gamma);
                gammaTable[i] = Convert.ToByte(cal);
            }
            return gammaTable;
        }
        private void InitColorParameters()
        {
            UpdateColorLookUpTable(_colorMono, ChannelType.Mono);
            UpdateColorLookUpTable(_colorR, ChannelType.R);
            UpdateColorLookUpTable(_colorG, ChannelType.G);
            UpdateColorLookUpTable(_colorB, ChannelType.B);
        }

        //for mono
        private void PreProcessMono(P2dInfo info)
        {
            GenerateTempImage(ref monoTempImage, info, P2dChannels.P2D_CHANNELS_1);
            info.pix_type = P2dDataFormat.P2D_8U;
            info.valid_bits = 8;
            GenerateTempImage(ref monoTempImage8u, info, P2dChannels.P2D_CHANNELS_1);
            GenerateTempImage(ref monoTempGammaImage, info, P2dChannels.P2D_CHANNELS_1);

            GenerateTempImage(ref monoColorImage, info, P2dChannels.P2D_CHANNELS_3);
        }
        private void ProcessMono(PipelineBasicFrame frame)
        {
            var data = frame.ImageData;
            var info = data.DataInfo;
            if (_isAutoScalEnable)
            {
                _isAutoScalEnable = false;
                GetMonoMinMaxForAuto(data);
                EventAggregator?.GetEvent<UpdatePopupHistogramMinMaxEvent>().Publish(P2dChannels.P2D_CHANNELS_1);
            }

            ImageData.ProcessWithMinMaxNew(data, monoTempImage, MinMono, MaxMono);
            //Flip
            ImageData.ProcessWithFlipI(monoTempImage, IsFlipH, IsFlipV);
            //Pixel color
            if (CusorLocation != null)
            {
                monoTempImage.GetOnePixelData(_cursorLocation.PixelPoint.XValue, _cursorLocation.PixelPoint.YValue, out int pixelValue);
                EventAggregator?.GetEvent<UpdatePixelDataEvent>().Publish(new PixelDataMono(_cursorLocation.PixelPoint, pixelValue));
            }

            //Histogram
            if (_isHistogramShown)
                GetHistogramMono(monoTempImage, ref HistogramMono);

            //Profile
            if (_isProfileShown)
            {
                if (IsValidLine(ProfileStartPoint, ProfileEndPoint))
                    ProfileMono = GetProfileMono(monoTempImage);
                else
                    ProfileMono = null;
            }

            //Statistic
            if (_isStatisticsShown)
                GetStatisticColor(monoTempImage, ChannelType.Mono);



            ThorColor currentSlotColor;
            byte[] currentSlotColorLUT;
            if (frame.CorrespondingSlotIndex >= 0)
            {
                currentSlotColor = Slots[frame.CorrespondingSlotIndex].SlotParameters.SlotColor;
                currentSlotColorLUT = GetCurrentColorLUT(currentSlotColor);
                //_colorTableMono= 
            }
            else
            {
                currentSlotColor = Slots[CurrentSlotIndex].SlotParameters.SlotColor;
                currentSlotColorLUT = GetCurrentColorLUT(currentSlotColor);
            }


            monoColorImage.Reset();
            //The follow procedure need to transform the 16u image to 8u
            if (Math.Abs(GammaMono - 1) > 1e-6)
            {
                var gammaTable = UpdateGammaLookUpTable(_gammaMono);
                switch (data.DataInfo.pix_type)
                {
                    case P2dDataFormat.P2D_8U:
                        ImageData.GammaCorrection(monoTempImage, monoTempGammaImage, gammaTable);
                        ImageData.ProcessColorWithLUTNew(monoTempGammaImage, monoColorImage, currentSlotColor, currentSlotColorLUT);
                        break;
                    case P2dDataFormat.P2D_16U:
                        monoTempImage.ImageScale16uTo8u(monoTempImage8u, 0, 255);
                        ImageData.GammaCorrection(monoTempImage8u, monoTempGammaImage, gammaTable);
                        ImageData.ProcessColorWithLUTNew(monoTempGammaImage, monoColorImage, currentSlotColor, currentSlotColorLUT);
                        break;
                }
            }
            else
            {
                switch (data.DataInfo.pix_type)
                {
                    case P2dDataFormat.P2D_8U:
                        ImageData.ProcessColorWithLUTNew(monoTempImage, monoColorImage, currentSlotColor, currentSlotColorLUT);
                        break;
                    case P2dDataFormat.P2D_16U:
                        monoTempImage.ImageScale16uTo8u(monoTempImage8u, 0, 255);
                        ImageData.ProcessColorWithLUTNew(monoTempImage8u, monoColorImage, currentSlotColor, currentSlotColorLUT);
                        break;
                }
            }
            if (frame.CorrespondingSlotIndex >= 0 && frame.FrameType == PipelineBasicFrameType.ForCircle)
            {
                CurrentSlotIndex = frame.CorrespondingSlotIndex;
                var processSlot = Slots[frame.CorrespondingSlotIndex];
                //data.CopyTo(processSlot.SlotImage);
                var tempThumbnail = GenerateThumbnail(monoColorImage);
                tempThumbnail?.Freeze();
                processSlot.SlotThumbnail = tempThumbnail;
                EventAggregator?.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(CurrentSlotIndex);
            }

            if (_allowUpdate)
            {
                PrepareForUpdateDisplayImageCallBack?.Invoke(this, monoColorImage);
                EventAggregator?.GetEvent<UpdateDisplayImageEvent>().Publish(new Tuple<ImageData, bool>(monoColorImage, frame.IsThumbnailNeed));
                System.Threading.Thread.Yield();
            }
        }
        //for color
        private void PreProcessColor(P2dInfo info)
        {
            GenerateTempImage(ref rChannelImg, info, P2dChannels.P2D_CHANNELS_1);
            GenerateTempImage(ref gChannelImg, info, P2dChannels.P2D_CHANNELS_1);
            GenerateTempImage(ref bChannelImg, info, P2dChannels.P2D_CHANNELS_1);
            GenerateTempImage(ref tempChannelImg, info, P2dChannels.P2D_CHANNELS_1);

            GenerateTempImage(ref rChannelColorImg, info, P2dChannels.P2D_CHANNELS_3);
            GenerateTempImage(ref gChannelColorImg, info, P2dChannels.P2D_CHANNELS_3);
            GenerateTempImage(ref bChannelColorImg, info, P2dChannels.P2D_CHANNELS_3);

            GenerateTempImage(ref CombineColorImage, info, P2dChannels.P2D_CHANNELS_3);
        }
        private void ProcessColor(PipelineBasicFrame frame)
        {
            var data = frame.ImageData;

            if (_isAutoCombieScaleEnable || _isAutoRScalEnable || _isAutoGScalEnable || _isAutoBScalEnable)
            {
                GetColorMinMaxForAuto(data);
                _isAutoRScalEnable = false;
                _isAutoGScalEnable = false;
                _isAutoBScalEnable = false;
                _isAutoCombieScaleEnable = false;
                EventAggregator?.GetEvent<UpdatePopupHistogramMinMaxEvent>().Publish(P2dChannels.P2D_CHANNELS_3);
            }

            bool[] channelCheckedStatus = { IsCheckedR, IsCheckedG, IsCheckedB };
            if (channelCheckedStatus.All(c => !c))
            {
                EventAggregator?.GetEvent<UpdatePopupStatisticEvent>().Publish(0);
                UpdateWriteableBitmapCallBack?.Invoke(this, null);
                return;
            }

            IntPoint pixelLocation = null;
            DoublePoint physicalLocation = null;
            if (CusorLocation != null)
            {
                pixelLocation = new IntPoint(CusorLocation.PixelPoint);
                physicalLocation = new DoublePoint(CusorLocation.PhysicalPoint);
            }

            double[] gammas;
            IntPoint[] minMaxs;
            if (IsHistogramCombine)
            {
                gammas = new double[3] { GammaCombine, GammaCombine, GammaCombine };
                var pointCombine = new IntPoint(MinCombine, MaxCombine);
                minMaxs = new IntPoint[3] { pointCombine, pointCombine, pointCombine };
            }
            else
            {
                gammas = new double[3] { GammaR, GammaG, GammaB };
                minMaxs = new IntPoint[3] { new IntPoint(MinR, MaxR), new IntPoint(MinG, MaxG), new IntPoint(MinB, MaxB) };
            }
            ThorColor[] colors = { ColorR, ColorG, ColorB };
            var dataInfo = data.DataInfo;

            ImageData.CopyColorToRGBChannelImages(data, rChannelImg, gChannelImg, bChannelImg);

            ImageData[] images = { rChannelImg, gChannelImg, bChannelImg };
            ImageData[] colorImages = { rChannelColorImg, gChannelColorImg, bChannelColorImg };
            List<byte[]> colorTable = new List<byte[]> { _colorTableR, _colorTableG, _colorTableB };

            List<ImageData> combingImages = new List<ImageData>();
            int?[] pixelData = new int?[3] { null, null, null };
            for (int index = 0; index < channelCheckedStatus.Count(); index++)
            {
                if (channelCheckedStatus[index])
                {
                    var info = data.DataInfo;
                    ImageData tempImg = images[index];
                    ImageData.ProcessWithMinMaxI(tempImg, minMaxs[index].XValue, minMaxs[index].YValue);

                    //Flip
                    ImageData.ProcessWithFlipI(tempImg, IsFlipH, IsFlipV);

                    //Pixel color
                    if (pixelLocation != null)
                    {
                        tempImg.GetOnePixelData(pixelLocation.XValue, pixelLocation.YValue, out int pixelValue);
                        pixelData[index] = pixelValue;
                    }

                    //Histogram
                    if (_isHistogramShown)
                        GetHistogramColor(tempImg, index);

                    //Profile
                    if (_isProfileShown)
                    {
                        if (IsValidLine(ProfileStartPoint, ProfileEndPoint))
                            GetProfileColor(tempImg, index);
                        else
                        {
                            ProfileR = null;
                            ProfileG = null;
                            ProfileB = null;
                        }
                    }

                    //Statistic
                    if (_isStatisticsShown)
                        GetStatisticColor(tempImg, (ChannelType)index);

                    //if (Math.Abs(gammas[index] - 1) > 1e-6)
                    //    ImageData.GammaCorrectionI(tempImg, gammas[index]);
                    colorImages[index].Reset();
                    if (Math.Abs(gammas[index] - 1) > 1e-6)
                    {
                        var gammaTable = UpdateGammaLookUpTable(gammas[index]);
                        ImageData.GammaCorrection(tempImg, tempChannelImg, gammaTable);
                        ImageData.ProcessColorWithLUTNew(tempChannelImg, colorImages[index], colors[index], colorTable[index]);
                    }
                    else
                    {
                        ImageData.ProcessColorWithLUTNew(tempImg, colorImages[index], colors[index], colorTable[index]);
                    }
                    combingImages.Add(colorImages[index]);
                }
            }

            if (pixelLocation != null)
                EventAggregator?.GetEvent<UpdatePixelDataEvent>().Publish(new PixelDataRGB(pixelLocation, pixelData[0], pixelData[1], pixelData[2]));

            try
            {

                if (combingImages.Count > 0)
                {
                    CombineColorImage.Reset();
                    ImageData.Combine(combingImages.ToArray(), CombineColorImage);
                    if (_allowUpdate)
                    {
                        PrepareForUpdateDisplayImageCallBack?.Invoke(this, CombineColorImage);
                        EventAggregator?.GetEvent<UpdateDisplayImageEvent>().Publish(new Tuple<ImageData, bool>(CombineColorImage, frame.IsThumbnailNeed));
                        System.Threading.Thread.Yield();
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    UpdateWriteableBitmapCallBack?.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Combine images failed.");
            }
            finally
            {
            }
        }

        private void DataProcess(PipelineBasicFrame frame)
        {
            //var sw = new System.Diagnostics.Stopwatch();
            //sw.Start();

            ImageData data = frame.ImageData;
            if (data != null && data.IsDisposed) return;

            var info = data.DataInfo;
            CurrentProcessImgChannels = info.channels;

            if (info.channels == P2dChannels.P2D_CHANNELS_1)
            {
                PreProcessMono(info);
                ProcessMono(frame);
            }
            else
            {
                PreProcessColor(info);
                ProcessColor(frame);
            }
            // Notify data update
            EventAggregator?.GetEvent<UpdatePopupHistogramEvent>().Publish(info.channels);
            EventAggregator?.GetEvent<UpdatePopupProfileEvent>().Publish(info.channels);
            EventAggregator?.GetEvent<UpdatePopupStatisticEvent>().Publish(0);

            //sw.Stop();
            //var t = sw.ElapsedMilliseconds;
            //ThorLogger.Log(t.ToString(), ThorLogWrapper.ThorLogLevel.Error);
        }
        #endregion

        #region public functions

        #region For ROI change in statistic 
        public void ROIAdd(int ROIIndex, P2dRect ROIPixel, System.Windows.Rect ROIPhysical, SolidColorBrush ROIBrush, P2dRoiType ROIType)
        {
            if (!IsColor)
            {
                var item = new StatisticItem
                {
                    Index = ROIIndex,
                    ROIPixel = ROIPixel,
                    ROIPhysical = ROIPhysical,
                    ROIBrush = ROIBrush,
                    ROIType = ROIType,
                    ChannelType = ChannelType.Mono
                };
                StatisticMono.Add(item);
                StatisticMono.Sort();
            }
            else
            {
                bool[] isChannelsEnable = { IsCheckedR, IsCheckedG, IsCheckedB };

                for (int i = 0; i < 3; i++)
                {
                    var item = new StatisticItem
                    {
                        Index = ROIIndex,
                        ROIPixel = ROIPixel,
                        ROIPhysical = ROIPhysical,
                        ROIBrush = ROIBrush,
                        ROIType = ROIType,
                        ChannelType = (ChannelType)i,
                        IsChannelEnable = isChannelsEnable[i]
                    };
                    StatisticRGB.Add(item);
                }
                StatisticRGB.Sort();
            }
            UpdateExistingImage();
        }
        public void ROISelected(int ROIIndex, bool isSelected)
        {
            if (!IsColor)
            {
                if (ROIIndex == -1)
                {
                    StatisticMono.ForEach(i => i.IsSelected = isSelected);
                }
                else
                {
                    var item = StatisticMono.FirstOrDefault(i => i.Index == ROIIndex);
                    if (item != null)
                        item.IsSelected = isSelected;
                }
            }
            else
            {
                if (ROIIndex == -1)
                {
                    StatisticRGB.ForEach(i => i.IsSelected = isSelected);
                }
                else
                {
                    var items = StatisticRGB.Where(i => i.Index == ROIIndex).ToList();
                    foreach (var item in items)
                    {
                        item.IsSelected = isSelected;
                    }
                }
            }
            EventAggregator?.GetEvent<ROISelectedEvent>().Publish();
        }
        public void ROIModify(int ROIIndex, P2dRect ROIPixel, System.Windows.Rect ROIPhysical)
        {
            if (!IsColor)
            {
                var item = StatisticMono.Where(i => i.Index == ROIIndex).FirstOrDefault();
                if (item != null)
                {
                    item.ROIPixel = ROIPixel;
                    item.ROIPhysical = ROIPhysical;
                }
            }
            else
            {
                var items = StatisticRGB.Where(i => i.Index == ROIIndex).ToList();
                foreach (var item in items)
                {
                    item.ROIPixel = ROIPixel;
                    item.ROIPhysical = ROIPhysical;
                }
            }
            UpdateExistingImage();
        }
        public void ROIDelete(List<int> ROIIndexs)
        {
            if (!IsColor)
            {
                var items = StatisticMono.Where(i => ROIIndexs.Contains(i.Index)).ToList();
                foreach (var item in items)
                {
                    StatisticMono.Remove(item);
                }
            }
            else
            {
                var items = StatisticRGB.Where(i => ROIIndexs.Contains(i.Index)).ToList();
                foreach (var item in items)
                {
                    StatisticRGB.Remove(item);
                }
            }
            EventAggregator?.GetEvent<UpdatePopupStatisticEvent>().Publish(0);
            UpdateExistingImage();
        }
        public void UpdateMinMax(int min, int max, ChannelType ctype, bool updateExisting = true)
        {
            switch (ctype)
            {
                case ChannelType.Mono:
                    _minMono = min;
                    _maxMono = max;
                    break;
                case ChannelType.R:
                    _minR = min;
                    _maxR = max;
                    break;
                case ChannelType.G:
                    _minG = min;
                    _maxG = max;
                    break;
                case ChannelType.B:
                    _minB = min;
                    _maxB = max;
                    break;
                case ChannelType.COMBINE:
                    UpdateMinMaxCombine(min, max, false);
                    break;
            }
            if (updateExisting)
                UpdateExistingImage();
        }
        public void UpdateGamma(ChannelType channel, double gamma, bool updateExisting = true)
        {
            switch (channel)
            {
                case ChannelType.Mono:
                    _gammaMono = gamma;
                    break;
                case ChannelType.R:
                    _gammaR = gamma;
                    break;
                case ChannelType.G:
                    _gammaG = gamma;
                    break;
                case ChannelType.B:
                    _gammaB = gamma;
                    break;
            }
            if (updateExisting)
                UpdateExistingImage();
        }
        #endregion

        public void UpdateCameraParameters()
        {

        }


        public void DoUpdateSizeRatio()
        {
            UpdateSizeRatio?.Invoke(this, EventArgs.Empty);
        }

        #region for combine RGB histogram window
        public void UpdateMinMaxCombine(int min, int max, bool updateExisting = true)
        {
            _minCombine = min;
            _maxCombine = max;

            if (!IsCheckedR && !IsCheckedG && !IsCheckedB)
                return;

            if (updateExisting)
                UpdateExistingImage();
        }

        public void UpdateGammaCombine(double gamma, bool updateExisting = true)
        {
            _gammaCombine = gamma;

            if (!IsCheckedR && !IsCheckedG && !IsCheckedB)
                return;

            if (updateExisting)
                UpdateExistingImage();
        }

        public void SetRGBHistogramRGBShow(bool value)
        {
            _isHistogramShown = value;
            UpdateExistingImage();
        }
        #endregion

        #region For Update



        public void StopUpdate()
        {
            StopResumeCallback?.Invoke(null, false);
            _allowUpdate = false;
            //ClearQueue();
            DisplayRefreshModule.Instance.Release();
        }

        private Task _processTask = null;
        public void ResumeUpdate(P2dInfo info)
        {
            StopResumeCallback?.Invoke(null, true);

            _allowUpdate = true;
            if (_processTask == null || _processTask.IsCompleted || _processTask.IsFaulted || _processTask.IsCanceled || _processTask.IsCompletedSuccessfully)
            {
                _processTask = Task.Run(() =>
                {
                    while (true)
                    {
                        PipelineBasicFrame frame = DisplayRefreshModule.Instance.DequeueDisplayQueue();
                        if (frame != null)
                        {
                            try
                            {
                                DataProcess(frame);
                            }
                            catch (Exception e)
                            {
                                ThorLogger.Log("Process Error.", e, ThorLogWrapper.ThorLogLevel.Error);
                            }
                            finally
                            {
                                DisplayRefreshModule.Instance.FrameRecycling(frame, Slots);
                            }
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                });
            }
        }
        #endregion
        #endregion
    }
}
