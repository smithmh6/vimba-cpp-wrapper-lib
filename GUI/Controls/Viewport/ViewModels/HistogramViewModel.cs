using Prism.Events;
using Prism.Mvvm;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using System.Diagnostics;
using FilterWheelShared.ImageProcess;

namespace Viewport.ViewModels
{
    public class HistogramViewModel : BindableBase, IUpdate
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();

        #region Properties
        private uint _horiAxisMin = 0;
        public uint HoriAxisMin
        {
            get => _horiAxisMin;
            set
            {
                if (SetProperty(ref _horiAxisMin, value))
                {
                    RaisePropertyChanged(nameof(HoriZoomStart));
                }
            }
        }

        private uint _horiAxisMax = 255;
        public uint HoriAxisMax
        {
            get => _horiAxisMax;
            set
            {
                if (SetProperty(ref _horiAxisMax, value))
                {
                    RaisePropertyChanged(nameof(HoriZoomEnd));
                }
            }
        }

        private uint _maxLimit = 255;
        public uint MaxLimit
        {
            get => _maxLimit;
            private set
            {
                if (SetProperty(ref _maxLimit, value))
                {
                    RaisePropertyChanged(nameof(HoriZoomStart));
                    RaisePropertyChanged(nameof(HoriZoomEnd));
                }
            }
        }

        public double HoriZoomStart => (double)_horiAxisMin / (double)MaxLimit;
        public double HoriZoomEnd => (double)_horiAxisMax / (double)MaxLimit;

        #endregion

        private ChannelType _channelType;
        private IEventAggregator _eventAggregator;
        public HistogramViewModel(ChannelType channelType)
        {
            _channelType = channelType;
            //ResetCommand = new DelegateCommand(ResetExecute);
            //AutoCommand = new DelegateCommand(AutoExecute);
            _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
            _eventAggregator.GetEvent<ImageValidbitsChanged>().Subscribe(UpdateLimit);
            _eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Subscribe(ChangeChannelType);
            UpdateLimit(ThorlabsCamera.Instance.UsedValidBits);
        }

        private void UpdateLimit(int validBits)
        {
            switch (_channelType)
            {
                case ChannelType.Mono:
                    HoriAxisMax = MaxLimit = (uint)(DisplayService.Instance.MaxMono);
                    break;
                case ChannelType.COMBINE:
                    HoriAxisMax = MaxLimit = (uint)(DisplayService.Instance.MaxR);
                    break;
            }
        }

        //private void ResetExecute()
        //{
        //    HistogramMin = 0;
        //    HistogramMax = 255;
        //}

        //public void AutoExecute()
        //{

        //}

        //private void UpdateHistogram(int arg)
        //{
        //    uint ymax = _yMax;
        //    List<ChartPoint> points = null;
        //    switch (_channelType)
        //    {
        //        case ChannelType.Mono:
        //            ymax = DisplayService.Instance.HistogramMonoYMax;
        //            points = DisplayService.Instance.HistogramMono;
        //            break;
        //        case ChannelType.R:
        //            ymax = DisplayService.Instance.HistogramRYMax;
        //            points = DisplayService.Instance.HistogramR;
        //            break;
        //        case ChannelType.G:
        //            ymax = DisplayService.Instance.HistogramGYMax;
        //            points = DisplayService.Instance.HistogramG;
        //            break;
        //        case ChannelType.B:
        //            ymax = DisplayService.Instance.HistogramBYMax;
        //            points = DisplayService.Instance.HistogramB;
        //            break;
        //    }
        //    if (points == null || points.Count == 0)
        //        return;

        //    uint count = (uint)points.Count;
        //    YMax = (uint)(ymax * 1.05);
        //    MaxLimit = count - 1;
        //    if (HistSeries == null)
        //    {
        //        HistSeries = new ObservableCollection<ChartPoint>(points);
        //        return;
        //    }

        //    for (int i = 0; i < count; i++)
        //    {
        //        if (i < HistSeries.Count)
        //        {
        //            System.Windows.Application.Current?.Dispatcher.Invoke(() => HistSeries[i] = points[i]);
        //        }
        //        else
        //            System.Windows.Application.Current?.Dispatcher.Invoke(() => HistSeries.Add(points[i]));
        //    }
        //}

        private bool _isUpdating = false;
        public void StartUpdate(object param = null)
        {
            if (_isUpdating) return;
            //start update
            //_eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Subscribe(UpdateLimit, ThreadOption.BackgroundThread);
            //_eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Subscribe(UpdateHistogram);
            switch (_channelType)
            {
                case ChannelType.Mono:
                    DisplayService.Instance.IsHistogramShown = true;
                    break;
                case ChannelType.COMBINE:
                    DisplayService.Instance.SetRGBHistogramRGBShow(true);
                    break;
                default:
                    throw new NotImplementedException("Not defined channel type");
            }
            _isUpdating = true;
        }

        public void StopUpdate(object param = null)
        {
            if (!_isUpdating) return;
            //stop update
            //_eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Unsubscribe(UpdateLimit);
            //_eventAggregator.GetEvent<UpdatePopupHistogramEvent>().Unsubscribe(UpdateHistogram);
            switch (_channelType)
            {
                case ChannelType.Mono:
                    DisplayService.Instance.IsHistogramShown = false;
                    break;
                case ChannelType.COMBINE:
                    DisplayService.Instance.SetRGBHistogramRGBShow(false);
                    break;
                default:
                    throw new NotImplementedException("Not defined channel type");
            }
            _isUpdating = false;
        }

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(HistogramViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        private void ChangeChannelType(P2dChannels channel)
        {
            if (channel == P2dChannels.P2D_CHANNELS_1 && _channelType != ChannelType.Mono)
            {
                _channelType = ChannelType.Mono;
                UpdateLimit(-1);
            }
            else if (channel == P2dChannels.P2D_CHANNELS_3 && _channelType != ChannelType.COMBINE)
            {
                _channelType = ChannelType.COMBINE;
                UpdateLimit(-1);
            }


        }
    }
}
