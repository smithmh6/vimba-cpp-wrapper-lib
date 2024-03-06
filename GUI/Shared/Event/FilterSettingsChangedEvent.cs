using Prism.Events;

namespace FilterWheelShared.Event
{
    public enum FilterSettingsEventType
    {
        Index,
        Exposure,
        AutoExposure,
        Gain,
        AutoGain,
    }
    public class FilterSettingsChangedEventArg
    {
        public FilterSettingsEventType Type { get; private set; }
        public object Value { get; private set; }
        public FilterSettingsChangedEventArg(FilterSettingsEventType type, object value)
        {
            Type = type;
            Value = value;
        }
    }
    public class FilterSettingsChangedEvent : PubSubEvent<FilterSettingsChangedEventArg>
    {
    }
}
