using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ControllerMouse.App;

internal sealed class TargetOverlay : Window
{
    public TargetOverlay()
    {
        Width = Height = 30; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        IsHitTestVisible = false; Focusable = false;
        var rings = new Grid { IsHitTestVisible = false };
        var highlight = new Ellipse { Margin = new Thickness(3), StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false };
        highlight.SetResourceReference(Shape.StrokeProperty, "FocusRingBrush");
        highlight.SetResourceReference(Shape.StrokeThicknessProperty, "FocusStrokeWidth");
        var keyline = new Ellipse { Margin = new Thickness(8), StrokeThickness = 2, IsHitTestVisible = false };
        keyline.SetResourceReference(Shape.StrokeProperty, "FocusInnerBrush");
        rings.Children.Add(highlight); rings.Children.Add(keyline); Content = rings;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            long style = NativeMethods.GetWindowLongPtr(handle, -20).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, -20, new IntPtr(style | 0x20 | 0x08000000 | 0x80));
        };
    }
    public void Update(PredictionSnapshot? prediction, bool show)
    {
        if (!show || prediction == null) { Hide(); return; }
        if (!IsVisible) Show();
        // UI Automation gives physical pixels; position natively to support mixed-DPI monitors.
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1),
            (int)prediction.Candidate.Aim.X - 15, (int)prediction.Candidate.Aim.Y - 15, 30, 30, 0x0010);
    }
}
