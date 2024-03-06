using DrawingTool.Factory;
using DrawingTool.Factory.Material;
using System.Windows.Media;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Materials
{
    public class CustomScalerViewModel : ROIViewModelBase
    {
        private FontFamily _fontFamily;
        public FontFamily FontFamily
        {
            get { return _fontFamily; }
            set { SetProperty(ref _fontFamily, value); }
        }

        private double _fontSize;
        public double FontSize
        {
            get { return _fontSize; }
            set { SetProperty(ref _fontSize, value); }
        }

        private Color _fontColor;
        public Color FontColor
        {
            get { return _fontColor; }
            set { SetProperty(ref _fontColor, value); }
        }

        private int _lineWidth;
        public int LineWidth
        {
            get { return _lineWidth; }
            set { SetProperty(ref _lineWidth, value); }
        }

        private Color _lineColor;
        public Color LineColor
        {
            get { return _lineColor; }
            set { SetProperty(ref _lineColor, value); }
        }

        private int _panelOpacity;
        public int PanelOpacity
        {
            get { return _panelOpacity; }
            set { SetProperty(ref _panelOpacity, value); }
        }

        private Color _panelColor;
        public Color PanelColor
        {
            get { return _panelColor; }
            set { SetProperty(ref _panelColor, value); }
        }

        private RelativePlacement _scalerPlacement;
        public RelativePlacement ScalerPlacement
        {
            get { return _scalerPlacement; }
            set { SetProperty(ref _scalerPlacement, value); }
        }

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
