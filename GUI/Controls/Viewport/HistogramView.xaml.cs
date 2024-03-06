using Microsoft.Win32;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Telerik.Charting;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.ChartView;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;
using Viewport.ViewModels;

namespace Viewport
{
    /// <summary>
    /// Interaction logic for HistogramView.xaml
    /// </summary>
    public partial class HistogramView
    {
        private IEventAggregator eventAggregator = DisplayService.Instance.EventAggregator;
        private ChannelType _type;

        public HistogramView(ChannelType type)
        {
            Init(true);

            InitializeComponent();

            NumberFormatInfo nfi = (NumberFormatInfo)System.Threading.Thread.CurrentThread
                       .CurrentCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "";
            Num_min.NumberFormatInfo = nfi;
            Num_max.NumberFormatInfo = nfi;

            this.Name = $"Histogram_{type}";
            this.DataContext = new ViewModels.HistogramViewModel(type);
            SetToLinearAxis();
            _type = type;

            Loaded += HistogramView_Loaded;
        }

        private void SetToLinearAxis()
        {
            var lineAxis = new LinearAxis
            {
                ShowLabels = true,
                Visibility = Visibility.Visible,
                Minimum = 0,
                SmartLabelsMode = AxisSmartLabelsMode.SmartStep,
                RangeExtendDirection = NumericalAxisRangeExtendDirection.Positive,
                LabelFormat = "e2"
            };
            BarChartSeries.VerticalAxis = lineAxis;
            BarChartSeriesR.VerticalAxis = lineAxis;
            BarChartSeriesG.VerticalAxis = lineAxis;
            BarChartSeriesB.VerticalAxis = lineAxis;
        }

        private void SetToLogarithmicAxis()
        {
            var logAxis = new LogarithmicAxis
            {
                ShowLabels = true,
                Visibility = Visibility.Visible,
                Minimum = 0,
                LogarithmBase = 10,
                SmartLabelsMode = AxisSmartLabelsMode.SmartStep,
                RangeExtendDirection = NumericalAxisRangeExtendDirection.Positive,
                LabelFormat = "e2"
            };
            BarChartSeries.VerticalAxis = logAxis;
            BarChartSeriesR.VerticalAxis = logAxis;
            BarChartSeriesG.VerticalAxis = logAxis;
            BarChartSeriesB.VerticalAxis = logAxis;
        }

