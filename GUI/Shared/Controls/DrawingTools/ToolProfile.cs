using DrawingTool;
using DrawingTool.Factory.Material;
using DrawingTool.Tools;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FilterWheelShared.Controls.DrawingTools.Factory.Materials;
using FilterWheelShared.Controls.DrawingTools.Factory.Products;

namespace FilterWheelShared.Controls.DrawingTools
{
    public class ToolProfile : ToolROIBase
    {
        private readonly Cursor _cursor = Cursors.Hand;
        public ToolProfile()
        {
            MemoryStream stream = new MemoryStream(Properties.Resources.CurProfile);
            _cursor = new Cursor(stream);
            _descriptionToolTip = new System.Windows.Controls.ToolTip() { Content = "Profile" };
        }

        public override Cursor GetCursor()
        {
            return _cursor;
        }

        private CustomProfileViewModel _graphic;
        private double _width;
        private int _handler;
        private Point _startPoint;

        public override void KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is CanvasBase canvasBase)
            {
                if (e.Key == Key.Escape || e.Key == Key.Delete)
                {
                    if (_graphic != null)
                    {
                        RemoveROIHandler?.Invoke(sender, new ROIEventArgs() { Graphic = _graphic });
                        OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.remove));
                    }
                    Clear();
                    canvasBase.ReleaseMouseCapture();
                }
            }
            else
                throw new ArgumentException("Sender must be typeof CanvasBase");
            e.Handled = IsHandleKeyDownEvents;
        }

        public override void MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    var position = e.GetPosition(canvas);
                    var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(position);
                    if (canvas.Coordinate.PhysicalRect.Contains(pointInPhysical))
                    {
                        _handler = -2;
                        if (_graphic?.IsSelected == true)
                        {
                            var profile = canvas.GetAllROIs().FirstOrDefault(g => g is GraphicProfile);
                            _handler = profile?.GetHitTestResult(pointInPhysical) ?? -2;
                        }
                        if (_handler == -2)
                        {
                            Clear();
                            if (_graphic == null)
                            {
                                _graphic = new CustomProfileViewModel
                                {
                                    Width = _width,
                                    IsSelected = true
                                };
                            }

                            _graphic.StartPoint = pointInPhysical;
                            _graphic.EndPoint = pointInPhysical;

                            var pointInPixel = canvas.Coordinate.GetPixelPointFromScreen(position);
                            _graphic.StartPixelPoint = pointInPixel;
                            _graphic.EndPixelPoint = pointInPixel;

                            AddROIHandler?.Invoke(sender, new ROIEventArgs { Graphic = _graphic });
                        }
                        else
                        {
                            _startPoint = pointInPhysical;
                        }
                        _graphic.IsMoving = true;
                        canvas.CaptureMouse();
                    }
                }
            }
            else
                throw new ArgumentException("Sender must be typeof CanvasBase");
            e.Handled = IsHandleMouseDownEvents;
        }

        public override void MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawingCanvas canvas)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var position = e.GetPosition(canvas);
                    var pointInPhysical = canvas.Coordinate.GetPhysicalPointFromScreen(position);
                    var profile = canvas.GetAllROIs().FirstOrDefault(g => g is GraphicProfile);
                    if (profile != null && _graphic?.IsSelected == true && e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
                    {
                        var handler = profile?.GetHitTestResult(pointInPhysical) ?? -2;
                        canvas.Cursor = profile.GetHandlerCursor(handler) ?? _cursor;
                    }
                    if (profile == null || _graphic == null || e.LeftButton == MouseButtonState.Released || !canvas.IsMouseCaptured)
                        return;
                    var point = canvas.Coordinate.CoercePointInPhysicalRect(pointInPhysical);
                    switch (_handler)
                    {
                        case -2:
                            _graphic.EndPoint = point;
                            var pointInPixel = canvas.Coordinate.GetPixelPointFromPhysical(point);
                            _graphic.EndPixelPoint = pointInPixel;
                            break;
                        default:
                            profile.MoveHandler(_handler, point - _startPoint);
                            _startPoint = point;
                            break;
                    }
                }
            }
            else
                throw new ArgumentException("Sender must be typeof CanvasBase");

            e.Handled = IsHandleMouseMoveEvents;
        }

        public override void MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is CanvasBase canvasBase)
            {
                if (e.ChangedButton == MouseButton.Left && _graphic != null)
                {
                    canvasBase.ReleaseMouseCapture();
                    Point? pixelStart = _graphic.StartPoint;
                    Point? pixelEnd = _graphic.EndPoint;
                    _graphic.IsMoving = false;
                    if (pixelStart == pixelEnd)
                    {
                        pixelStart = pixelEnd = null;
                        RemoveROIHandler?.Invoke(this, new ROIEventArgs() { Graphic = _graphic });
                        _graphic = null;
                    }
                    _handler = -2;
                    var islegal = Islegal(_graphic, canvasBase);
                    if (islegal)
                    {
                        OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.add));
                    }
                    else
                    {
                        RemoveROIHandler?.Invoke(sender, new ROIEventArgs() { Graphic = _graphic });
                        OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.illegal));
                    }
                }
                e.Handled = true;
            }
            else
                throw new ArgumentException("Sender must be typeof CanvasBase");

            e.Handled = IsHandleMouseUpEvents;
        }

        public void SetWidth(double width)
        {
            if (_width == width) return;
            _width = width;
            if (_graphic != null && _graphic.StartPoint != _graphic.EndPoint)
            {
                _graphic.Width = width;
            }
        }

        public void Clear()
        {
            if (_graphic != null)
            {
                RemoveROIHandler?.Invoke(this, new ROIEventArgs { Graphic = _graphic });
                _graphic = null;
                _handler = -2;
            }
        }

        protected override bool Islegal(GraphicViewModelBase viewModelBase, CanvasBase container)
        {
            return _graphic != null && _graphic.StartPoint != _graphic.EndPoint;
        }
    }
}
