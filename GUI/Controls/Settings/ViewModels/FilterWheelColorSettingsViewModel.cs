using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using Thorlabs.CustomControls.TelerikAndSciChart.Controls.ColorMapEditor;

namespace Settings.ViewModels
{  
    public class FilterWheelColorSettingsViewModel : BindableBase
    {
        public DelegateCommand DefaultCommand { get; private set; }
        public DelegateCommand OKCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }

        private readonly ObservableCollection<ThorColor> _colorList = new ObservableCollection<ThorColor>();
        public ObservableCollection<ThorColor> ColorList => _colorList;

        private readonly ObservableCollection<SimpleSlotForColorSettings> _simpleSlots = new ObservableCollection<SimpleSlotForColorSettings>();
        public ObservableCollection<SimpleSlotForColorSettings> SimpleSlots => _simpleSlots;

        public FilterWheelColorSettingsViewModel() 
        {
            var systemColorList = ThorColorService.GetInstance().SystemColors;
            _colorList.AddRange(systemColorList);
            var customColorList = ThorColorService.GetInstance().CustomColors;
            _colorList.AddRange(customColorList);
            _colorList.CollectionChanged += ColorList_CollectionChanged;
            DefaultCommand = new DelegateCommand(DefaultExecute);
            OKCommand = new DelegateCommand(OkExecute);
            CancelCommand = new DelegateCommand(CancelExecute);
            //UpdateSource();
        }

        private void DefaultExecute()
        {
            int count = _simpleSlots.Count;
            for (int i = 0; i < count; i++)
            {
                _simpleSlots[i].Name = $"Slot {i}";
                _simpleSlots[i].Color = ThorColorService.GetInstance().GrayColor;
            }
        }

        private void OkExecute()
        {
            DisplayService.Instance.UpdateDisplayImgAndThumbnailsAsColorConfig(_simpleSlots);
        }

        private void CancelExecute()
        {
            int count = _simpleSlots.Count;
            for (int i = 0; i < count; i++)
            {
                _simpleSlots[i].Name = DisplayService.Instance.Slots[i].SlotName;
                _simpleSlots[i].Color = DisplayService.Instance.Slots[i].SlotParameters.SlotColor;
            }
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
                    ThorColorService.GetInstance().RemoveCustomColor(color);
                }
                return;
            }
        }

        public void UpdateSource()
        {
            _simpleSlots.Clear();
            foreach (var slot in DisplayService.Instance.Slots)
            {
                _simpleSlots.Add(new SimpleSlotForColorSettings()
                {
                    Name = slot.SlotName,
                    Color = slot.SlotParameters.SlotColor
                });
            }
        }
    }
}
