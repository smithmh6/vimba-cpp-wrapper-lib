using DrawingTool;

namespace FilterWheelShared.Controls.DrawingTools
{
    public class CustomCoordinate : Coordinate
    {
        protected override int MaxZoomTimes => 0;
        protected override int OnUpdateMaxZoomSacale()
        {
            return 20;
        }
    }
}
