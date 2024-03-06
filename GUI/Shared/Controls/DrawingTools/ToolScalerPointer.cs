using DrawingTool;
using DrawingTool.Factory.Material;
using DrawingTool.Factory.Products;
using DrawingTool.Tools;
using DrawingTool.Tools.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FilterWheelShared.Controls.DrawingTools.Factory.Products;

namespace FilterWheelShared.Controls.DrawingTools
{
    public class ToolScalerPointer : ToolROIBase, IPixelInfoViewer, IROIContextMenu
    {
        public event EventHandler<DrawingCanvas> ScalerClicked;

        private GraphicSelection _graphic;
        private Point _point;
        private int _hitTestResult = -2;
        private Action<Point> ViewPixelAtPointHandler;
        private List<GraphicROIBase> _rois;
        private ToolTip toolTip = new ToolTip();
        private Brush sampleRectBrush;

        public event Action<Point> ViewPixelAtPoint
        {
            add { ViewPixelAtPointHandler += value; }
            remove { ViewPixelAtPointHandler -= value; }
        }
        public Func<GraphicBase, ContextMenu> GetGraphicContextMenu { get; set; }

        public ToolScalerPointer()
        {
            _descriptionToolTip = new ToolTip() { Content = "Pointer" };
            sampleRectBrush = new SolidColorBrush(Color.FromArgb(255, 173, 216, 230));
            sampleRectBrush.Freeze();
        }

