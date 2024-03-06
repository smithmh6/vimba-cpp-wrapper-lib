using Prism.Events;
using Prism.Mvvm;
using Prism.Commands;
using Prism.Ioc;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;

namespace Settings.ViewModels
{
    public class CameraSettingsViewModelBase : BindableBase
    {
        public virtual int Top { get; set; }
        public virtual int Left { get; set; }
        public virtual int Bottom { get; set; }
        public virtual int Right { get; set; }
        public virtual int BinX { get; set; }
        public virtual int BinY { get; set; }

        private bool _isEnableCapturingImageUpdate;
        public virtual bool IsEnableCapturingImageUpdate
        {
            get => _isEnableCapturingImageUpdate;
            set => SetProperty(ref _isEnableCapturingImageUpdate, value);
        }

        private HardwareTriggerPolarity _hardwareTrigger = HardwareTriggerPolarity.TriggerPolarityActiveHigh;
        public virtual HardwareTriggerPolarity HardwareTrigger
        {
            get => _hardwareTrigger;
            set => SetProperty(ref _hardwareTrigger, value);
        }

    }
    public class CameraSettingsViewModel : CameraSettingsViewModelBase, IJson
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();
        private ThorlabsCamera _camera = ThorlabsCamera.Instance;
        private readonly IEventAggregator _eventAggregator;

        #region UI Properties

        #region ROI
        private int _top;
        public override int Top
        {
            get => _top;
            set
            {
                var target = System.Math.Clamp(value, MinTop, MaxTop);
                SetProperty(ref _top, target);
                RaisePropertyChanged(nameof(MinBottom));
            }
        }

        public int MinTop => _camera.CameraROIRange.TopLeftYMin;
        public int MaxTop => System.Math.Min(_camera.CameraROIRange.TopLeftYMax, _bottom - 1);

        private int _left;
        public override int Left
        {
            get => _left;
            set
            {
                var target = System.Math.Clamp(value, MinLeft, MaxLeft);
                SetProperty(ref _left, target);
                RaisePropertyChanged(nameof(MinRight));
            }
        }

        public int MinLeft => _camera.CameraROIRange.TopLeftXMin;
        public int MaxLeft => System.Math.Min(_camera.CameraROIRange.TopLeftXMax, _right - 1);

        private int _bottom;
        public override int Bottom
        {
            get => _bottom;
            set
            {
                var target = System.Math.Clamp(value, MinBottom, MaxBottom);
                SetProperty(ref _bottom, target);
                RaisePropertyChanged(nameof(MaxTop));
            }
        }

        public int MinBottom => System.Math.Max(_camera.CameraROIRange.BottomRightYMin, _top + 1);
        public int MaxBottom => _camera.CameraROIRange.BottomRightYMax;

        private int _right;
        public override int Right
        {
            get => _right;
            set
            {
                var target = System.Math.Clamp(value, MinRight, MaxRight);
                SetProperty(ref _right, target);
                RaisePropertyChanged(nameof(MaxLeft));
            }
        }

        public int MinRight => System.Math.Max(_camera.CameraROIRange.BottomRightXMin, _left + 1);
        public int MaxRight => _camera.CameraROIRange.BottomRightXMax;

        #endregion
        public System.Tuple<int, int> BinXRange
        {
            get => _camera.BinXRange;
        }

        private int _binX;
        public override int BinX
        {
            get => _binX;
            set
            {
                var target = System.Math.Clamp(value, BinXRange.Item1, BinXRange.Item2);
                SetProperty(ref _binX, target);
            }
        }

        public System.Tuple<int, int> BinYRange
        {
            get => _camera.BinYRange;
        }

        private int _binY;
        public override int BinY
        {
            get => _binY;
            set
            {
                var target = System.Math.Clamp(value, BinYRange.Item1, BinYRange.Item2);
                SetProperty(ref _binY, target);
            }
        }

        private bool _isBinEnabled;
        public bool IsBinEanbled
        { 
            get => _isBinEnabled;
            set => SetProperty(ref _isBinEnabled, value);
        }

        private bool _reverseX;
        public bool ReverseX
        {
            get => _reverseX;
            set => SetProperty(ref _reverseX, value);
        }

        private bool _reverseY;
        public bool ReverseY
        {
            get => _reverseY;
            set => SetProperty(ref _reverseY, value);
        }

        private bool _isCorrectionEnabled;
        public bool IsCorrectionEnabled
        {
            get => _isCorrectionEnabled;
            set => SetProperty(ref _isCorrectionEnabled, value);
        }

        private CorrectionMode _correctionMode;
        public CorrectionMode CorrectionMode
        {
            get => _correctionMode;
            set => SetProperty(ref _correctionMode, value);
        }

        public bool IsColor => DisplayService.Instance.IsColor;

        public ICommand FullFrameCommand { get; private set; }
        public ICommand OKCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand DefaultCommand { get; private set; }

        #endregion

        public CameraSettingsViewModel()
        {
            FullFrameCommand = new DelegateCommand(FullFrameCommandExecute, CanFullFrameCommandExecute);
            OKCommand = new DelegateCommand(OnOkCommandExecute, CanOkCommandExecute);
            CancelCommand = new DelegateCommand(OnCancelCommandExecute, CanCancelCommandExecute);
            DefaultCommand = new DelegateCommand(OnDefaultCommandExecute, CanDefaultCommandExecute);
            _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
        }

        private void FullFrameCommandExecute()
        {
            Top = MinTop;
            Left = MinLeft;
            Bottom = MaxBottom;
            Right = MaxRight;
        }

        private bool CanFullFrameCommandExecute()
        {
            return true;
        }

        private void OnOkCommandExecute()
        {
            ApplyValue(this);
        }

