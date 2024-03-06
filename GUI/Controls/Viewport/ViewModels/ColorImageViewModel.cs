using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using Thorlabs.CustomControls.TelerikAndSciChart.Controls.ColorMapEditor;

namespace Viewport.ViewModels
{
    public class ChannelProp : BindableBase
    {
        public string ChannelName { get; }

        private ThorColor _channelColor;
        public ThorColor ChannelColor
        {
            get => _channelColor;
            set
            {
                if (value == null) return;
                if (SetProperty(ref _channelColor, value))
                {
                    switch (ChannelName)
                    {
                        case "R":
                            DisplayService.Instance.ColorR = _channelColor;
                            break;
                        case "G":
                            DisplayService.Instance.ColorG = _channelColor;
                            break;
                        case "B":
                            DisplayService.Instance.ColorB = _channelColor;
                            break;
                        case "A":
                            DisplayService.Instance.ColorMono = _channelColor;
                            break;
                    }
                }
            }
        }

        private ThorColor _previousColor;
        private ThorColor _defaultColor;

        public ChannelProp(string channelName, ThorColor channelColor)
        {
            ChannelName = channelName;
            _channelColor = channelColor;
            RaisePropertyChanged(nameof(ChannelColor));
            _previousColor = channelColor;
            _defaultColor = channelColor;
        }

        public void Apply()
        {
            _previousColor = _channelColor;
        }

        public void Revoke()
        {
            ChannelColor = _previousColor;
        }

        public void OnCustomColorRemove(ThorColor color)
        {
            if (_previousColor == color)
                _previousColor = _defaultColor;

            Revoke();
        }
    }

    public class ColorImageViewModel : BindableBase, IUpdate
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();
        #region Properties
        private ObservableCollection<ChannelProp> _channelList = new ObservableCollection<ChannelProp>();
        private ObservableCollection<ThorColor> _colorList = new ObservableCollection<ThorColor>();
        public ObservableCollection<ChannelProp> ChannelList => _channelList;
        public ObservableCollection<ThorColor> ColorList => _colorList;
        #endregion

        public ColorImageViewModel()
        {
            var systemColorList = ThorColorService.GetInstance().SystemColors;
            _colorList.AddRange(systemColorList);
            var customColorList = ThorColorService.GetInstance().CustomColors;
            _colorList.AddRange(customColorList);
            _colorList.CollectionChanged += ColorList_CollectionChanged;
        }

        private void ColorList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                var colors = e.NewItems;
                foreach (ThorColor color in colors)
                {
                    ThorColorService.GetInstance().AddCustomColor(color);
                }
                return;
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                var colors = e.OldItems;
                foreach (ThorColor color in colors)
                {
                    foreach (var item in _channelList)
                    {
                        item.OnCustomColorRemove(color);
                    }
                    ThorColorService.GetInstance().RemoveCustomColor(color);
                }
                return;
            }
        }

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(ColorImageViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        public void StartUpdate(object param = null)
        {
            _channelList.Clear();
            if (DisplayService.Instance.IsColor)
            {
                //when R checked
                _channelList.Add(new ChannelProp("R", DisplayService.Instance.ColorR));
                //when G checked
                _channelList.Add(new ChannelProp("G", DisplayService.Instance.ColorG));
                //when B checked
                _channelList.Add(new ChannelProp("B", DisplayService.Instance.ColorB));
            }
            else
                _channelList.Add(new ChannelProp("A", DisplayService.Instance.ColorMono));
        }

        public void StopUpdate(object param = null)
        {
            if (param is bool apply)
            {
                if (apply)
                {
                    foreach (var item in _channelList)
                    {
                        item.Apply();
                    }
                }
                else
                {
                    foreach (var item in _channelList)
                    {
                        item.Revoke();
                    }
                }
            }
            //throw new System.NotImplementedException();
        }
    }
}
