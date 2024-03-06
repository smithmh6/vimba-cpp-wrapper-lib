using DrawingTool;
using DrawingTool.Factory;
using DrawingTool.Factory.Products;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Products
{
    public class GraphicCustomScaler : GraphicROIBase
    {

        public FontFamily FontFamily
        {
            get { return (FontFamily)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(GraphicCustomScaler), new PropertyMetadata(new FontFamily("Segoe UI"), FontFamilyPropertyChangedCallback));

        private static void FontFamilyPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }
        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(GraphicCustomScaler), new PropertyMetadata(16.0, FontSizePropertyChangedCallback));

        private static void FontSizePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public Color FontColor
        {
            get { return (Color)GetValue(FontColorProperty); }
            set { SetValue(FontColorProperty, value); }
        }

        public static readonly DependencyProperty FontColorProperty =
            DependencyProperty.Register("FontColor", typeof(Color), typeof(GraphicCustomScaler), new PropertyMetadata(Colors.Black, FontColorPropertyChangedCallback));

        private static void FontColorPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public int LineWidth
        {
            get { return (int)GetValue(LineWidthProperty); }
            set { SetValue(LineWidthProperty, value); }
        }

        public static readonly DependencyProperty LineWidthProperty =
            DependencyProperty.Register("LineWidth", typeof(int), typeof(GraphicCustomScaler), new PropertyMetadata(1, LineWidthPropertyChangedCallback));

        private static void LineWidthPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public Color LineColor
        {
            get { return (Color)GetValue(LineColorProperty); }
            set { SetValue(LineColorProperty, value); }
        }

        public static readonly DependencyProperty LineColorProperty =
            DependencyProperty.Register("LineColor", typeof(Color), typeof(GraphicCustomScaler), new PropertyMetadata(Colors.Black, LineColorPropertyChangedCallback));

        private static void LineColorPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public int PanelOpacity
        {
            get { return (int)GetValue(PanelOpacityProperty); }
            set { SetValue(PanelOpacityProperty, value); }
        }

        public static readonly DependencyProperty PanelOpacityProperty =
            DependencyProperty.Register("PanelOpacity", typeof(int), typeof(GraphicCustomScaler), new PropertyMetadata(39, PanelOpacityPropertyChangedCallback));

        private static void PanelOpacityPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        public Color PanelColor
        {
            get { return (Color)GetValue(PanelColorProperty); }
            set { SetValue(PanelColorProperty, value); }
        }

        public static readonly DependencyProperty PanelColorProperty =
            DependencyProperty.Register("PanelColor", typeof(Color), typeof(GraphicCustomScaler), new PropertyMetadata(Color.FromArgb(255, 255, 255, 255), PanelColorPropertyChangedCallback));

        private static void PanelColorPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }

        public RelativePlacement ScalerPlacement
        {
            get { return (RelativePlacement)GetValue(ScalerPlacementProperty); }
            set { SetValue(ScalerPlacementProperty, value); }
        }

        public static readonly DependencyProperty ScalerPlacementProperty =
            DependencyProperty.Register("ScalerPlacement", typeof(RelativePlacement), typeof(GraphicCustomScaler), new PropertyMetadata(RelativePlacement.BottomLeft, ScalerPlacementPropertyChangedCallback));

        private static void ScalerPlacementPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }

        public double XPhysicalRatio
        {
            get { return (double)GetValue(XPhysicalRatioProperty); }
            set { SetValue(XPhysicalRatioProperty, value); }
        }

        // Using a DependencyProperty as the backing store for XPhysicalRatio.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty XPhysicalRatioProperty =
            DependencyProperty.Register("XPhysicalRatio", typeof(double), typeof(GraphicCustomScaler), new PropertyMetadata(1.0, PhysicalRelatedPropertyChangedCallback));


        public double YPhysicalRatio
        {
            get { return (double)GetValue(YPhysicalRatioProperty); }
            set { SetValue(YPhysicalRatioProperty, value); }
        }

        // Using a DependencyProperty as the backing store for YPhysicalRatio.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty YPhysicalRatioProperty =
            DependencyProperty.Register("YPhysicalRatio", typeof(double), typeof(GraphicCustomScaler), new PropertyMetadata(1.0, PhysicalRelatedPropertyChangedCallback));

        private static void PhysicalRelatedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaler = d as GraphicCustomScaler;
            scaler.Draw();
        }


        private RectangleGeometry PhysicalRectG;

        public GraphicCustomScaler()
        {
            //RenderColor = Colors.Black;
        }
        protected override void OnDraw(DrawingContext drawingContext)
        {
            if (double.IsInfinity(Coordinate.MaxScale) || double.IsNaN(Coordinate.MaxScale))
                return;
            CalculateScalerLength();
            CalculateDisplaySize();
            CalculatePosition();

            drawingContext.DrawRoundedRectangle(new SolidColorBrush(PanelColor) { Opacity = (double)PanelOpacity / 100 }, null,
                new Rect(_startPoint.X, _startPoint.Y, _displayWidth, _displayHeight), 4, 4);

            // Get PhysicalRectG for HitTest purpose when mouse down
            var physicalRect = Coordinate.GetPhysicalRectFromScreen(new Rect(_startPoint.X, _startPoint.Y, _displayWidth, _displayHeight));
            var physicalRadius = Coordinate.GetPhysicalRectFromScreen(new Rect(0, 0, 4, 0)).Width;
            PhysicalRectG = new RectangleGeometry(physicalRect, physicalRadius, physicalRadius);

            Point _lineStart = new Point();
            Point _lineEnd = new Point();
            _lineStart.X = _startPoint.X + (_displayWidth - _lineLength) / 2;
            _lineStart.Y = _startPoint.Y + yPadding + _format.Height + 5;
            _lineEnd.X = _lineStart.X + _lineLength;
            _lineEnd.Y = _startPoint.Y + yPadding + _format.Height + 5;

            // Horizontal line
            drawingContext.DrawLine(new Pen(new SolidColorBrush(LineColor), LineWidth + 1), _lineStart, _lineEnd);
            // Vertical lines
            drawingContext.DrawLine(new Pen(new SolidColorBrush(LineColor), 2),
                new Point(_lineStart.X, _lineStart.Y - 8), new Point(_lineStart.X, _lineStart.Y + 6));
            drawingContext.DrawLine(new Pen(new SolidColorBrush(LineColor), 2),
                new Point(_lineEnd.X, _lineEnd.Y - 8), new Point(_lineEnd.X, _lineEnd.Y + 6));
            var textPoint = new Point(_startPoint.X + (_displayWidth - _format.Width) / 2, _startPoint.Y + yPadding);
            drawingContext.DrawText(_format, textPoint);

        }

        //private const int ScalerHeight = 35;
        //private double _fontHeight;
        //private double _fontWidth;
        const double xPadding = 4;
        const double yPadding = 4;
        private double _displayWidth;
        private double _displayHeight;
        private double _lineLength;
        private FormattedText _format;
        private Point _startPoint;

        private void CalculatePosition()
        {
            switch (ScalerPlacement)
            {
                case RelativePlacement.TopLeft:
                    {
                        var x = Math.Max(Coordinate.DisplayPhysicalArea.X, Coordinate.PhysicalRect.X);
                        var y = Math.Max(Coordinate.DisplayPhysicalArea.Y, Coordinate.PhysicalRect.Y);
                        _startPoint = Coordinate.GetScreenPointFromPhysical(new Point(x, y));
                        _startPoint.X += 3;
                        _startPoint.Y += 3;
                    }
                    break;
                case RelativePlacement.TopRight:
                    {
                        var x = Math.Min(Coordinate.DisplayPhysicalArea.Right, Coordinate.PhysicalRect.Right);
                        var y = Math.Max(Coordinate.DisplayPhysicalArea.Y, Coordinate.PhysicalRect.Y);
                        _startPoint = Coordinate.GetScreenPointFromPhysical(new Point(x, y));
                        _startPoint.X -= _displayWidth + 3;
                        _startPoint.Y += 3;
                    }
                    break;
                case RelativePlacement.BottomLeft:
                    {
                        var x = Math.Max(Coordinate.DisplayPhysicalArea.X, Coordinate.PhysicalRect.X);
                        var y = Math.Min(Coordinate.DisplayPhysicalArea.Bottom, Coordinate.PhysicalRect.Bottom);
                        _startPoint = Coordinate.GetScreenPointFromPhysical(new Point(x, y));
                        _startPoint.X += 3;
                        _startPoint.Y -= _displayHeight + 3;
                    }
                    break;
                case RelativePlacement.BottomRight:
                    {
                        var x = Math.Min(Coordinate.DisplayPhysicalArea.Right, Coordinate.PhysicalRect.Right);
                        var y = Math.Min(Coordinate.DisplayPhysicalArea.Bottom, Coordinate.PhysicalRect.Bottom);
                        _startPoint = Coordinate.GetScreenPointFromPhysical(new Point(x, y));
                        _startPoint.X -= _displayWidth + 3;
                        _startPoint.Y -= _displayHeight + 3;
                    }
                    break;
            }
        }
        private void CalculateScalerLength(int width = 100)
        {
            int textLength;
            double unitC = 1;
            var realLength = Coordinate.GetPhysicalRectFromScreen(new Rect(0, 0, width, 0)).Width * XPhysicalRatio;
            if (double.IsNaN(realLength))
                return;
            var unit = string.Empty;
            var digits = (int)Math.Floor(Math.Log10(realLength));
            if (digits < 0)
            {
                var w = width * 2;
                CalculateScalerLength(w);
                return;
            }
            int tmp = (int)(realLength / Math.Pow(10, digits));
            if (tmp >= 10 || tmp < 1)
                throw new Exception();
            else if (tmp >= 5)
                tmp = 5;
            else if (tmp >= 2)
                tmp = 2;
            else if (tmp >= 1)
                tmp = 1;
            if (digits >= 6)
            {
                unit = "m";
                textLength = tmp * (int)Math.Pow(10, digits - 6);
                unitC = (int)Math.Pow(10, 6);
            }
            else if (digits >= 3)
            {
                unit = "mm";
                textLength = tmp * (int)Math.Pow(10, digits - 3);
                unitC = (int)Math.Pow(10, 3);
            }
            else
            {
                unit = "µm";
                textLength = tmp * (int)Math.Pow(10, digits);
            }

            _lineLength = Coordinate.GetScreenRectFromPhysical(new Rect(
                Coordinate.DisplayPhysicalArea.X, Coordinate.DisplayPhysicalArea.Y, textLength * unitC / XPhysicalRatio, 0)).Width;
            _format = GetFormattedText(textLength.ToString() + " " + unit);
            //_fontHeight = _format.Height;
            //_fontWidth = _format.Width;

        }

        private void CalculateDisplaySize()
        {
            _displayWidth = Math.Max(_lineLength, _format.Width) + xPadding * 2;
            _displayHeight = _format.Height + 10 + yPadding * 2;
        }

        private FormattedText GetFormattedText(string text)
        {

            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            return new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface,
                FontSize,
                new SolidColorBrush(FontColor), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        public override int GetHitTestResult(Point pointInPhysical)
        {
            if (PhysicalRectG?.FillContains(pointInPhysical) == true)
                return -1;
            return -2;
        }


        protected override void SetContextBinding()
        {
            base.SetContextBinding();

            SetBinding(FontFamilyProperty);
            SetBinding(FontSizeProperty);
            SetBinding(FontColorProperty);
            SetBinding(LineWidthProperty);
            SetBinding(LineColorProperty);
            SetBinding(PanelOpacityProperty);
            SetBinding(PanelColorProperty);
            SetBinding(ScalerPlacementProperty);
            SetBinding(XPhysicalRatioProperty);
            SetBinding(YPhysicalRatioProperty);
        }

        public override Cursor GetHandlerCursor(int handlerId)
        {
            if (handlerId == -1)
                return Cursors.Hand;
            return base.GetHandlerCursor(handlerId);
        }
    }
}
