using Prism.Events;
using System.Collections.Specialized;

namespace FilterWheelShared.Event
{
    public class StatisticItemSelectedEvent : PubSubEvent<NotifyCollectionChangedEventArgs>
    {
    }

    public class ROISelectedEvent : PubSubEvent
    {

    }
}
