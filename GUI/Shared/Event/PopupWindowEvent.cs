using Prism.Events;
using System.ComponentModel;

namespace FilterWheelShared.Event
{
    public enum PopupWindowKey
    {
        [Description("Ruler Configuration Window")]
        RulerConfigWindowKey,
    }

    public class PopupWindowEvent : PubSubEvent<PopupWindowKey>
    {
    }
}
