using DrawingTool.Factory.Products;
using DrawingTool.Share;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FilterWheelShared.Common;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Products
{
    public class GraphicCustomRuler : GraphicRuler
    {
        public double XPhysicalRatio
        {
            get { return (double)GetValue(XPhysicalRatioProperty); }
            set { SetValue(XPhysicalRatioProperty, value); }
        }

        // Using a DependencyProperty as the backing store for XPhysicalRatio.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty XPhysicalRatioProperty =
            DependencyProperty.Register("XPhysicalRatio", typeof(double), typeof(GraphicRuler), new PropertyMetadata(1.0, PhysicalRelatedPropertyChangedCallback));


        public double YPhysicalRatio
        {
            get { return (double)GetValue(YPhysicalRatioProperty); }
            set { SetValue(YPhysicalRatioProperty, value); }
        }

        // Using a DependencyProperty as the backing store for YPhysicalRatio.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty YPhysicalRatioProperty =
            DependencyProperty.Register("YPhysicalRatio", typeof(double), typeof(GraphicRuler), new PropertyMetadata(1.0, PhysicalRelatedPropertyChangedCallback));

        private static void PhysicalRelatedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as GraphicRuler).Draw();
        }

        protected override void SetContextBinding()
        {
            base.SetContextBinding();
            SetBinding(XPhysicalRatioProperty);
            SetBinding(YPhysicalRatioProperty);
        }

        protected override void DrawRender(DrawingContext drawingContext)
        {
            var vecDiff = EndPoint - StartPoint;
            vecDiff.X *= XPhysicalRatio;
            vecDiff.Y *= YPhysicalRatio;
            var num = Math.Abs(vecDiff.Length);
            if (num < 1e-6) return;
            var digits = (int)Math.Floor(Math.Log10(num));
            var temp = digits / 3;
            var dstUnit = PhysicalUnit.um;
            if (temp < 3)
                dstUnit += temp;
            else
                dstUnit = PhysicalUnit.m;

            var multiplier = Math.Pow(1e3, dstUnit - PhysicalUnit.um);

            string description = dstUnit.DescriptionAttr();
            var dstSize = num / multiplier;
            FormattedText formattedText = new FormattedText(string.Format($"{StringFormat} {description}", dstSize) ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16.0, DrawStroke, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            Point pointInPhysical = new Point((StartPoint.X + EndPoint.X) / 2.0, (StartPoint.Y + EndPoint.Y) / 2.0);
            Point screenPointFromPhysical = base.Coordinate.GetScreenPointFromPhysical(pointInPhysical);
            Rect screenRectFromPhysical = base.Coordinate.GetScreenRectFromPhysical(base.Coordinate.PhysicalRect);
            if (screenPointFromPhysical.X + formattedText.Width > screenRectFromPhysical.Right)
            {
                screenPointFromPhysical.X -= formattedText.Width;
            }
            else
            {
                screenPointFromPhysical.X -= formattedText.Width / 2;
            }

            if (screenPointFromPhysical.Y + formattedText.Height > screenRectFromPhysical.Bottom)
            {
                screenPointFromPhysical.Y -= formattedText.Height;
            }

            drawingContext.DrawText(formattedText, screenPointFromPhysical);
        }

        protected override void DrawROI(DrawingContext drawingContext)
        {
            drawingContext.DrawGeometry(DrawFill, DrawPen, GetAcutalGeomety(StartPoint, EndPoint));
        }

        private Point CoerceScreenPoint(Point point)
        {
            var screenRect = base.Coordinate.GetScreenRectFromPhysical(base.Coordinate.PhysicalRect);
            return point.CoercePointInRect(screenRect);
        }

        private Geometry GetAcutalGeomety(Point p1, Point p2)
        {
            Point screenPointFromPhysical1 = base.Coordinate.GetScreenPointFromPhysical(p1);
            Point screenPointFromPhysical2 = base.Coordinate.GetScreenPointFromPhysical(p2);

            Vector v = new Vector(screenPointFromPhysical2.Y - screenPointFromPhysical1.Y, screenPointFromPhysical1.X - screenPointFromPhysical2.X);
            v.Normalize();
            v *= 5;
            Point p1v1 = CoerceScreenPoint(screenPointFromPhysical1 - v);
            Point p1v2 = CoerceScreenPoint(screenPointFromPhysical1 + v);

            Point p2v1 = CoerceScreenPoint(screenPointFromPhysical2 - v);
            Point p2v2 = CoerceScreenPoint(screenPointFromPhysical2 + v);

            StreamGeometry streamGeometry = new StreamGeometry();
            using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
            {
                streamGeometryContext.BeginFigure(screenPointFromPhysical1, isFilled: true, isClosed: false);
                streamGeometryContext.LineTo(screenPointFromPhysical2, isStroked: true, isSmoothJoin: false);

                streamGeometryContext.BeginFigure(p1v1, isFilled: true, isClosed: false);
                streamGeometryContext.LineTo(p1v2, isStroked: true, isSmoothJoin: false);

                streamGeometryContext.BeginFigure(p2v1, isFilled: true, isClosed: false);
                streamGeometryContext.LineTo(p2v2, isStroked: true, isSmoothJoin: false);
            }

            streamGeometry.Freeze();
            return streamGeometry;
        }
    }
}
