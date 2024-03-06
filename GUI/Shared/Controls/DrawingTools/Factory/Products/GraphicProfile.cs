using DrawingTool;
using DrawingTool.Factory.Products;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FilterWheelShared.Controls.DrawingTools.Factory.Products
{
    public class GraphicProfile : GraphicROIBase
    {
        public GraphicProfile()
        {
            IsSelectable = false;
        }

        public bool IsMoving
        {
            get => (bool)GetValue(IsMovingProperty);
            set => SetValue(IsMovingProperty, value);
        }

        public static readonly DependencyProperty IsMovingProperty =
            DependencyProperty.Register("IsMoving", typeof(bool), typeof(GraphicProfile), new PropertyMetadata(false, IsMoveingChangedCallbck));

        private static void IsMoveingChangedCallbck(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as GraphicProfile;
            var s = obj.Stroke.Clone();
            if ((bool)e.NewValue)
            {
                s.Opacity = 0.5;
                obj.Stroke = s;
            }
            else
            {
                s.Opacity = 1;
                obj.Stroke = s;
            }
            obj.Stroke.Freeze();
            obj.Draw();
        }

        public static readonly DependencyProperty StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(GraphicProfile), new PropertyMetadata(default(Point), StartPointPropertyChangedCallback));

        public Point StartPoint
        {
            get => (Point)GetValue(StartPointProperty);
            set => SetValue(StartPointProperty, value);
        }

        private static void StartPointPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as GraphicProfile).Draw();
        }


        public static readonly DependencyProperty EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(GraphicProfile), new PropertyMetadata(default(Point), EndPointPropertyChangedCallback));

        public Point EndPoint
        {
            get => (Point)GetValue(EndPointProperty);
            set => SetValue(EndPointProperty, value);
        }

        private static void EndPointPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as GraphicProfile).Draw();
        }

        public Point StartPixelPoint
        {
            get { return (Point)GetValue(StartPixelPointProperty); }
            set { SetValue(StartPixelPointProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartPixelPoint.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartPixelPointProperty =
            DependencyProperty.Register("StartPixelPoint", typeof(Point), typeof(GraphicProfile), new PropertyMetadata(default(Point)));

        public Point EndPixelPoint
        {
            get { return (Point)GetValue(EndPixelPointProperty); }
            set { SetValue(EndPixelPointProperty, value); }
        }

        // Using a DependencyProperty as the backing store for EndPixelPoint.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EndPixelPointProperty =
            DependencyProperty.Register("EndPixelPoint", typeof(Point), typeof(GraphicProfile), new PropertyMetadata(default(Point)));



        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register("Width", typeof(double), typeof(GraphicProfile), new PropertyMetadata(1.0, WidthPropertyChangedCallback));

        private static void WidthPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as GraphicProfile).Draw();
        }

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        protected override IEnumerable<Point> GetHandlerPoints()
        {
            return new Point[] { StartPoint, new Point((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2), EndPoint };
        }

        protected override void DrawROI(DrawingContext drawingContext)
        {
            var startPoint = Coordinate.GetScreenPointFromPhysical(StartPoint);
            var endPoint = Coordinate.GetScreenPointFromPhysical(EndPoint);
            var graphicStroke = Coordinate.GetScreenRectFromPixel(new Rect(0, 0, Width, 1)).Width;

            if (graphicStroke < 1)
                graphicStroke = 1;

            var _pen = new Pen(DrawStroke, graphicStroke);
            _pen.Freeze();
            drawingContext.DrawLine(_pen, startPoint, endPoint);
            //For GraphicProfile , in any time, the handler should be drawn
            if (!IsSelected)
            {
                DrawHandler(drawingContext);
                IsSelected = true;
            }
        }
        public override Geometry GetClip()
        {
            var r1 = new RectangleGeometry(new Rect(Coordinate.SizeOnScreen));
            var r2 = new RectangleGeometry(Coordinate.GetScreenRectFromPhysical(Coordinate.PhysicalRect));
            return Geometry.Combine(r1, r2, GeometryCombineMode.Intersect, null);
        }
        public override bool InteractWith(Rect r)
        {
            if (r.Contains(StartPoint) || r.Contains(EndPoint))
                return true;
            return LineIntersectsRect(StartPoint, EndPoint, r);
        }

        private bool LineIntersectsLine(Point l1p1, Point l1p2, Point l2p1, Point l2p2)
        {
            double q = (l1p1.Y - l2p1.Y) * (l2p2.X - l2p1.X) - (l1p1.X - l2p1.X) * (l2p2.Y - l2p1.Y);
            double d = (l1p2.X - l1p1.X) * (l2p2.Y - l2p1.Y) - (l1p2.Y - l1p1.Y) * (l2p2.X - l2p1.X);

            if (d == 0)
            {
                return false;
            }

            double r = q / d;

            q = (l1p1.Y - l2p1.Y) * (l1p2.X - l1p1.X) - (l1p1.X - l2p1.X) * (l1p2.Y - l1p1.Y);
            double s = q / d;

            if (r < 0 || r > 1 || s < 0 || s > 1)
            {
                return false;
            }
            return true;
        }

        private bool LineIntersectsRect(Point p1, Point p2, Rect r)
        {
            return LineIntersectsLine(p1, p2, new Point(r.X, r.Y), new Point(r.X + r.Width, r.Y)) ||
                   LineIntersectsLine(p1, p2, new Point(r.X + r.Width, r.Y), new Point(r.X + r.Width, r.Y + r.Height)) ||
                   LineIntersectsLine(p1, p2, new Point(r.X + r.Width, r.Y + r.Height), new Point(r.X, r.Y + r.Height)) ||
                   LineIntersectsLine(p1, p2, new Point(r.X, r.Y + r.Height), new Point(r.X, r.Y)) ||
                   (r.Contains(p1) && r.Contains(p2));
        }


        protected override void DrawRender(DrawingContext drawingContext)
        {

        }

        public override int GetHitTestResult(Point pointInPhysical)
        {
            var pointOnScreen = Coordinate.GetScreenPointFromPhysical(pointInPhysical);
            var startPointOnScreen = Coordinate.GetScreenPointFromPhysical(StartPoint);
            var endPointOnScreen = Coordinate.GetScreenPointFromPhysical(EndPoint);
            var startHandlerRect = new Rect(startPointOnScreen.X - handlerThumbSize / 2, startPointOnScreen.Y - handlerThumbSize / 2, handlerThumbSize, handlerThumbSize);
            var endHandlerRect = new Rect(endPointOnScreen.X - handlerThumbSize / 2, endPointOnScreen.Y - handlerThumbSize / 2, handlerThumbSize, handlerThumbSize);
            var middlePoint = new Point((startPointOnScreen.X + endPointOnScreen.X) / 2, (startPointOnScreen.Y + endPointOnScreen.Y) / 2);
            var middleHandlerRect = new Rect(middlePoint.X - handlerThumbSize / 2, middlePoint.Y - handlerThumbSize / 2, handlerThumbSize, handlerThumbSize);
            if (startHandlerRect.Contains(pointOnScreen))
                return 0;
            if (endHandlerRect.Contains(pointOnScreen))
                return 1;
            if (middleHandlerRect.Contains(pointOnScreen))
                return -1;
            return -2;
        }

        public override Cursor GetHandlerCursor(int handlerId)
        {
            switch (handlerId)
            {
                case -1:
                case 0:
                case 1:
                    return Cursors.SizeAll;
                default:
                    return null;
            }
        }

        protected override void SetContextBinding()
        {
            base.SetContextBinding();
            SetBinding(StartPointProperty);
            SetBinding(EndPointProperty);
            SetBinding(WidthProperty);
            SetBinding(StartPixelPointProperty);
            SetBinding(EndPixelPointProperty);
            SetBinding(IsMovingProperty, bindingMode: BindingMode.Default);
        }

        protected override void DrawHandler(DrawingContext drawingContext)
        {
            var startPoint = Coordinate.GetScreenPointFromPhysical(StartPoint);
            var endPoint = Coordinate.GetScreenPointFromPhysical(EndPoint);
            var graphicStroke = Coordinate.GetScreenRectFromPixel(new Rect(0, 0, Width, 1)).Width * 1.75 * Math.Pow(0.95, Width);
            if (graphicStroke < 1.1)//if the size is too small, try to increase it.
                graphicStroke = 1.1;
            var _pen = new Pen(DrawStroke, graphicStroke);
            _pen.Freeze();
            drawingContext.DrawEllipse(DrawStroke, _pen, startPoint, graphicStroke, graphicStroke);

            graphicStroke = Math.Pow(1.05, Width) * graphicStroke;//need to become bigger to show

            var Anglemark = new PathGeometry();
            var figure = new PathFigure() { StartPoint = endPoint, IsClosed = true };

            var segment = new PolyLineSegment();
            segment.Points.Add(new Point(endPoint.X - graphicStroke * 2, endPoint.Y - graphicStroke * 1.5));
            segment.Points.Add(new Point(endPoint.X - graphicStroke, endPoint.Y));
            segment.Points.Add(new Point(endPoint.X - graphicStroke * 2, endPoint.Y + graphicStroke * 1.5));

            figure.Segments.Add(segment);
            Anglemark.Figures.Add(figure);

            var v = endPoint - startPoint;
            Anglemark.Transform = new RotateTransform(180 * Math.Atan2(v.Y, v.X) / Math.PI) { CenterX = endPoint.X, CenterY = endPoint.Y };
            Anglemark.Freeze();
            drawingContext.DrawGeometry(DrawStroke, _pen, Anglemark);
        }

        public override void MoveHandler(int handlerId, Vector vector)
        {
            switch (handlerId)
            {
                case -1:
                    var rect = Coordinate.CoerceRectInPhysicalArea(Coordinate.CoerceRectInDisplayArea(new Rect(ROIRect.TopLeft + vector, ROIRect.BottomRight + vector)));
                    vector = rect.TopLeft - ROIRect.TopLeft;
                    StartPoint = Coordinate.CoercePointInPhysicalRect(Coordinate.CoercePointInDisplayArea(StartPoint + vector));
                    StartPixelPoint = Coordinate.GetPixelPointFromPhysical(StartPoint);
                    EndPoint = Coordinate.CoercePointInPhysicalRect(Coordinate.CoercePointInDisplayArea(EndPoint + vector));
                    EndPixelPoint = Coordinate.GetPixelPointFromPhysical(EndPoint);
                    break;
                case 0:
                    StartPoint = Coordinate.CoercePointInPhysicalRect(Coordinate.CoercePointInDisplayArea(StartPoint + vector));
                    StartPixelPoint = Coordinate.GetPixelPointFromPhysical(StartPoint);
                    break;
                case 1:
                    EndPoint = Coordinate.CoercePointInPhysicalRect(Coordinate.CoercePointInDisplayArea(EndPoint + vector));
                    EndPixelPoint = Coordinate.GetPixelPointFromPhysical(EndPoint);
                    break;
                default:
                    break;
            }
        }
    }
}