        private void UpdateHistogram(P2dChannels channel)
        {
            ChangeChannelType(channel);

            switch (channel)
            {
                case P2dChannels.P2D_CHANNELS_1:
                    if (DisplayService.Instance.HistogramMono?.Count > 0)
                        this.Dispatcher.InvokeAsync(new Action(() =>
                        {
                            BarChartSeriesR.ItemsSource = null;
                            BarChartSeriesG.ItemsSource = null;
                            BarChartSeriesB.ItemsSource = null;
                            BarChartSeries.ItemsSource = DisplayService.Instance.HistogramMono;
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    break;

                case P2dChannels.P2D_CHANNELS_3:


                    this.Dispatcher.InvokeAsync(new Action(() =>
                    {
                        BarChartSeries.ItemsSource = null;
                        if (DisplayService.Instance.IsCheckedR && DisplayService.Instance.HistogramR?.Count > 0)
                            BarChartSeriesR.ItemsSource = DisplayService.Instance.HistogramR;
                        if (DisplayService.Instance.IsCheckedG && DisplayService.Instance.HistogramG?.Count > 0)
                            BarChartSeriesG.ItemsSource = DisplayService.Instance.HistogramG;
                        if (DisplayService.Instance.IsCheckedB && DisplayService.Instance.HistogramB?.Count > 0)
                            BarChartSeriesB.ItemsSource = DisplayService.Instance.HistogramB;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                    break;
                default:
                    throw new NotImplementedException("Not defined Channel.");
            }
        }
        private void ChangeChannelType(P2dChannels channel)
        {
            if (channel == P2dChannels.P2D_CHANNELS_1 && _type != ChannelType.Mono)
            {
                _type = ChannelType.Mono;
                ReLoadHistogramView();
            }

            else if (channel == P2dChannels.P2D_CHANNELS_3 && _type != ChannelType.COMBINE)
            {
                _type = ChannelType.COMBINE;
                ReLoadHistogramView();
            }
        }

        private int _lastMin = 0;
        private int _lastMax = 255;

        private void UpdateHistogramMinMax(P2dChannels channel)
        {
            ChangeChannelType(channel);

            var min = 0;
            var max = 255;
            switch (_type)
            {
                case ChannelType.Mono:
                    min = DisplayService.Instance.MinMono;
                    max = DisplayService.Instance.MaxMono;
                    break;
                case ChannelType.COMBINE:
                    min = DisplayService.Instance.MinCombine;
                    max = DisplayService.Instance.MaxCombine;
                    break;
            }
            bool isChanged = false;
            if (min != _lastMin)
            {
                isChanged = true;
                _lastMin = min;
            }
            if (max != _lastMax)
            {
                isChanged = true;
                _lastMax = max;
            }
            if (isChanged)
            {
                DetachEvent();
                this.Dispatcher.Invoke(() =>
                {
                    Num_min.Value = _lastMin;
                    Num_max.Value = _lastMax;
                }, System.Windows.Threading.DispatcherPriority.Input);
                AttachEvent();
            }
        }

        private void HistogramView_Loaded(object sender, RoutedEventArgs e)
        {
            ReLoadHistogramView();
        }

        private void ReLoadHistogramView()
        {
            DetachEvent();
            int min = 0, max = 0;
            switch (_type)
            {
                case ChannelType.Mono:
                    min = DisplayService.Instance.MinMono;
                    max = DisplayService.Instance.MaxMono;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Num_min.Value = DisplayService.Instance.MinMono;
                        Num_max.Value = DisplayService.Instance.MaxMono;
                    });
                    break;
                case ChannelType.COMBINE:
                    min = DisplayService.Instance.MinCombine;
                    max = DisplayService.Instance.MaxCombine;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Num_min.Value = DisplayService.Instance.MinCombine;
                        Num_max.Value = max = DisplayService.Instance.MaxCombine;
                    });

                    break;
                default:
                    throw new NotImplementedException("Not defined ChannelType.");
            }
            DisplayService.Instance.UpdateMinMax(min, max, _type);

            //var usedMax = (1 << ThorlabsCamera.Instance.UsedValidBits);
            //var step = usedMax / 64;
            var step = (max + 1) / 64;
            var tempSeries = new List<ChartPoint>();
            for (int i = 0; i < 64; i++)
            {
                tempSeries.Add(new ChartPoint(i * step, double.NaN));
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                BarSeriesHorizontalAxis.ItemsSource = tempSeries;
            });

            AttachEvent();
        }

        private bool _isAttached = false;
        protected override bool OnClosing()
        {
            if (_isAttached)
            {
                eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Unsubscribe(UpdateHistogram);
                eventAggregator.GetEvent<UpdatePopupHistogramMinMaxEvent>().Unsubscribe(UpdateHistogramMinMax);
                _isAttached = false;
            }
            DetachEvent();
            if (this.DataContext is IUpdate @interface)
            {
                @interface.StopUpdate();
            }
            return base.OnClosing();
        }

        protected override void OnActivated(EventArgs e)
        {
            if (!_isAttached)
            {
                eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Subscribe(UpdateHistogram, ThreadOption.BackgroundThread);
                eventAggregator.GetEvent<UpdatePopupHistogramMinMaxEvent>().Subscribe(UpdateHistogramMinMax, ThreadOption.BackgroundThread);
                _isAttached = true;
            }

            if (this.DataContext is IUpdate @interface)
            {
                @interface.StartUpdate();
            }

            base.OnActivated(e);
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

        private void AttachEvent()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Num_min.ValueChanged += UpdateMinValue;
                Num_max.ValueChanged += UpdateMaxValue;
            });
        }

