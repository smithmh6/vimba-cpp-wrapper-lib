using Prism.Events;

namespace FilterWheelShared.Event
{
    public class AutoExposureEventArgs
    {
        public bool IsStable { get; private set; }
        public double Exposure { get; private set; }
        public AutoExposureEventArgs(bool isStable, double exposure)
        {
            IsStable = isStable;
            Exposure = exposure;
        }
    }
    public class AutoExposureEvent : PubSubEvent<AutoExposureEventArgs>
    {
    }
}
