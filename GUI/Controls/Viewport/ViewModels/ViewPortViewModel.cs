using DrawingTool;
using DrawingTool.Factory.Material;
using DrawingTool.Tools;
using Microsoft.Win32;
using PluginCommon.Localization;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using FilterWheelShared.Common;
using FilterWheelShared.Controls.DrawingTools;
using FilterWheelShared.Controls.DrawingTools.Factory;
using FilterWheelShared.Controls.DrawingTools.Factory.Materials;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;
using System.Threading.Tasks;

namespace Viewport.ViewModels
{
    public class ViewportViewModelBase : BindableBase
    {
        public virtual bool IsShowScale { get; set; }
    }

    public class ViewportViewModel : ViewportViewModelBase, IIndependentJson
    {
        private WriteableBitmap _wbForDisplay = null;
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();
        private readonly IEventAggregator _eventAggregator;

        #region Properties
        [JsonIgnore]
        public ObservableCollection<ImageViewModel> Images { get; private set; } = new ObservableCollection<ImageViewModel>();
        [JsonIgnore]
        public ObservableCollection<GraphicViewModelBase> ROIs { get; private set; } = new ObservableCollection<GraphicViewModelBase>();

        public bool IsViewPortNotEmpty => Images.Count > 0;

        public bool _isColor = false;
        public bool IsColor// => DisplayService.Instance.IsColor;
        {
            get => _isColor;//=> ThorlabsCamera.Instance.IsColorCamera;
            private set => SetProperty(ref _isColor, value);
        }

        public bool _isPolar = false;
        public bool IsPolar
        {
            get => _isPolar;
            private set => SetProperty(ref _isPolar, value);
        }

        private bool _isShowScale;
        public override bool IsShowScale
        {
            get { return _isShowScale; }
            set
            {
                if (SetProperty(ref _isShowScale, value))
                    OnShowScaleChanged(_isShowScale);
            }
        }

        [JsonIgnore]
        public bool IsFlipHorizontal
        {
            get => DisplayService.Instance.IsFlipH;
            set
            {
                DisplayService.Instance.IsFlipH = value;
                RaisePropertyChanged(nameof(IsFlipHorizontal));
            }
        }

        [JsonIgnore]
        public bool IsFlipVertical
        {
            get => DisplayService.Instance.IsFlipV;
            set
            {
                DisplayService.Instance.IsFlipV = value;
                RaisePropertyChanged(nameof(IsFlipVertical));
            }
        }

        private readonly ToolScalerPointer _pointTool = new ToolScalerPointer();

        private readonly ToolRectangle _rectTool = new ToolRectangle();

        private bool _isRectTool;
        [JsonIgnore]
        public bool IsRectTool
        {
            get => _isRectTool;
            set => SetProperty(ref _isRectTool, value);
        }

        private readonly ToolEllipse _ovalTool = new ToolEllipse();

        private bool _isOvalTool;
        [JsonIgnore]
        public bool IsOvalTool
        {
            get => _isOvalTool;
            set => SetProperty(ref _isOvalTool, value);
        }

        private readonly ToolProfile _profileTool = new ToolProfile();

        private bool _isProfileTool;
        [JsonIgnore]
        public bool IsProfileTool
        {
            get => _isProfileTool;
            set => SetProperty(ref _isProfileTool, value);
        }

        private readonly ToolRulerEx _rulerTool = new ToolRulerEx();

        private bool _isRulerTool;
        [JsonIgnore]
        public bool IsRulerTool
        {
            get => _isRulerTool;
            set => SetProperty(ref _isRulerTool, value);
        }

