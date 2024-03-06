using DrawingTool;
using DrawingTool.Tools;
using System;
using System.Windows;
using System.Windows.Input;

namespace FilterWheelShared.Controls.DrawingTools
{
    public class ToolRulerEx : ToolRuler
    {
        public decimal XPhysicalRatio { get; set; } = 1;
        public decimal YPhysicalRatio { get; set; } = 1;

        private Factory.Materials.CustomRulerViewModel _graphic;

        public override void KeyDown(object sender, KeyEventArgs e)
        {
            CanvasBase canvasBase = sender as CanvasBase;
            if (canvasBase != null)
            {
                if (e.Key == Key.Escape || e.Key == Key.Delete)
                {
                    RemoveROIHandler?.Invoke(sender, new ROIEventArgs
                    {
                        Graphic = _graphic
                    });
                    OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.remove));
                    canvasBase.ReleaseMouseCapture();
                    _graphic = null;
                }

                base.KeyDown(sender, e);
                return;
            }

            throw new ArgumentException("Sender must be typeof CanvasBase");
        }

        public override void MouseDown(object sender, MouseButtonEventArgs e)
        {
            CanvasBase canvasBase = sender as CanvasBase;
            if (canvasBase != null)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    Point physicalPointFromScreen = canvasBase.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvasBase));
                    if (canvasBase.Coordinate.PhysicalRect.Contains(physicalPointFromScreen))
                    {
                        _graphic = new Factory.Materials.CustomRulerViewModel
                        {
                            StartPoint = physicalPointFromScreen,
                            EndPoint = physicalPointFromScreen
                        };
                        AddROIHandler?.Invoke(sender, new ROIEventArgs
                        {
                            Graphic = _graphic
                        });
                        canvasBase.CaptureMouse();
                    }
                }

                base.MouseDown(sender, e);
                return;
            }

            throw new ArgumentException("Sender must be typeof CanvasBase");
        }

        public override void MouseMove(object sender, MouseEventArgs e)
        {
            CanvasBase canvasBase = sender as CanvasBase;
            if (canvasBase != null)
            {
                if (_graphic != null)
                {
                    Point physicalPointFromScreen = canvasBase.Coordinate.GetPhysicalPointFromScreen(e.GetPosition(canvasBase));
                    _graphic.EndPoint = canvasBase.Coordinate.CoercePointInPhysicalRect(physicalPointFromScreen);
                    base.MouseMove(sender, e);
                }

                return;
            }

            throw new ArgumentException("Sender must be typeof CanvasBase");
        }

        public override void MouseUp(object sender, MouseButtonEventArgs e)
        {
            CanvasBase canvasBase = sender as CanvasBase;
            if (canvasBase != null)
            {
                if (e.ChangedButton == MouseButton.Left && _graphic != null)
                {
                    canvasBase.ReleaseMouseCapture();
                    if (Islegal(_graphic, canvasBase))
                    {
                        OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.add));
                    }
                    else
                    {
                        RemoveROIHandler?.Invoke(sender, new ROIEventArgs
                        {
                            Graphic = _graphic
                        });
                        OperationFinishedCallbackHandler?.Invoke(sender, new ROIActionFinishEventArgs(_graphic, GraphicActions.illegal));
                    }

                    _graphic = null;
                }

                base.MouseUp(sender, e);
                return;
            }

            throw new ArgumentException("Sender must be typeof CanvasBase");
        }
    }

}
