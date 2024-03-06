using Prism.Events;
using System;
using FilterWheelShared.ImageProcess;

namespace FilterWheelShared.Event
{
    //Image,IsLoading,IsThumbnailUpdate
    public class UpdateDisplayImageEvent : PubSubEvent<Tuple<ImageData, bool>>
    {
    }
}
