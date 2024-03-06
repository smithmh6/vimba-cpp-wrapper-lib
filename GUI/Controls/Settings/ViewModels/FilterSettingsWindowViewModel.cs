using FilterWheelShared.DeviceDataService;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;

namespace Settings.ViewModels
{
    public class SlotSettingEx : SlotSetting
    {
        private double _exposureTime;
        public override double ExposureTime 
        {
            get => _exposureTime;
            set
            {
                var exposureParams = ThorlabsCamera.Instance.ExposureTimeParams;
                var target = Math.Clamp(value, exposureParams.min_value, exposureParams.max_value);
                target = Math.Floor((target - exposureParams.min_value) / exposureParams.increment) * exposureParams.increment + exposureParams.min_value;
                SetProperty(ref _exposureTime, target);
            }
        }

        private double _gain;
        public override double Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        private bool _isAutoExposure;
        public override bool IsAutoExposure
        {
            get => _isAutoExposure;
            set => SetProperty(ref _isAutoExposure, value);
        }

        private bool _isAutoGain;
        public override bool IsAutoGain
        {
            get => _isAutoGain;
            set => SetProperty(ref _isAutoGain, value);
        }

        private bool _isChecked = false;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetProperty(ref _isChecked, value) && _isChecked)
                    _callback?.Invoke(Index);
            }
        }
        //Id and GroupName will never be changed
        public int Id { get; }
        public string GroupName { get; }

        private readonly Action<int> _callback = null;
        public SlotSettingEx(int settingIndex, int slotIndex, Action<int> callback) : base(settingIndex)
        {
            Id = Index + 1;
            GroupName = $"Group_Slot_{slotIndex}";
            _callback = callback;
        }

        internal void UpdateExposureWithoutCalculate(double exposure)
        {
            _exposureTime = exposure;
            RaisePropertyChanged(nameof(ExposureTime));
        }
    }

    public class SimpleSlotForFilterSettings : BindableBase
    {
        private string _name;
        public string Name 
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public int SelectedSettingIndex { get; set; }
        public SlotSettingEx[] Settings { get; } = new SlotSettingEx[4];
    }

    public class FilterSettingsWindowViewModel : BindableBase
    {
        public DelegateCommand OkCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }

        private readonly ObservableCollection<SimpleSlotForFilterSettings> _simpleSlots = new ObservableCollection<SimpleSlotForFilterSettings>();
        public ObservableCollection<SimpleSlotForFilterSettings> SimpleSlots => _simpleSlots;

        private ThorlabsCamera _camera = ThorlabsCamera.Instance;

        public Tuple<double, double> ExposureTimeRange
        {
            get => new Tuple<double, double>(_camera.ExposureTimeParams.min_value, _camera.ExposureTimeParams.max_value);
        }

        public double ExposureTimeIncrement => _camera.ExposureTimeParams.increment;

        public Tuple<double, double> GainRange
        {
            get => _camera.GainRange;
        }

        public FilterSettingsWindowViewModel()
        {
            OkCommand = new DelegateCommand(OkCommandExecute);
            CancelCommand = new DelegateCommand(CancelCommandExecute);
            _simpleSlots.Clear();
            int count = DisplayService.Instance.Slots.Count;
            for (int i = 0; i < count; i++)
            {
                _simpleSlots.Add(new SimpleSlotForFilterSettings());
            }
            //UpdateSlots();
        }
        private void OkCommandExecute()
        {
            int count = DisplayService.Instance.Slots.Count;
            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                var slot = DisplayService.Instance.Slots[slotIndex];
                var simpleSlot = _simpleSlots[slotIndex];
                slot.SlotParameters.CurrentSettingIndex = simpleSlot.SelectedSettingIndex;
                for (int settingIndex = 0; settingIndex < 4; settingIndex++)
                {
                    slot[settingIndex].ExposureTime = simpleSlot.Settings[settingIndex].ExposureTime;
                    slot[settingIndex].IsAutoExposure = simpleSlot.Settings[settingIndex].IsAutoExposure;
                    slot[settingIndex].Gain = simpleSlot.Settings[settingIndex].Gain;
                    slot[settingIndex].IsAutoGain = simpleSlot.Settings[settingIndex].IsAutoGain;

                }
            }
        }

        private void CancelCommandExecute()
        {
            UpdateSlots();
        }

        public void UpdateSlotSetting()
        {
            var slotIndex = DisplayService.Instance.CurrentSlotIndex;
            var settingIndex = DisplayService.Instance.CurrentSettingIndex;
            var slot = DisplayService.Instance.Slots[slotIndex];
            var simpleSlot = _simpleSlots[slotIndex];
            simpleSlot.SelectedSettingIndex = settingIndex;
            simpleSlot.Settings[settingIndex].UpdateExposureWithoutCalculate(slot[settingIndex].ExposureTime);
            simpleSlot.Settings[settingIndex].IsAutoExposure = slot[settingIndex].IsAutoExposure;
            simpleSlot.Settings[settingIndex].Gain = slot[settingIndex].Gain;
            simpleSlot.Settings[settingIndex].IsAutoGain = slot[settingIndex].IsAutoGain;
            simpleSlot.Settings[settingIndex].IsChecked = simpleSlot.SelectedSettingIndex == settingIndex;
        }

        public void UpdateSlots()
        {
            int count = DisplayService.Instance.Slots.Count;
            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                var slot = DisplayService.Instance.Slots[slotIndex];
                var simpleSlot = _simpleSlots[slotIndex];
                simpleSlot.Name = slot.SlotName;
                simpleSlot.SelectedSettingIndex = slot.SlotParameters.CurrentSettingIndex;
                for (int settingIndex = 0; settingIndex < 4; settingIndex++)
                {
                    if (simpleSlot.Settings[settingIndex] == null)
                    {
                        simpleSlot.Settings[settingIndex] = new SlotSettingEx(settingIndex, slotIndex, (index) =>
                        {
                            simpleSlot.SelectedSettingIndex = index;
                        });
                    }
                    simpleSlot.Settings[settingIndex].UpdateExposureWithoutCalculate(slot[settingIndex].ExposureTime);
                    simpleSlot.Settings[settingIndex].IsAutoExposure = slot[settingIndex].IsAutoExposure;
                    simpleSlot.Settings[settingIndex].Gain = slot[settingIndex].Gain;
                    simpleSlot.Settings[settingIndex].IsAutoGain = slot[settingIndex].IsAutoGain;
                    simpleSlot.Settings[settingIndex].IsChecked = simpleSlot.SelectedSettingIndex == settingIndex;
                }
            }
        }
    }
}
