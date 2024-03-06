using Prism.Events;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using Viewport.ViewModels;
using FilterWheelShared.ImageProcess;
using FilterWheelShared.Event;
using System.Data;

namespace Viewport
{
    public static class RadWindowExtension
    {
        public static void CenterInScreen(this RadWindow window)
        {
            double width = window.ActualWidth;
            double height = window.ActualHeight;

            // Set Left and Top manually and calculate center of screen.
            window.Left = (SystemParameters.WorkArea.Width - width) / 2
                + SystemParameters.WorkArea.Left;
            window.Top = (SystemParameters.WorkArea.Height - height) / 2
                + SystemParameters.WorkArea.Top;
        }
    }
    public class PopupLocation
    {
        public string PopupName { get; }

        public double X { get; set; }

        public double Y { get; set; }

        public PopupLocation(string popupName, double x, double y)
        {
            PopupName = popupName;
            X = x;
            Y = y;
        }
    }

    public partial class ViewportUC : UserControl
    {
        private readonly IEventAggregator _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
        public ViewportUC()
        {
            Init(true);

            InitializeComponent();

            Canvas.AvailableTools.Add(typeof(FilterWheelShared.Controls.DrawingTools.ToolScalerPointer));
            Canvas.AvailableTools.Add(typeof(FilterWheelShared.Controls.DrawingTools.ToolProfile));
            Canvas.AvailableTools.Add(typeof(FilterWheelShared.Controls.DrawingTools.ToolRulerEx));
            this.DataContext = new ViewportViewModel();

            CaptureService.Instance.ROIClearedEvent += Instance_ROICleared;

            _eventAggregator.GetEvent<FilterWheelShared.Event.ResetMainPanelEvent>().Subscribe(ClosePopups, ThreadOption.UIThread);
            _eventAggregator.GetEvent<FilterWheelShared.Event.ProfilePopupEvent>().Subscribe(OpenProfileWindow, ThreadOption.UIThread);
            _eventAggregator.GetEvent<FilterWheelShared.Event.HistogramModeChangedEvent>().Subscribe(CloseHistograms, ThreadOption.UIThread);
            _eventAggregator.GetEvent<FilterWheelShared.Event.ThorCamStatusEvent>().Subscribe((arg) => 
            {
                if (arg.Status == FilterWheelShared.Event.ThorCamStatus.Error 
                        || arg.Status == FilterWheelShared.Event.ThorCamStatus.Capturing 
                        || arg.Status == FilterWheelShared.Event.ThorCamStatus.Snapshoting
                        || arg.Status == FilterWheelShared.Event.ThorCamStatus.Jogging)
                    ThumbnailsList.IsEnabled = false;
                else
                    ThumbnailsList.IsEnabled = true;
            }, ThreadOption.UIThread);
            _eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Subscribe(ChangeChannelType, ThreadOption.BackgroundThread);
            LoadLocationDoc();
        }

        private List<PopupLocation> _popupLocations = new List<PopupLocation>();

        private void LoadLocationDoc()
        {
            var settingFolder = ThorlabsProduct.ApplitaionSettingDir;
            if (!System.IO.Directory.Exists(settingFolder))
            {
                System.IO.Directory.CreateDirectory(settingFolder);
            }
            if (!System.IO.File.Exists(ThorlabsProduct.ThorImageCamPopupsLocationPath))
                return;
            try
            {
                string json = System.IO.File.ReadAllText(ThorlabsProduct.ThorImageCamPopupsLocationPath);
                _popupLocations = JsonSerializer.Deserialize<List<PopupLocation>>(json);
            }
            catch (Exception e)
            {
                ;
            }
        }

        private void GenerateElement(RadWindow window)
        {
            if (window == null) return;
            var item = _popupLocations.FirstOrDefault(i => i.PopupName == window.Name);
            if (item == null)
            {
                item = new PopupLocation(window.Name, window.Left, window.Top);
                _popupLocations.Add(item);
            }
            else
            {
                item.X = window.Left;
                item.Y = window.Top;
            }
        }

        public void SaveLocationDoc()
        {
            GenerateElement(_statisticWindow);
            GenerateElement(_profileView);
            GenerateElement(_colorImgView);

            GenerateElement(_histogramView);

            try
            {
                string json = JsonSerializer.Serialize(_popupLocations);
                System.IO.File.WriteAllText(ThorlabsProduct.ThorImageCamPopupsLocationPath, json);
            }
            catch (Exception e)
            {
                ;
            }

        }

