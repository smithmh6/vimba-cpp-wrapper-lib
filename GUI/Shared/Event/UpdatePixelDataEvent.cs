using Prism.Events;
using FilterWheelShared.Common;

namespace FilterWheelShared.Event
{
    public class PixelData
    {
        public IntPoint Location { get; protected set; }
    }

    public class PixelDataMono : PixelData
    {
        public int Mono { get; private set; }
        private PixelDataMono() { }
        public PixelDataMono(IntPoint location, int mono)
        {
            Location = location;
            Mono = mono;
        }
    }

    public class PixelDataRGB : PixelData
    {
        public int? R { get; private set; }
        public int? G { get; private set; }
        public int? B { get; private set; }

        private PixelDataRGB() { }
        public PixelDataRGB(IntPoint location, int? r, int? g, int? b)
        {
            Location = location;
            R = r;
            G = g;
            B = b;
        }
    }

    public class UpdatePixelDataEvent : PubSubEvent<PixelData>
    {
    }
}
