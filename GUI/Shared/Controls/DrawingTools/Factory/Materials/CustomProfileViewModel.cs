using DrawingTool.Factory.Material;
using System.Windows;
using System.Windows.Media;
using FilterWheelShared.DeviceDataService;
using Point = System.Windows.Point;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Materials
{
    public class CustomProfileViewModel : GraphicRelativeLineViewModelBase
    {
        private Point _startPoint;
        public Point StartPoint
        {
            get => _startPoint;
            set
            {
                SetProperty(ref _startPoint, value);
                OccupationRect = new Rect(_startPoint, _endPoint);
                //DisplayService.Instance.ProfileStartPoint = new Event.Point((int)StartPoint.X, (int)StartPoint.Y);
            }
        }

        private Point _startPixelPoint;
        public Point StartPixelPoint
        {
            get => _startPixelPoint;
            set
            {
                SetProperty(ref _startPixelPoint, value);
                DisplayService.Instance.ProfileStartPoint = new Common.IntPoint((int)_startPixelPoint.X, (int)_startPixelPoint.Y);
            }
        }

        private Point _endPoint;
        public Point EndPoint
        {
            get => _endPoint;
            set
            {
                SetProperty(ref _endPoint, value);
                OccupationRect = new Rect(_startPoint, _endPoint);
                //DisplayService.Instance.ProfileEndPoint = new Event.Point((int)_endPoint.X, (int)_endPoint.Y);
            }
        }

        private Point _endPixelPoint;
        public Point EndPixelPoint
        {
            get => _endPixelPoint;
            set
            {
                SetProperty(ref _endPixelPoint, value);
                DisplayService.Instance.ProfileEndPoint = new Common.IntPoint((int)_endPixelPoint.X, (int)_endPixelPoint.Y);
            }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set
            {
                if (value < 0) return;
                SetProperty(ref _width, value);
                DisplayService.Instance.ProfileLineWidth = (int)_width;
            }
        }
        public CustomProfileViewModel()
        {
            Stroke = new SolidColorBrush(Colors.DeepSkyBlue);
        }

        private bool _isMoving;

        public bool IsMoving
        {
            get => _isMoving;
            set => SetProperty(ref _isMoving, value);
        }

        public void Move(Vector vector)
        {
            StartPoint += vector;
            EndPoint += vector;
        }
    }
}