        private void CloseHistograms()
        {
            if (_histogramView != null && _histogramView.IsOpen)
                _histogramView.Close();
        }

        private void ClosePopups(bool _)
        {
            CloseHistograms();

            //DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.Mono, false);
            //DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.R, false);
            //DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.G, false);
            //DisplayService.Instance.UpdateMinMax(0, 255, ChannelType.B, false);

            if (_statisticWindow != null && _statisticWindow.IsOpen)
                _statisticWindow.Close();

            if (_profileView != null && _profileView.IsOpen)
                _profileView.Close();

            if (_colorImgView != null && _colorImgView.IsOpen)
                _colorImgView.Close();
        }

        private void Instance_ROICleared(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (_statisticWindow != null && _statisticWindow.IsOpen)
                    _statisticWindow.Close();
            });
        }

        public static void Init(bool isStandAlone)
        {
            if (!isStandAlone)
            {
                CultureInfo.CurrentCulture =
                CultureInfo.CurrentUICulture =
                CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }

            PluginCommon.PluginTheme.LoadPluginTheme(isStandAlone);

            FilterWheelShared.Localization.LocalizationManager.GetInstance().AddListener(PluginCommon.Localization.PluginLocalizationService.GetInstance());
        }

        private HistogramView _histogramView = null;

        private void OpenHistogramWindow(ChannelType type)
        {
            //360 is histogram view height
            //520 is histogram view width
            var offset = 50;
            var viewWidth = 520;
            var viewHeight = 360;

            if (_histogramView == null)
            {
                _histogramView = new HistogramView(type) { Owner = Application.Current.MainWindow };
                var location = _popupLocations.FirstOrDefault(p => p.PopupName == _histogramView.Name);
                var left = Application.Current.MainWindow.Left;
                var top= Application.Current.MainWindow.Top;
                var width = Application.Current.MainWindow.Width;
                var height = Application.Current.MainWindow.Height;
                _histogramView.Left = left+ width - viewWidth - offset;
                _histogramView.Top = top+ height- viewHeight- offset;
            }
            if (!_histogramView.IsOpen)
                _histogramView.Show();
            _histogramView.IsTopmost = true;
            _histogramView.IsTopmost = false;
        }

        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            if (_type == ChannelType.COMBINE)
            {
                OpenHistogramWindow(ChannelType.COMBINE);
            }
            else
            {
                OpenHistogramWindow(ChannelType.Mono);
            }
        }

        private ChannelType _type;
        private void ChangeChannelType(P2dChannels channel)
        {
            if (channel == P2dChannels.P2D_CHANNELS_1 && _type != ChannelType.Mono)
            {
                _type = ChannelType.Mono;
            }

            else if (channel == P2dChannels.P2D_CHANNELS_3 && _type != ChannelType.COMBINE)
            {
                _type = ChannelType.COMBINE;
            }

        }

        private void EventHandler(object sender, WindowClosedEventArgs e)
        {
            BT_Profile.IsChecked = false;
        }

        private ProfileView _profileView = null;
        private void OpenProfileWindow()
        {
            if (_profileView == null)
            {
                _profileView = new ProfileView() { Owner = Application.Current.MainWindow };
                var location = _popupLocations.FirstOrDefault(p => p.PopupName == _profileView.Name);
                if (location != null)
                {
                    _profileView.WindowStartupLocation = WindowStartupLocation.Manual;
                    _profileView.Top = location.Y;
                    _profileView.Left = location.X;
                }
                _profileView.Closed += EventHandler;
            }
            if (!_profileView.IsOpen)
            {
                _profileView.Show();
            }
            _profileView.IsTopmost = true;
            _profileView.IsTopmost = false;
            _profileView.WindowStartupLocation = WindowStartupLocation.Manual;
        }

        //private void BT_Profile_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    _profileView?.Close();
        //}

        private void BT_ROI_Click(object sender, RoutedEventArgs e)
        {
            BT_Profile.IsChecked = false;
        }

        private ColorImageView _colorImgView = null;
        private void ColorImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_colorImgView == null)
            {
                _colorImgView = new ColorImageView() { Owner = Application.Current.MainWindow };
                var location = _popupLocations.FirstOrDefault(p => p.PopupName == _colorImgView.Name);
                if (location != null)
                {
                    _colorImgView.WindowStartupLocation = WindowStartupLocation.Manual;
                    _colorImgView.Top = location.Y;
                    _colorImgView.Left = location.X;
                }
            }
            if (!_colorImgView.IsOpen)
            {
                //_colorImgView.UpdateDataContext();
                _colorImgView.Show();
            }
            _colorImgView.IsTopmost = true;
            _colorImgView.IsTopmost = false;
            _colorImgView.WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void RadContextMenu_Opening(object sender, Telerik.Windows.RadRoutedEventArgs e)
        {
            ;
        }

        private StatisticWindow _statisticWindow = null;
        private void StatisticButton_Click(object sender, RoutedEventArgs e)
        {
            if (_statisticWindow == null)
            {
                _statisticWindow = new StatisticWindow() { Owner = Application.Current.MainWindow };
                var location = _popupLocations.FirstOrDefault(p => p.PopupName == _statisticWindow.Name);
                if (location != null)
                {
                    _statisticWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _statisticWindow.Top = location.Y;
                    _statisticWindow.Left = location.X;
                }
            }
            if (!_statisticWindow.IsOpen)
            {
                _statisticWindow.Reset();
                _statisticWindow.Show();
            }
            _statisticWindow.IsTopmost = true;
            _statisticWindow.IsTopmost = false;
            _statisticWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void RadContextMenu_PreviewMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ResetWindowPosButton_Click(object sender, RoutedEventArgs e)
        {
            _popupLocations.Clear();
            if (_statisticWindow != null && _statisticWindow.IsOpen)
            {
                _statisticWindow.CenterInScreen();
            }
            if (_colorImgView != null && _colorImgView.IsOpen)
            {
                _colorImgView.CenterInScreen();
            }
            if (_profileView != null && _profileView.IsOpen)
            {
                _profileView.CenterInScreen();
            }

            var offset = 50;
            var viewWidth = 520;
            var viewHeight = 360;
            var centerTop = (SystemParameters.WorkArea.Height - viewHeight) / 2;
            var centerLeft = SystemParameters.WorkArea.Width - viewWidth - offset;

            if (_histogramView != null && _histogramView.IsOpen)
            {
                _histogramView.Left = centerLeft;
                _histogramView.Top = centerTop + offset;
            }
        }

        private void RadSlider_DragStarted(object sender, RadDragStartedEventArgs e)
        {
            ImageLoadService.Instance.StartManulMove();
        }

        private void RadSlider_DragCompleted(object sender, RadDragCompletedEventArgs e)
        {
            ImageLoadService.Instance.CompleteManulMove();
        }


        private void CombineImageButton_Click(object sender, RoutedEventArgs e)
        {
            CombineExecute();
        }

        private void CombineExecute()
        {
            var combineWindowViewModel = new CombineWindowViewModel();
            var combineWindow = new Views.CombineWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = combineWindowViewModel,                
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            combineWindow.Loaded += (s, e) => combineWindowViewModel.UpdateSource();
            if (combineWindow.ShowDialog() == true)
            {
                var selectedImages = combineWindowViewModel.Images.Where(item => item.IsSelected);

                string combineFilePath = CaptureService.Instance.SaveFilePath;

                ImageData tempResult16U3CImage = null;
                ImageData tempScale8U1CImage = null;
                ImageData tempScale8U3CImage = null;
                ImageData tempConvert16U3CImage = null;

                var combineCount = selectedImages.Count();
                double combine_m = 1.0 / combineCount;
                double[] m_values = new double[3] { combine_m, combine_m, combine_m };

                sbyte status = 0;
                try
                {
                    foreach (var image in selectedImages)
                    {
                        var imageData = image.Data;
                        var imageInfo = imageData.DataInfo;

                        var color = DisplayService.Instance.Slots[(int)image.SlotIndex].SlotParameters.SlotColor;
                        var colorTable = DisplayService.Instance.GetCurrentColorLUT(color);
                        if (colorTable == null)
                            throw new Exception($"Get color table with color{color} failed.");

                        tempResult16U3CImage ??= new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_16U, 16, P2dChannels.P2D_CHANNELS_3);
                        tempConvert16U3CImage ??= new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_16U, 16, P2dChannels.P2D_CHANNELS_3);

                        if (imageInfo.pix_type == P2dDataFormat.P2D_16U)
                        {
                            if (imageInfo.channels == P2dChannels.P2D_CHANNELS_1)
                            {
                                tempScale8U1CImage ??= new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1);

                                if (tempScale8U3CImage == null)
                                    tempScale8U3CImage = new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3);
                                else
                                    tempScale8U3CImage.Reset();

                                imageData.ImageScale16uTo8u(tempScale8U1CImage, 0, 255);
                                ImageData.ProcessColorWithLUTNew(tempScale8U1CImage, tempScale8U3CImage, color, colorTable);
                                status = P2DWrapper.Convert(tempScale8U3CImage.Hdl, tempConvert16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Convert from 8U3C to 16U3C failed.");
                                status = P2DWrapper.AddToImage(tempConvert16U3CImage.Hdl, tempResult16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Add image to 16U3C failed.");
                            }
                            else
                            {
                                throw new Exception("16bits 3 channel image is not support now.");
                            }
                        }
                        else if (imageInfo.pix_type == P2dDataFormat.P2D_8U)
                        {
                            if (imageInfo.channels == P2dChannels.P2D_CHANNELS_1)
                            {
                                if (tempScale8U3CImage == null)
                                    tempScale8U3CImage = new ImageData(imageInfo.x_size, imageInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3);
                                else
                                    tempScale8U3CImage.Reset();

                                ImageData.ProcessColorWithLUTNew(imageData, tempScale8U3CImage, color, colorTable);
                                status = P2DWrapper.Convert(tempScale8U3CImage.Hdl, tempConvert16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Convert from 8U3C to 16U3C failed.");
                                status = P2DWrapper.AddToImage(tempConvert16U3CImage.Hdl, tempResult16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Add image to 16U3C failed.");
                            }
                            else
                            {
                                status = P2DWrapper.Convert(imageData.Hdl, tempConvert16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Convert from 8U3C to 16U3C failed.");
                                status = P2DWrapper.AddToImage(tempConvert16U3CImage.Hdl, tempResult16U3CImage.Hdl);
                                if (status != 0)
                                    throw new Exception("Add image to 16U3C failed.");

                            }
                        }
                        else
                        {
                            throw new Exception("Source image pixel type is not support now.");
                        }
                    }

                    status = P2DWrapper.Multiply(tempResult16U3CImage.Hdl, m_values);
                    if (status != 0)
                        throw new Exception("Myltiply image failed.");

                    var tempInfo = tempConvert16U3CImage.DataInfo;
                    var resultImage = new ImageData(tempInfo.x_size, tempInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3);
                    status = P2DWrapper.Convert(tempResult16U3CImage.Hdl, resultImage.Hdl);
                    if (status != 0)
                        throw new Exception("Convert from 16U3C to 8U3C failed.");

                    var camString = $"{ThorlabsCamera.Instance.CurrentCamera.ModelName}({ThorlabsCamera.Instance.CurrentCamera.SerialNumber})";
                    var folderPath = System.IO.Path.Combine(CaptureService.Instance.SaveFilePath, camString);
                    if (!System.IO.Directory.Exists(folderPath))
                    {
                        var iinfo = System.IO.Directory.CreateDirectory(folderPath);
                        if (!iinfo.Exists)
                            throw new Exception($"Create directory : {folderPath} failed.");
                    }

                    var filePath = string.Empty;

                    int snapFileIndex = 1;
                    var isExist = true;
                    while (isExist)
                    {
                        var fileName = $"{CaptureService.Instance.PrefixName}_combine_{snapFileIndex.ToString().PadLeft(4, '0')}.tif";
                        filePath = System.IO.Path.Combine(folderPath, fileName);
                        isExist = System.IO.File.Exists(filePath);
                        snapFileIndex++;
                    }

                    if (string.IsNullOrEmpty(filePath))
                        throw new Exception($"Saved file path is null or empty.");

                    ThorlabsCamera.SaveImage(filePath, resultImage.Hdl);
                    var message = $"The image saved to {filePath}";
                    _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Saved, new Exception(message)));
                    ThorlabsCamera.Instance.OnLoadImage(resultImage.Hdl);
                }
                catch (Exception e)
                {
                    //show warning messagebox
                }
                finally
                {
                    tempResult16U3CImage?.Dispose();
                    tempScale8U1CImage?.Dispose();
                    tempScale8U3CImage?.Dispose();
                    tempConvert16U3CImage?.Dispose();
                }
            }
        }
    }
}