        public ToolScalerPointer(Brush brush)
        {
            _descriptionToolTip = new ToolTip() { Content = "Pointer" };
            sampleRectBrush = brush;
            if (sampleRectBrush.CanFreeze)
                sampleRectBrush.Freeze();
        }
        public override Cursor GetCursor()
        {
            return Cursors.Arrow;
        }
        public override void KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (_rois == null)
                    _rois = canvas.GetAllSelectableROI().ToList();
                if (e.Key == Key.Delete)
                {
                    var deleteitems = _rois.Where(r => r.IsSelected).Select(roi => roi.DataContext).ToList();
                    foreach (var vm in deleteitems)
                        RemoveROIHandler?.Invoke(sender, new ROIEventArgs { Graphic = vm as GraphicViewModelBase });
                    canvas.RemoveVisual(_graphic);
                    _graphic = null;
                    canvas.ReleaseMouseCapture();
                }
                else if (e.Key == Key.Tab)
                {
                    if (_rois.Count(r => r.IsSelected) != 1)
                        return;
                    var roi = _rois.First(r => r.IsSelected);
                    roi.IsSelected = false;
                    var index = _rois.IndexOf(roi) + 1;
                    _rois[index % _rois.Count].IsSelected = true;
                }
                else if (e.Key == Key.Escape)
                {
                    canvas.RemoveVisual(_graphic);
                    _graphic = null;
                    canvas.ReleaseMouseCapture();
                }
                base.KeyDown(sender, e);
                if (e.Key == Key.Tab)
                {
                    e.Handled = true;
                }
                return;
            }
            throw new ArgumentException("sender must be DrawingCanvas type");
        }
        private void UnSelectAll()
        {
            if (_rois == null)
                return;
            foreach (var roi in _rois)
            {
                roi.IsSelected = false;
            }
        }

        private bool _isHitOnScaler;

        public override void MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 2)
                    {
                        canvas.Coordinate.AutoFit();
                        return;
                    }
                    _rois = canvas.GetAllSelectableROI().ToList();
                    var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                    _point = pointInPhysical;
                    _hitTestResult = -2;
                    var selecteitems = _rois.Where(r => r.IsSelected).ToList();
                    foreach (var roi in selecteitems)
                    {
                        var tHitTestResult = roi.GetHitTestResult(pointInPhysical);
                        if (tHitTestResult == -2)
                            continue;
                        _hitTestResult = tHitTestResult;
                        break;
                    }
                    //Then,check unselected rois
                    if (_hitTestResult == -2)
                    {
                        var unselecteitems = _rois.Where(r => !r.IsSelected).ToList();
                        foreach (var roi in unselecteitems)
                        {
                            var tHitTestResult = roi.GetHitTestResult(pointInPhysical);
                            if (tHitTestResult == -2)
                                continue;
                            _hitTestResult = tHitTestResult;
                            if (roi.IsSelected != true)
                            {
                                UnSelectAll();
                                roi.IsSelected = true;
                            }
                            break;
                        }
                    }
                    //Final,draw visual
                    if (_hitTestResult == -2)
                    {
                        UnSelectAll();
                        _graphic = new GraphicSelection(new Rect(_point, _point), sampleRectBrush) { Coordinate = canvas.Coordinate };
                        canvas.AddVisual(_graphic);
                        var scaler = canvas.GetAllROIs().ToList().FirstOrDefault(r => r is GraphicCustomScaler);
                        if (scaler != null)
                        {
                            var tHitTestResult = scaler.GetHitTestResult(pointInPhysical);
                            if (tHitTestResult == -1)
                            {
                                _isHitOnScaler = true;
                                canvas.CaptureMouse();
                                return;
                            }
                        }
                    }
                    canvas.CaptureMouse();
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    if (canvas.IsMouseCaptured)
                    {
                        return;
                    }
                    _rois = canvas.GetAllSelectableROI().ToList();
                    var roiList = _rois.Where(r => r.IsSelected).ToList();
                    if (roiList.Count == 0)
                        return;
                    var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                    int tHitTestResult = roiList[0].GetHitTestResult(pointInPhysical);
                    if (tHitTestResult != -2)
                    {
                        var menu = GetGraphicContextMenu?.Invoke(roiList[0]);
                        if (menu != null)
                            menu.IsOpen = true;
                        else
                            canvas.CaptureMouse();
                    }
                }
                else if (e.ChangedButton == MouseButton.Middle)
                {                    
                    if (e.ClickCount == 2)
                    {
                        canvas.Coordinate.AutoFit();
                    }
                    else
                    {
                        _point = e.GetPosition(canvas);
                        canvas.CaptureMouse();
                    }
                }
            }
            else
                throw new ArgumentException("sender must be DrawingCanvas type");
            base.MouseDown(sender, e);
        }
        private void RiseViewPixelAtPointEvent(CanvasBase canvasBase, MouseEventArgs e)
        {
            var physicalPoint = canvasBase.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvasBase));
            if (canvasBase.Coordinate.PhysicalRect.Contains(physicalPoint))
            {
                var pixelPoint = canvasBase.Coordinate.GetPixelPointFromPhysical(physicalPoint);
                if (pixelPoint.X < 0)
                    pixelPoint.X = 0;
                else if (pixelPoint.X > canvasBase.Coordinate.PixelSize.Width)
                    pixelPoint.X = canvasBase.Coordinate.PixelSize.Width;
                if (pixelPoint.Y < 0)
                    pixelPoint.Y = 0;
                else if (pixelPoint.Y > canvasBase.Coordinate.PixelSize.Height)
                    pixelPoint.Y = canvasBase.Coordinate.PixelSize.Height;
                ViewPixelAtPointHandler?.Invoke(pixelPoint);
            }
        }

        private IEnumerable<GraphicROIBase> GetValidROIs(DrawingCanvas canvas)
        {
            var rois = canvas.GetAllROIs().ToList();
            return rois.Where(r => r is GraphicCustomScaler || (r.IsSelectable && r.IsSelected)).ToList();
        }

        public override void MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                RiseViewPixelAtPointEvent(canvas, e);
                _rois = canvas.GetAllSelectableROI().ToList();
                if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released)
                {
                    var validRois = GetValidROIs(canvas).ToList();
                    var isMultiSelection = validRois.Where(r => r.IsSelected).Count() > 1;
                    var hitTestResult = -2;
                    GraphicROIBase hitROI = null;
                    var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                    var rois = validRois.Where(r => r.IsSelected).ToList();
                    foreach (var roi in rois)
                    {
                        var tHitTestResult = roi.GetHitTestResult(pointInPhysical);
                        if (!isMultiSelection && tHitTestResult == -2)
                            continue;
                        else if (isMultiSelection && tHitTestResult != -1)
                            continue;
                        hitTestResult = tHitTestResult;
                        hitROI = roi;
                        break;
                    }
                    //if hit test success
                    if (hitROI != null && hitROI.IsSelected)
                    {
                        canvas.Cursor = hitROI.GetHandlerCursor(hitTestResult);
                        if (!(hitROI is GraphicScaler) && !string.IsNullOrEmpty(hitROI.ToolTipString))
                        {
                            toolTip.Content = hitROI.ToolTipString;
                            toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                            toolTip.PlacementTarget = canvas;
                            toolTip.HorizontalOffset = e.GetPosition(canvas).X + 10;
                            toolTip.VerticalOffset = e.GetPosition(canvas).Y;
                            toolTip.IsOpen = true;
                        }
                    }
                    else
                    {
                        canvas.Cursor = null;

                        hitROI = null;
                        foreach (var roi in validRois)
                        {
                            if (roi.GetHitTestResult(pointInPhysical) != -2)
                            {
                                hitROI = roi;
                                break;
                            }
                        }
                        if (hitROI is GraphicCustomScaler)
                        {
                            canvas.Cursor = hitROI.GetHandlerCursor(-1);
                        }
                    }
                }
                if (e.LeftButton == MouseButtonState.Pressed && canvas.IsMouseCaptured)
                {
                    toolTip.IsOpen = false;
                    if (_graphic == null)
                    {
                        var selectedROIs = _rois.Where(r => r.IsSelected).ToArray();
                        var tPoint = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                        tPoint = canvas.Coordinate.CoercePointInPhysicalRect(tPoint);
                        var vector = tPoint - _point;
                        if (selectedROIs.Length > 1)
                        {
                            foreach (var roi in selectedROIs)
                            {
                                roi.StartResize();
                                roi.MoveHandler(-1, vector);
                            }
                        }
                        else if (selectedROIs.Length == 1 && _hitTestResult != -2)
                        {
                            canvas.Cursor = selectedROIs[0].GetHandlerCursor(_hitTestResult);
                            selectedROIs[0].StartResize();
                            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && _hitTestResult > -1 && _hitTestResult < 4)
                                selectedROIs[0].MoveHandler(_hitTestResult, tPoint);
                            else
                                selectedROIs[0].MoveHandler(_hitTestResult, vector);
                        }
                        _point = tPoint;
                    }
                    else
                    {
                        var tPoint = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                        tPoint = canvas.Coordinate.CoercePointInDisplayArea(tPoint);
                        _graphic.Rect = new Rect(_point, tPoint);
                        foreach (var r in _rois.Where(roi => roi.InteractWith(_graphic.Rect)))
                            r.IsSelected = true;
                        foreach (var r in _rois.Where(roi => !roi.InteractWith(_graphic.Rect)))
                            r.IsSelected = false;
                    }
                }
                if (e.MiddleButton == MouseButtonState.Pressed && canvas.IsMouseCaptured)
                {
                    var tPoint = e.GetPosition(canvas);
                    var v = _point - tPoint;
                    canvas.Coordinate?.MoveInScreen(v);
                    _point = tPoint;
                }
            }
            else
                throw new ArgumentException("sender must be DrawingCanvas type");
            base.MouseMove(sender, e);
        }
        public override void MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (canvas.IsMouseCaptured)
                {
                    canvas.ReleaseMouseCapture();
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        if (_graphic != null)
                        {
                            canvas.RemoveVisual(_graphic);
                            _graphic = null;
                        }
                        else
                        {
                            var rois = _rois.Where(r => r.IsSelected).ToList();
                            foreach (var roi in rois)
                                roi.EndResize();
                        }

                        var scaler = canvas.GetAllROIs().ToList().FirstOrDefault(r => r is GraphicCustomScaler);
                        if (scaler != null && _isHitOnScaler)
                        {
                            var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvas));
                            var tHitTestResult = scaler.GetHitTestResult(pointInPhysical);
                            if (tHitTestResult == -1)
                            {
                                ScalerClicked?.Invoke(this, canvas);
                            }
                        }
                        _isHitOnScaler = false;
                    }
                }
            }
            else
                throw new ArgumentException("sender must be DrawingCanvas type");
            base.MouseUp(sender, e);
        }

        public override void LostedFocus(object sender, RoutedEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (_graphic != null)
                {
                    canvas.RemoveVisual(_graphic);
                    _graphic = null;
                }
            }
            base.LostedFocus(sender, e);
        }
        public override void MouseLeave(object sender, MouseEventArgs e)
        {
            toolTip.IsOpen = false;
            base.MouseLeave(sender, e);
        }
        public override void MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (toolTip.IsOpen)
                    toolTip.IsOpen = false;
                if (canvas.IsMouseCaptured)
                    return;
                RiseViewPixelAtPointEvent(canvas, e);
            }
            else
                throw new ArgumentException("sender must be DrawingCanvas type");
            base.MouseWheel(sender, e);
        }

    }

}