        private bool CanOkCommandExecute()
        {
            return true;
        }

        private bool IsROIChanged(int top, int left, int bottom, int right)
        {
            var roi = _camera.CameraROI;
            if (roi.X != left)
                return true;
            if (roi.Y != top)
                return true;
            if (roi.Width + roi.X != right)
                return true;
            if (roi.Height + roi.Y != bottom)
                return true;
            return false;
        }

        private void ApplyValue(CameraSettingsViewModelBase obj)
        {
            var isROIChanged = IsROIChanged(obj.Top, obj.Left, obj.Bottom, obj.Right);

            if (isROIChanged)
            {
                bool _isliving = CaptureService.Instance.IsLiving;
                if (_isliving)
                {
                    CaptureService.Instance.StopLive();
                }

                var ROIRect = new System.Windows.Int32Rect(obj.Left, obj.Top, obj.Right - obj.Left, obj.Bottom - obj.Top);
                if (ROIRect != _camera.CameraROI)
                {
                    _camera.SetROI(ROIRect);
                    _camera.CameraOrignalROI = _camera.CameraROI;
                }
                
                if (_isliving)
                {
                    CaptureService.Instance.LiveImages();
                    _eventAggregator.GetEvent<ClearROIEvent>().Publish(-1);
                }
            }

            if (IsBinEanbled)
            {
                var binx = System.Math.Clamp(obj.BinX, BinXRange.Item1, BinXRange.Item2);
                if (binx != _camera.BinX)
                {
                    _camera.SetBinX(binx);
                }
                var biny = System.Math.Clamp(obj.BinY, BinXRange.Item1, BinXRange.Item2);
                if (biny != _camera.BinY)
                {
                    _camera.SetBinY(biny);
                }
            }
            else
            {
                _camera.SetBinX(1);
                _camera.SetBinY(1);
            }

            if (IsCorrectionEnabled != _camera.IsCorrectionModeEnabled)
            {
                _camera.SetCorrectionModeEnabled(IsCorrectionEnabled);
            }
            if (CorrectionMode != _camera.CorrectionMode)
            {
                _camera.SetCorrectionMode(CorrectionMode);
            }

            if (ReverseX != _camera.IsReverseXEnabled)
            {
                _camera.SetReverseXEnabled(ReverseX);
            }
            if (ReverseY != _camera.IsReverseYEnabled)
            {
                _camera.SetReverseYEnabled(ReverseY);
            }

            CaptureService.Instance.IsEnableCapturingImageUpdate = obj.IsEnableCapturingImageUpdate;
        }

        private void OnCancelCommandExecute()
        {
            var rect = _camera.CameraOrignalROI;
            Top = rect.Y;
            Left = rect.X;
            Bottom = rect.Y + rect.Height;
            Right = rect.X + rect.Width;
            BinX = _camera.BinX;
            BinY = _camera.BinY;
            IsBinEanbled = true;
            IsCorrectionEnabled = _camera.IsCorrectionModeEnabled;
            CorrectionMode = _camera.CorrectionMode;
            ReverseX = _camera.IsReverseXEnabled;
            ReverseY = _camera.IsReverseYEnabled;
            IsEnableCapturingImageUpdate = CaptureService.Instance.IsEnableCapturingImageUpdate;
        }

        private bool CanCancelCommandExecute()
        {
            return true;
        }

        private void OnDefaultCommandExecute()
        {
            Top = MinTop;
            Left = MinLeft;
            Bottom = MaxBottom;
            Right = MaxRight;
            BinX = _camera.BinXRange.Item1;
            BinY = _camera.BinYRange.Item1;
            IsBinEanbled = true;
            IsCorrectionEnabled = true;
            CorrectionMode = CorrectionMode.DPC;
            ReverseX = false;
            ReverseY = false;
            HardwareTrigger = HardwareTriggerPolarity.TriggerPolarityActiveHigh;
        }

        private bool CanDefaultCommandExecute()
        {
            return true;
        }


        // Below functions should be kept to fit for ThorImage

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(CameraSettingsViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        public void StartUpdate(object param = null)
        {
            RaisePropertyChanged(nameof(IsColor));
            var rect = _camera.CameraOrignalROI;
            Bottom = rect.Y + rect.Height;
            Right = rect.X + rect.Width;
            Top = rect.Y;
            Left = rect.X;
            BinX = _camera.BinX;
            BinY = _camera.BinY;
            IsBinEanbled = true;
            IsCorrectionEnabled = _camera.IsCorrectionModeEnabled;
            CorrectionMode = _camera.CorrectionMode;
            ReverseX = _camera.IsReverseXEnabled;
            ReverseY = _camera.IsReverseYEnabled;
            IsEnableCapturingImageUpdate = CaptureService.Instance.IsEnableCapturingImageUpdate;
        }

        private const string Name = "CameraOptions";
        private void GetConfig(object sender, System.EventArgs e)
        {
            var obj = ConfigService.Instance.GetCorrespondingConfig<CameraSettingsViewModelBase>(Name);
            ApplyValue(obj);
        }

        public void LoadJsonSettings(List<JsonObject> jsonDatas)
        {
            var target = jsonDatas.FirstOrDefault(item => item.Name == Name);
            if (target != null)
            {
                var obj = JsonSerializer.Deserialize<CameraSettingsViewModelBase>(target.Setting.ToString());
                ApplyValue(obj);
                jsonDatas.Remove(target);
            }
        }

        public void SaveJsonSettings(List<JsonObject> jsonDatas)
        {
            StartUpdate();
            jsonDatas.Add(new JsonObject() { Name = Name, Setting = (CameraSettingsViewModelBase)this });
        }
    }
}