        private void DetachEvent()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Num_min.ValueChanged -= UpdateMinValue;
                Num_max.ValueChanged -= UpdateMaxValue;
            });
        }


        #region Events
        private void UpdateMinValue(object sender, RadRangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == null)
            {
                Num_min.Value = e.OldValue;
            }
            else if (e.NewValue != e.OldValue)
            {
                var min = Convert.ToInt32(Num_min.Value);
                var max = Convert.ToInt32(Num_max.Value);

                DisplayService.Instance.UpdateMinMax(min, max, _type);

            }
        }
        private void UpdateMaxValue(object sender, RadRangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == null)
            {
                Num_max.Value = e.OldValue;
            }
            else if (e.NewValue != e.OldValue)
            {
                var min = Convert.ToInt32(Num_min.Value);
                var max = Convert.ToInt32(Num_max.Value);
                DisplayService.Instance.UpdateMinMax(min, max, _type);

            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetToLogarithmicAxis();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetToLinearAxis();
        }

        private void ann_show_textBlock_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEnterBarSeries)
            {
                ann_show.HorizontalValue = ann_show.VerticalValue = -1;
                return;
            }
            HistogramViewModel viewModel = this.DataContext as HistogramViewModel;
            int maxValue = (int)viewModel.HoriAxisMax;
            //int.TryParse(axisMax_txt.Text, out int maxValue);
            Point mousePosition = e.GetPosition(chart_histogram);
            Point textBlockTopLeftPoint = new Point(mousePosition.X, 15);
            var chartMaxPoint = chart_histogram.ConvertDataToPoint(new DataTuple(maxValue, 0));
            if (textBlockTopLeftPoint.X + ann_show_textBlock.ActualWidth > chartMaxPoint.X)
            {
                textBlockTopLeftPoint.X = chartMaxPoint.X - ann_show_textBlock.ActualWidth;
            }
            var ann_show_Tuple = chart_histogram.ConvertPointToData(textBlockTopLeftPoint);

            ann_show.HorizontalValue = ann_show_Tuple.FirstValue;
            ann_show.VerticalValue = ann_show_Tuple.SecondValue;
        }

        System.Windows.Point _lastPoint;
        private void chart_histogram_MouseMove(object sender, MouseEventArgs e)
        {
            ann_show_textBlock_MouseMove(sender, e);
        }
        #endregion

        private void chart_histogram_MouseLeave(object sender, MouseEventArgs e)
        {
            ann_show.HorizontalValue = ann_show.VerticalValue = -1;
        }

        private void AutoButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_type)
            {
                case ChannelType.Mono:
                    DisplayService.Instance.IsAutoScalEnable = true;
                    break;
                case ChannelType.R:
                    DisplayService.Instance.IsAutoRScalEnable = true;
                    break;
                case ChannelType.G:
                    DisplayService.Instance.IsAutoGScalEnable = true;
                    break;
                case ChannelType.B:
                    DisplayService.Instance.IsAutoBScalEnable = true;
                    break;
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            int maxVal = 0;
            if (_type == ChannelType.Mono)
                maxVal = (1 << ThorlabsCamera.Instance.UsedValidBits) - 1;
            else
                maxVal = 255;
            UpdateWithMinMax(0, maxVal);
            _lastMin = 0;
            _lastMax = maxVal;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                RestoreDirectory = true,
                Filter = "Csv File|*.csv"
            };
            if (saveDialog.ShowDialog() == true)
            {
                var fileName = saveDialog.FileName;
                if (!fileName.EndsWith(".csv"))
                {
                    fileName += ".csv";
                }

                if (System.IO.File.Exists(fileName))
                {
                    try
                    {
                        System.IO.File.Delete(fileName);
                    }
                    catch (Exception)
                    {
                        eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(fileName, ErrorType.FileOccupied));
                        return;
                    }
                }
                try
                {
                    var values = new List<ChartPoint>((IEnumerable<ChartPoint>)BarChartSeries.ItemsSource);
                    var bits = (1 << ThorlabsCamera.Instance.UsedValidBits);
                    var step = bits / values.Count;

                    using (var fs = new System.IO.FileStream(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    {
                        using (var sw = new System.IO.StreamWriter(fs, new UnicodeEncoding()))
                        {
                            sw.Write($"Category\tValue_{_type}\n");
                            for (int i = 0; i < values.Count; i++)
                            {
                                var v = values[i];
                                var x = v.XValue * step;
                                sw.Write($"{x}~{x + step}\t{v.YValue}\n");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs($"Histogram_{_type}, {fileName}", ErrorType.ExportFileFailed));
                }
                eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Export, new Exception($"Histogram_{_type}, {fileName}")));
            }
        }

        private void UpdateWithMinMax(int min = 0, int max = 255)
        {
            DetachEvent();
            Num_min.Value = min;
            Num_max.Value = max;
            AttachEvent();
            DisplayService.Instance.UpdateMinMax(min, max, _type);
        }

        private void ChartTrackBallBehavior_TrackInfoUpdated(object sender, TrackBallInfoEventArgs e)
        {
            List<DataPointInfo> closestDataPoints = e.Context.DataPointInfos;
            DataPointInfo barPointInfo = closestDataPoints.Find(x => x.DisplayHeader.ToString() == nameof(BarSeries));
            if (barPointInfo != null)
            {
                var point = barPointInfo.DataPoint.DataItem as ChartPoint;
                if (point == null) return;

                if (BarChartSeries.ItemsSource != null)//Mono
                {
                    if (BarChartSeries.DataPoints.Count > point.XValue)
                    {
                        var whitePoint = BarChartSeries.DataPoints[point.XValue].DataItem as ChartPoint;
                        string s = $"{whitePoint.YValue}";
                        ann_show_textBlock.Text = s;
                    }

                }
                else if (BarChartSeriesR.ItemsSource != null && BarChartSeriesG.ItemsSource != null && BarChartSeriesB.ItemsSource != null)
                {
                    string s = String.Empty;
                    if (BarChartSeriesR.DataPoints.Count > point.XValue)
                    {
                        var redPoint = BarChartSeriesR.DataPoints[point.XValue].DataItem as ChartPoint;
                        s += $"R:{redPoint.YValue}";
                    }
                    if (BarChartSeriesG.DataPoints.Count > point.XValue)
                    {
                        var greenPoint = BarChartSeriesG.DataPoints[point.XValue].DataItem as ChartPoint;
                        if (s != String.Empty) s += " ";
                        s += $"G:{greenPoint.YValue}";
                    }
                    if (BarChartSeriesB.DataPoints.Count > point.XValue)
                    {
                        var bluePoint = BarChartSeriesB.DataPoints[point.XValue].DataItem as ChartPoint;
                        if (s != String.Empty) s += " ";
                        s += $"B:{bluePoint.YValue}";
                    }
                    ann_show_textBlock.Text = s;
                }
            }
        }

        bool _isEnterBarSeries = false;
        private void BarSeries_MouseEnter(object sender, MouseEventArgs e)
        {
            UIElement bar = (UIElement)sender;
            bar.Opacity = 1;
            if (!_isEnterBarSeries)
                _isEnterBarSeries = true;
        }

        private void BarSeries_MouseLeave(object sender, MouseEventArgs e)
        {
            UIElement bar = (UIElement)sender;
            bar.Opacity = 0.5;
            if (_isEnterBarSeries)
                _isEnterBarSeries = false;
        }
    }
}
