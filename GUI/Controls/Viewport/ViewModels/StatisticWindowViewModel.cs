using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;

namespace Viewport.ViewModels
{
    public class StatisticWindowViewModel : BindableBase, IUpdate
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();

        public StatisticWindowViewModel()
        {
        }

        private bool _isColor;
        public bool IsColor
        {
            get => _isColor;
            private set => SetProperty(ref _isColor, value);
        }

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(StatisticWindowViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        private bool _isUpdating = false;
        public void StartUpdate(object param = null)
        {
            if (_isUpdating) return;
            IsColor = DisplayService.Instance.IsColor;
            //_eventAggregator.GetEvent<UpdatePopupStatisticEvent>().Subscribe(UpdateStatistic);
            DisplayService.Instance.IsStatisticsShown = true;
            _isUpdating = true;
        }

        public void StopUpdate(object param = null)
        {
            if (!_isUpdating) return;
            //_eventAggregator.GetEvent<UpdatePopupStatisticEvent>().Unsubscribe(UpdateStatistic);
            DisplayService.Instance.IsStatisticsShown = false;
            _isUpdating = false;
        }
    }
}