        public bool HasValidRoi => ROIs.Any(r => r is ROIViewModelBase roi && roi.IsSelectable);

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
                    CaptureService.Instance.JumpToSlot(CurrentSlotIndex);
                    CaptureService.Instance.IsJogging = false;
                });
                //CaptureService.SetCurrentSlotParasIntoCam(CurrentSlotIndex);
                DisplayService.Instance.UpdateInterfaceAfterSlotSlection(value);
                //_eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Publish(null);
            }
        }

        //private Slot _currentSelectedSlot;
        //public Slot CurrentSelectedSlot
        //{
        //    get=> _currentSelectedSlot;
        //    set
        //    {
        //        SetProperty(ref _currentSelectedSlot, value);
        //    }
        //}

        //public ObservableCollection<Slot> Slots
        //{
        //    get => DisplayService.Instance.Slots;
        //}
        public ObservableCollection<Slot> Slots
        {
            get => DisplayService.Instance.Slots;
            set
            {
                DisplayService.Instance.Slots = value;
                RaisePropertyChanged(nameof(Slots));
            }
        }

        #endregion

        #region DrawingTools
        public CustomFactory Factory { get; private set; } = new CustomFactory();
        public Coordinate Coordinate { get; private set; } = new CustomCoordinate();

        private static CustomScalerViewModel _customScalerViewModel = null;
        public static CustomScalerViewModel CustomScalerViewModelInstance
        {
            get
            {
                if (_customScalerViewModel == null)
                {
                    _customScalerViewModel = new CustomScalerViewModel
                    {
                        FontFamily = Properties.RulerConfigurationSettings.Default.FontFamily,
                        FontSize = Properties.RulerConfigurationSettings.Default.FontSize,
                        FontColor = Properties.RulerConfigurationSettings.Default.FontColor,
                        LineWidth = Properties.RulerConfigurationSettings.Default.LineWidth,
                        LineColor = Properties.RulerConfigurationSettings.Default.LineColor,
                        PanelOpacity = Properties.RulerConfigurationSettings.Default.PanelOpacity,
                        PanelColor = Properties.RulerConfigurationSettings.Default.PanelColor,
                        ScalerPlacement = Properties.RulerConfigurationSettings.Default.ScalerPlacement,
                        IsSelectable = false,
                        IsSelected = false,
                        IsRenderShow = false,
                    };
                }
                return _customScalerViewModel;
            }
        }

        private ToolBase _currentTool;
        public ToolBase CurrentTool
        {
            get => _currentTool;
            private set
            {
                if (_currentTool == value) return;
                if (_currentTool is ToolROIBase toolROI)
                {
                    toolROI.AddROI -= ViewModel_AddROI;
                    toolROI.RemoveROI -= ViewModel_RemoveROI;
                    toolROI.OperatinFinishedCallback -= ViewModel_OperationFinishedCallback;
                }
                if (_currentTool is ToolScalerPointer toolScalerPointer1)
                {
                    toolScalerPointer1.ViewPixelAtPoint -= ToolScalerPointer_ViewPixelAtPoint;
                    toolScalerPointer1.ScalerClicked -= CurrentTool_ScalerClicked;
                    toolScalerPointer1.MouseLeaved -= ToolScalerPointer_MouseLeaved;
                }

                SetProperty(ref _currentTool, value);

                if (_currentTool is ToolROIBase toolROI1)
                {
                    toolROI1.AddROI += ViewModel_AddROI;
                    toolROI1.RemoveROI += ViewModel_RemoveROI;
                    toolROI1.OperatinFinishedCallback += ViewModel_OperationFinishedCallback;
                }
                if (_currentTool is ToolScalerPointer toolScalerPointer2)
                {
                    toolScalerPointer2.ViewPixelAtPoint += ToolScalerPointer_ViewPixelAtPoint;
                    toolScalerPointer2.ScalerClicked += CurrentTool_ScalerClicked;
                    toolScalerPointer2.MouseLeaved += ToolScalerPointer_MouseLeaved;
                }
            }
        }

        private void ToolScalerPointer_MouseLeaved(object sender, RoutedEventArgs e)
        {
            //when mouse leave, stop update RGB value
            //DisplayService.Instance.CusorLocation = null;
        }

        private void ToolScalerPointer_ViewPixelAtPoint(Point obj)
        {
            var physicalLocation = Coordinate.GetPhysicalPointFromPixel(obj);
            var pixelPoint = new IntPoint((int)obj.X, (int)obj.Y);
            var PhysicalPoint = new DoublePoint(physicalLocation.X, physicalLocation.Y);
            //update RGB value
            DisplayService.Instance.CusorLocation = new PointEx(pixelPoint, PhysicalPoint);
        }
        #endregion

        #region Command
        public DelegateCommand SetRegionToWindowCommand { get; private set; }

        public ICommand FitImageToWindowCommand { get; private set; }
        public DelegateCommand SaveAsCommand { get; private set; }
        public DelegateCommand<object> ROICommand { get; private set; }
        public DelegateCommand HistogramCommand { get; private set; }
        #endregion

        private void UpdateCommand()
        {
            SetRegionToWindowCommand = new DelegateCommand(SetRegionToWindowExecute, SetRegionToWindowCanExecute);
            FitImageToWindowCommand = new DelegateCommand(FitImageToWindowExecute, FitImageToWindowCanExecute);
            SaveAsCommand = new DelegateCommand(SaveAsExecute, SaveAsCanExecute);
            ROICommand = new DelegateCommand<object>(ROICommandExecute, ROICommandCanExecute);
            HistogramCommand = new DelegateCommand(() => { }, () => Images.Count > 0);
        }
        private void UpdateEvent()
        {

            _eventAggregator.GetEvent<UpdateProfileDrawingWidthEvent>().Subscribe(UpdateToolProfile);
            //_eventAggregator.GetEvent<UpdateDisplayImageEvent>().Subscribe(UpdateWriteableBitmapCallbackEvent, ThreadOption.BackgroundThread);
            _eventAggregator.GetEvent<UpdateDisplayImageEvent>().Subscribe(UpdateWriteableBitmapCallbackNew);
            _eventAggregator.GetEvent<ResetMainPanelEvent>().Subscribe(OnResetMainPanel, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ClearROIEvent>().Subscribe((index) => ClearROIs(index, true));
            _eventAggregator.GetEvent<ThorCamStatusEvent>().Subscribe(OnThorCamStatusChanged, ThreadOption.UIThread);
            _eventAggregator.GetEvent<StatisticItemSelectedEvent>().Subscribe(OnStatisticItemSelected);
            _eventAggregator.GetEvent<AddROIEvent>().Subscribe(OnAddROI);
            _eventAggregator.GetEvent<UpdateSlotSelectedIndexEvent>().Subscribe(CurrentSelectedIndexChanged);

        }
        public ViewportViewModel()
        {
            CurrentTool = _pointTool;
            _profileTool.SetWidth(1);
            UpdateCommand();
            _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();

            Images.CollectionChanged += (s, e) =>
            {
                RaisePropertyChanged(nameof(IsViewPortNotEmpty));
                OnShowScaleChanged(Images.Count > 0 && _isShowScale);
                ROICommand.RaiseCanExecuteChanged();
                SaveAsCommand.RaiseCanExecuteChanged();
                HistogramCommand.RaiseCanExecuteChanged();
            };

            DisplayService.Instance.UpdateWriteableBitmapCallBack += UpdateWriteableBitmapCallback;
            DisplayService.Instance.PrepareForUpdateDisplayImageCallBack += PrepareForUpdateDisplayImageCallBack;
            DisplayService.Instance.StopResumeCallback += Instance_CameraOpenCloseEvent;
            DisplayService.Instance.UpdateSizeRatio += Instance_UpdateSizeRatio;

            UpdateEvent();
            Coordinate.ScaleUpdatedEvent += Coordinate_ScaleUpdatedEvent; ;
        }

        private void Instance_UpdateSizeRatio(object sender, EventArgs e)
        {
            var instance = DisplayService.Instance;
            var xPhysicalRatio = instance.PhysicalWidthPerPixel / instance.TargetObjective;
            var yPhysicalRatio = instance.PhysicalHeightPerPixel / instance.TargetObjective;

            CustomScalerViewModelInstance.XPhysicalRatio = xPhysicalRatio;
            CustomScalerViewModelInstance.YPhysicalRatio = yPhysicalRatio;

            _rulerTool.XPhysicalRatio = xPhysicalRatio;
            _rulerTool.YPhysicalRatio = yPhysicalRatio;

            var rulers = ROIs.Where(r => r is CustomRulerViewModel).Cast<CustomRulerViewModel>().ToList();
            foreach (var ruler in rulers)
            {
                ruler.XPhysicalRatio = xPhysicalRatio;
                ruler.YPhysicalRatio = yPhysicalRatio;
            }
        }

        private System.Threading.CancellationTokenSource _tokenSource = new System.Threading.CancellationTokenSource();
        private void Instance_CameraOpenCloseEvent(object sender, bool e)
        {
            if (e)
            {
                _tokenSource = new System.Threading.CancellationTokenSource();
            }
            else
            {
                _tokenSource.Cancel();
            }
            //Images.Clear();
        }

        private void OnStatisticItemSelected(System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            var rois = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable).Cast<ROIViewModelBase>();
            switch (args.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (StatisticItem item in args.NewItems)
                    {
                        var tar = rois.FirstOrDefault(r => r.Id == item.Index);
                        if (tar != null)
                        {
                            tar.IsSelected = true;
                            //SetSelectedWithoutPropertyChangeEvent(tar, true);
                        }
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    foreach (var roi in rois)
                    {
                        SetSelectedWithoutPropertyChangeEvent(roi, false);
                    }
                    DisplayService.Instance.ROISelected(-1, false);
                    break;
            }
        }

        private void SetSelectedWithoutPropertyChangeEvent(ROIViewModelBase roi, bool isSelected)
        {
            roi.PropertyChanged -= Graphic_PropertyChanged;
            roi.IsSelected = isSelected;
            roi.PropertyChanged += Graphic_PropertyChanged;
        }

        private void OnResetMainPanel(bool isColor)
        {
            IsColor = isColor;
            Images.Clear();
            ClearROIs(-1, true);
        }

        private void Coordinate_ScaleUpdatedEvent()
        {
            if (Images.Count > 0)
                _eventAggregator.GetEvent<CoordinateZoomChangedEvent>().Publish(Coordinate.MaxScale);
        }

        private ThorCamStatus _previousStatus = ThorCamStatus.None;
        private void OnThorCamStatusChanged(ThorCamStatusEventArgs e)
        {
            if (e.Status == ThorCamStatus.Living || e.Status == ThorCamStatus.Capturing)
            {
                if (_previousStatus != e.Status)
                {
                    if (ThorlabsCamera.Instance.PreviousROI != Int32Rect.Empty &&
                        ThorlabsCamera.Instance.PreviousROI != ThorlabsCamera.Instance.CameraROI)
                    {
                        ThorlabsCamera.Instance.PreviousROI = Int32Rect.Empty;
                        ClearROIs(-1, true);
                        Coordinate.AutoFit();
                    }
                }
            }
            else
            {
                if (e.Status == ThorCamStatus.Loaded)
                {
                    ClearROIs(-1, true);
                    Coordinate.AutoFit();
                }
                if (e.Status == ThorCamStatus.None)
                {
                    int i = 0;
                    i++;
                }
            }
            _previousStatus = e.Status;
        }

        private void SetRegionToWindowExecute()
        {
            var roi = ROIs.FirstOrDefault(r => r is ROIViewModelBase roi && roi.IsSelectable && roi.IsSelected);
            if (roi is RectangleViewModelBase rectVMB)
            {
                var physicalRect = rectVMB.Rect;
                var xScale = physicalRect.Width / Coordinate.PhysicalRect.Width;
                var yScale = physicalRect.Height / Coordinate.PhysicalRect.Height;
                if (xScale > yScale)
                {
                    var height = Coordinate.PhysicalRect.Height * xScale;
                    var y = physicalRect.Y - (height - physicalRect.Height) / 2;
                    physicalRect.Y = y;
                    physicalRect.Height = height;
                }
                else
                {
                    var width = Coordinate.PhysicalRect.Width * yScale;
                    var x = physicalRect.X - (width - physicalRect.Width) / 2;
                    physicalRect.X = x;
                    physicalRect.Width = width;
                }
                Coordinate.DisplayPhysicalArea = physicalRect;
                Coordinate.RaiseReDraw();
            }
        }

        private bool SetRegionToWindowCanExecute()
        {
            return ROIs.Count(r => r is ROIViewModelBase roi && roi.IsSelectable && roi.IsSelected) == 1;
        }

        private void FitImageToWindowExecute()
        {
            Coordinate.AutoFit();
        }

        private bool FitImageToWindowCanExecute()
        {
            return true;
        }

        private void SaveAsExecute()
        {
            var dialog = new SaveFileDialog()
            {
                Title = PluginCommon.Localization.PluginLocalizationService.GetInstance().GetLocalizationString(PluginCommon.Localization.PluginLocalziationKey.SaveAs),
                Filter = "TIF(*.tif)|*.tif|JPEG(*.jpg)|*.jpg",
            };
            if (dialog.ShowDialog() == true)
            {
                BitmapEncoder encoder = null;
                switch (dialog.FilterIndex)
                {
                    case 1:
                        encoder = new TiffBitmapEncoder();
                        break;
                    case 2:
                        encoder = new JpegBitmapEncoder();
                        break;
                    default:
                        break;
                }
                if (encoder == null)
                    return;

                var img = (WriteableBitmap)Images.First().Image;
                encoder.Frames.Add(BitmapFrame.Create(img));
                using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                {
                    encoder.Save(stream);
                }
            }
        }

        private bool SaveAsCanExecute()
        {
            return Images.Count > 0;
        }

        private void ROICommandExecute(object args)
        {
            if (args is string s && !string.IsNullOrEmpty(s))
            {
                switch (s)
                {
                    case "Rect":
                        if (IsRectTool)
                            CurrentTool = _rectTool;
                        else
                            CurrentTool = _pointTool;
                        IsOvalTool = false;
                        IsProfileTool = false;
                        IsRulerTool = false;
                        break;
                    case "Oval":
                        if (IsOvalTool)
                            CurrentTool = _ovalTool;
                        else
                            CurrentTool = _pointTool;
                        IsRectTool = false;
                        IsProfileTool = false;
                        IsRulerTool = false;
                        break;
                    case "Profile":
                        if (IsProfileTool)
                            CurrentTool = _profileTool;
                        else
                            CurrentTool = _pointTool;
                        IsRectTool = false;
                        IsOvalTool = false;
                        IsRulerTool = false;
                        break;
                    case "Ruler":
                        if (IsRulerTool)
                            CurrentTool = _rulerTool;
                        else
                            CurrentTool = _pointTool;
                        IsRectTool = false;
                        IsOvalTool = false;
                        IsProfileTool = false;
                        break;
                    default:
                        break;
                }
            }
        }

        private bool ROICommandCanExecute(object args)
        {
            return Images.Count > 0;
        }

        //private void UpdateImageCallBack(P2dInfo info, System.Threading.CancellationToken token)
        //{
        //    //Create a RGB24 writeable_bitmap for display
        //    bool needGC = false;
        //    bool needClear = true;
        //    if (Images.Count > 0 && Images.First().Rect.Width == info.x_size && Images.First().Rect.Height == info.y_size)
        //        needClear = false;
        //    else
        //        needClear = false;

        //    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        //    {
        //        if (needClear)
        //        {
        //            Images.Clear();
        //            needGC = true;
        //        }
        //        while (Images.Count > 1)
        //        {
        //            needGC = true;
        //            Images.RemoveAt(1);
        //        }

        //        if (token.IsCancellationRequested) return;

        //        if (Images.Count == 0)
        //        {
        //            var wbp = new WriteableBitmap(info.x_size, info.y_size, ImageData.DpiX, ImageData.DpiY, PixelFormats.Rgb24, null);
        //            var rect = new System.Windows.Rect(0, 0, info.x_size, info.y_size);
        //            var image = new ImageViewModel() { Image = wbp, Rect = rect };
        //            Images.Add(image);
        //        }
        //    }, System.Windows.Threading.DispatcherPriority.Normal, token);

        //    if (needGC)
        //    {
        //        GC.Collect();
        //    }
        //}

        private void PrepareForUpdateDisplayImageCallBack(object sender, ImageData img)
        {
            // this will not be happen
            //if (img == null)
            //{
            //    _wbForDisplay = null;
            //    Images.Clear();
            //    UpdateCoordinate(0, 0, true);
            //    return;
            //}

            int width = img.DataInfo.x_size;
            int height = img.DataInfo.y_size;

            //UpdateImageCallBack(img.DataInfo, _tokenSource.Token);
            bool needGC = false;
            bool needClear = true;
            if (Images.Count > 0 && Images.First().Rect.Width == width && Images.First().Rect.Height == height)
                needClear = false;

            IntPtr pBackBuffer = IntPtr.Zero;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {

                if (needClear)
                {
                    Images.Clear();
                    needGC = true;
                }
                while (Images.Count > 1)
                {
                    needGC = true;
                    Images.RemoveAt(1);
                }

                if (_tokenSource.Token.IsCancellationRequested)
                {
                    _wbForDisplay = null;
                    return;
                }
                if (Images.Count == 0)
                {
                    var wbp = new WriteableBitmap(width, height, ImageData.DpiX, ImageData.DpiY, PixelFormats.Rgb24, null);
                    var rect = new System.Windows.Rect(0, 0, width, height);
                    var image = new ImageViewModel() { Image = wbp, Rect = rect };
                    Images.Add(image);
                }

                _wbForDisplay = (WriteableBitmap)Images.First().Image;
                //_wbForDisplay.Lock();
                //pBackBuffer = _wbForDisplay.BackBuffer;
            }, System.Windows.Threading.DispatcherPriority.Render, _tokenSource.Token);

            if (needGC)
                GC.Collect();
        }

        private void UpdateWriteableBitmapCallback(object sender, ImageData img)
        {
            // this function only goes to null if statement
            if (img == null)
            {
                Images.Clear();
                UpdateCoordinate(0, 0);
                return;
            }
        }

        private void UpdateWriteableBitmapCallbackNew(Tuple<ImageData, bool> arg)
        {
            if (arg.Item2)
            {
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //RaisePropertyChanged(nameof(Slots));
                //RaisePropertyChanged(nameof(CurrentSlotIndex));
                //});
            }
            if (arg != null && _wbForDisplay != null)
            {
                var img = arg.Item1;
                img.UpdateWriteableBitmap(_wbForDisplay, _tokenSource.Token);

                //_wbForDisplay.AddDirtyRect(new Int32Rect(0, 0, img.DataInfo.x_size, img.DataInfo.y_size));
                //_wbForDisplay.Unlock();
                UpdateCoordinate(img.DataInfo.x_size, img.DataInfo.y_size);
            }
            
        }


        private void UpdateToolProfile(int width)
        {
            if (width < 0)
            {
                if (CurrentTool is ToolProfile)
                {
                    CurrentTool = _pointTool;
                }
                var profileRois = ROIs.Where(r => r is CustomProfileViewModel).ToList();
                foreach (var profile in profileRois)
                {
                    ROIs.Remove(profile);
                }
                return;
            }

            _profileTool.SetWidth(width);

            if (CurrentTool is ToolProfile) return;

            var profiles = ROIs.Where(r => r is CustomProfileViewModel).ToList();
            foreach (CustomProfileViewModel profile in profiles)
            {
                profile.Width = width;
            }
        }

        private bool _lastIsLoading = false;
        private void UpdateCoordinate(int width, int height)
        {         
            var pixelRect = new Rect(0, 0, width, height);
            var cameraROI = ThorlabsCamera.Instance.CameraROI;
            var physicalRect = new Rect(cameraROI.X, cameraROI.Y, cameraROI.Width, cameraROI.Height);

            var coordinateSize = Coordinate.PixelSize;
            if (Coordinate.PixelSize != pixelRect.Size || Coordinate.PhysicalRect != physicalRect)
            {
                Coordinate.PixelSize = pixelRect.Size;
                Coordinate.PhysicalRect = physicalRect;
                Application.Current?.Dispatcher.Invoke(() => Coordinate.AutoFit(), System.Windows.Threading.DispatcherPriority.Render);
            }
        }


        // Below functions should be kept to fit for ThorImage

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(ViewportViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        private int GetNextROIId()
        {
            var idList = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable).Cast<ROIViewModelBase>().Select(r => r.Id).ToList();
            idList.Sort();
            for (int i = 0; i < idList.Count; i++)
            {
                if (idList[i] != i)
                    return i;
            }
            return idList.Count;
        }

        private void UnSelectAllROIs()
        {
            var selectedROIS = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable).Cast<ROIViewModelBase>().ToList();
            foreach (var roi in selectedROIS)
            {
                roi.IsSelected = false;
            }
        }

        private void ViewModel_AddROI(object sender, ROIEventArgs e)
        {
            if (e.Graphic is ROIViewModelBase roi)
            {
                if (roi is RectangleViewModel || roi is EllipseViewModel)
                {
                    var brush = new SolidColorBrush(GenerateColor.Current);
                    roi.Stroke = brush;
                    roi.Id = GetNextROIId();
                    roi.IsRenderShow = true;
                    roi.GraphicsChanged += Roi_GraphicsChanged;
                    roi.PropertyChanged += Graphic_PropertyChanged;
                    ROIs.Add(roi);

                    var pixelRect = Coordinate.GetPixelRectFromPhysical(roi.ROIRect);
                    P2dRect ROIPixel = new P2dRect()
                    {
                        x = (int)pixelRect.X,
                        y = (int)pixelRect.Y,
                        width = (int)pixelRect.Width,
                        height = (int)pixelRect.Height,
                    };

                    P2dRoiType type = P2dRoiType.P2D_Ellipse;
                    if (roi is RectangleViewModel)
                        type = P2dRoiType.P2D_Rectangle;
                    DisplayService.Instance.ROIAdd(roi.Id, ROIPixel, roi.ROIRect, brush, type);
                }
                else if (roi is CustomScalerViewModel)
                {
                    ROIs.Add(roi);
                }
                RaisePropertyChanged(nameof(HasValidRoi));
            }
            else if (e.Graphic is CustomProfileViewModel)
            {
                ROIs.Add(e.Graphic);
                DisplayService.Instance.IsProfileShown = true;
            }
            else if (e.Graphic is GraphicViewModelBase graphic)
            {
                if (graphic is CustomRulerViewModel ruler)
                {
                    ruler.Stroke = new SolidColorBrush(GenerateColor.Current);
                    ruler.XPhysicalRatio = _rulerTool.XPhysicalRatio;
                    ruler.YPhysicalRatio = _rulerTool.YPhysicalRatio;
                    ROIs.Add(graphic);
                }
            }

        }

        private void Roi_GraphicsChanged(ROIViewModelBase e)
        {
            if (e == null) return;
            var pixelRect = Coordinate.GetPixelRectFromPhysical(e.ROIRect);
            P2dRect ROIPixel = new P2dRect()
            {
                x = (int)pixelRect.X,
                y = (int)pixelRect.Y,
                width = (int)pixelRect.Width,
                height = (int)pixelRect.Height,
            };

            var selectedROIs = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable && roi.IsSelected).Select(r => r as ROIViewModelBase).ToList();
            if (selectedROIs.Count == 1)
            {
                ROISelectedStatusChanged(selectedROIs[0]);
            }
            DisplayService.Instance.ROIModify(e.Id, ROIPixel, e.ROIRect);
        }

        private void Graphic_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ROIViewModelBase.IsSelected))
            {
                SetRegionToWindowCommand.RaiseCanExecuteChanged();

                var selectedROIs = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable && roi.IsSelected).Select(r => r as ROIViewModelBase).ToList();
                if (selectedROIs.Count == 1)
                    ROISelectedStatusChanged(selectedROIs[0]);
                else
                    ROISelectedStatusChanged(null);

                if (selectedROIs.Count == 0)
                {
                    DisplayService.Instance.ROISelected(-1, false);
                    return;
                }

                if (sender is ROIViewModelBase roi)
                {
                    DisplayService.Instance.ROISelected(roi.Id, roi.IsSelected);
                }
            }
        }

        private void OnAddROI(ROIViewModelBase roi)
        {
            if (roi == null) return;

            roi.GraphicsChanged -= Roi_GraphicsChanged;
            roi.PropertyChanged -= Graphic_PropertyChanged;
            roi.GraphicsChanged += Roi_GraphicsChanged;
            roi.PropertyChanged += Graphic_PropertyChanged;

            if (roi is RectangleViewModel || roi is EllipseViewModel)
            {
                ROIs.Add(roi);
                var pixelRect = Coordinate.GetPixelRectFromPhysical(roi.ROIRect);
                P2dRect ROIPixel = new P2dRect()
                {
                    x = (int)pixelRect.X,
                    y = (int)pixelRect.Y,
                    width = (int)pixelRect.Width,
                    height = (int)pixelRect.Height,
                };

                P2dRoiType type = P2dRoiType.P2D_Ellipse;
                if (roi is RectangleViewModel)
                    type = P2dRoiType.P2D_Rectangle;
                DisplayService.Instance.ROIAdd(roi.Id, ROIPixel, roi.ROIRect, (SolidColorBrush)roi.Stroke, type);
            }

            RaisePropertyChanged(nameof(HasValidRoi));
        }

        private void ClearROIs(int mustRemoveIndex, bool deleteAll = false)
        {
            var removes = ROIs.Where(r => r is ROIViewModelBase roi && roi.IsSelectable).Cast<ROIViewModelBase>().ToList();
            var rectCameraROI = new Rect(ThorlabsCamera.Instance.CameraROI.X, ThorlabsCamera.Instance.CameraROI.Y, ThorlabsCamera.Instance.CameraROI.Width, ThorlabsCamera.Instance.CameraROI.Height);
            var idList = new List<int>();
            foreach (var r in removes)
            {
                if (deleteAll || r.Id == mustRemoveIndex || !rectCameraROI.Contains(r.ROIRect))
                {
                    RemoveROI(r);
                    idList.Add(r.Id);
                }
            }
            DisplayService.Instance.ROIDelete(idList);
            PostRemoveROI();

            var removeRulers = ROIs.Where(r => r is RulerViewModel).Cast<RulerViewModel>().ToList();
            foreach (var ruler in removeRulers)
            {
                ruler.PropertyChanged -= Graphic_PropertyChanged;
                ROIs.Remove(ruler);
            }
        }

        private void PostRemoveROI()
        {
            SetRegionToWindowCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(HasValidRoi));
            if (!HasValidRoi)
                CaptureService.Instance.OnROICleared();

        }

        private void RemoveROI(ROIViewModelBase roi)
        {
            var id = roi.Id;
            var idList = new List<int>()
            {
                id,
            };
            roi.GraphicsChanged -= Roi_GraphicsChanged;
            roi.PropertyChanged -= Graphic_PropertyChanged;
            ROIs.Remove(roi);
        }

        private void ViewModel_RemoveROI(object sender, ROIEventArgs e)
        {
            if (e.Graphic is ROIViewModelBase roi)
            {
                if (roi is RectangleViewModel || roi is EllipseViewModel)
                {
                    var idList = new List<int>() { roi.Id };
                    RemoveROI(roi);
                    DisplayService.Instance.ROIDelete(idList);
                    ROISelectedStatusChanged(null);
                    PostRemoveROI();
                }
            }
            else if (e.Graphic is CustomProfileViewModel graphicProfile)
            {
                var invalidPoint = new IntPoint(-1, -1);
                DisplayService.Instance.ProfileStartPoint = invalidPoint;
                DisplayService.Instance.ProfileEndPoint = invalidPoint;
                ROIs.Remove(graphicProfile);
            }
            else if (e.Graphic is GraphicViewModelBase graphic)
            {
                ROIs.Remove(graphic);
            }
        }

        private void ViewModel_OperationFinishedCallback(object sender, ROIActionFinishEventArgs e)
        {
            if (e.Action == GraphicActions.add && e.Graphic is CustomProfileViewModel)
            {
                _eventAggregator.GetEvent<ProfilePopupEvent>().Publish();
                return;
            }

            if (e.Action == GraphicActions.add)
            {
                if (e.Graphic is ROIViewModelBase roi)
                {
                    var pixelRect = Coordinate.GetPixelRectFromPhysical(roi.ROIRect);
                    P2dRect ROIPixel = new P2dRect()
                    {
                        x = (int)pixelRect.X,
                        y = (int)pixelRect.Y,
                        width = (int)pixelRect.Width,
                        height = (int)pixelRect.Height,
                    };

                    DisplayService.Instance.ROIModify(roi.Id, ROIPixel, roi.ROIRect);
                    if (!roi.IsSelected)
                    {
                        UnSelectAllROIs();
                        roi.IsSelected = true;
                    }

                    CurrentTool = _pointTool;
                    IsRectTool = false;
                    IsOvalTool = false;
                }
            }
            //CurrentTool = new ToolScalerPointer();
        }

        private void CurrentTool_ScalerClicked(object sender, DrawingCanvas e)
        {
            _eventAggregator.GetEvent<PopupWindowEvent>().Publish(PopupWindowKey.RulerConfigWindowKey);
        }

        private void CurrentSelectedIndexChanged(int slotIndex)
        {
            RaisePropertyChanged(nameof(CurrentSlotIndex));
        }

        private void OnShowScaleChanged(bool showScale)
        {
            var scalerViewModel = CustomScalerViewModelInstance;
            if (showScale)
            {
                if (!ROIs.Contains(scalerViewModel))
                {
                    ROIs.Add(scalerViewModel);
                }
            }
            else
            {
                if (ROIs.Contains(scalerViewModel))
                {
                    ROIs.Remove(scalerViewModel);
                }
            }
        }

        private const string Name = "ViewPort";
        private void GetConfig(object sender, EventArgs e)
        {
            var obj = ConfigService.Instance.GetCorrespondingConfig<ViewportViewModelBase>(Name);
            this.IsShowScale = obj.IsShowScale;
        }
        public void LoadJsonSettings(List<JsonObject> jsonDatas)
        {
            var target = jsonDatas.FirstOrDefault(item => item.Name == Name);
            if (target != null)
            {
                var obj = JsonSerializer.Deserialize<ViewportViewModelBase>(target.Setting.ToString());
                this.IsShowScale = obj.IsShowScale;
                jsonDatas.Remove(target);
            }
        }

        public void SaveJsonSettings(List<JsonObject> jsonDatas)
        {
            jsonDatas.Add(new JsonObject() { Name = Name, Setting = (ViewportViewModelBase)this });
        }

        private void ROISelectedStatusChanged(ROIViewModelBase roi)
        {
            if (roi is RectangleViewModel || roi is EllipseViewModel)
                CaptureService.Instance.OnROISelectedStatusChanged(roi);
            else
                CaptureService.Instance.OnROISelectedStatusChanged(null);
        }
    }
}
