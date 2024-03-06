using DrawingTool.Factory.Material;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Materials
{
    public class CustomRulerViewModel : RulerViewModel
    {
        private decimal _xPhysicalRatio = 1;
        public decimal XPhysicalRatio
        {
            get { return _xPhysicalRatio; }
            set { SetProperty(ref _xPhysicalRatio, value); }
        }

        private decimal _yPhysicalRatio = 1;
        public decimal YPhysicalRatio
        {
            get { return _yPhysicalRatio; }
            set { SetProperty(ref _yPhysicalRatio, value); }
        }
    }
}
