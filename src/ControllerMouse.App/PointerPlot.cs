using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public sealed class PointerPlot : FrameworkElement
{
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new DiagramAutomationPeer(this, "Illustration of the current pointer settings. Read and change the equivalent values with the labelled sliders following this diagram.");
    public string Mode { get; set; } = "DeadZone";
    private Settings _settings = new();
    private Gamepad _state;
    public Settings SettingsState { get => _settings; set { _settings = value; InvalidateVisual(); } }
    public Gamepad State { get => _state; set { _state = value; InvalidateVisual(); } }
    private Brush B(string key) => (Brush)FindResource(key);
    private readonly List<(string Text, Rect Bounds, double FontSize)> _renderedLabels = [];
    internal IReadOnlyList<(string Text, Rect Bounds, double FontSize)> RenderedLabels => _renderedLabels;
    private double LabelFontSize => (double)(TryFindResource("CaptionFontSize") ?? 14d);
    private FormattedText Label(string text) => new(text, CultureInfo.CurrentCulture, FlowDirection,
        new Typeface(VisualAccessTheme.InterfaceFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        LabelFontSize, B("MutedBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private void DrawLabel(DrawingContext dc, FormattedText label, double x, double y)
    {
        var origin = new Point(Math.Clamp(x, 0, Math.Max(0, ActualWidth - label.Width)),
            Math.Clamp(y, 0, Math.Max(0, ActualHeight - label.Height)));
        dc.DrawText(label, origin);
        _renderedLabels.Add((label.Text, new Rect(origin, new Size(label.Width, label.Height)), LabelFontSize));
    }
    protected override void OnRender(DrawingContext dc)
    {
        _renderedLabels.Clear();
        if (ActualWidth < 1 || ActualHeight < 1) return;
        // Lay out the drawing around full-size labels. Scaling a fixed bitmap-like
        // coordinate system also shrank the text, especially at 200% in a narrow page.
        var full = Label("100%");
        var half = Label("50%");
        var pen = new Pen(B("LineBrush"), 1);
        if (Mode == "DeadZone")
        {
            double radius = Math.Max(1, Math.Min((ActualWidth - 24) / 2, (ActualHeight - full.Height - 24) / 2));
            var center = new Point(ActualWidth / 2, full.Height + 12 + radius);
            dc.DrawEllipse(B("ControlBrush"), pen, center, radius, radius);
            var dotted = new Pen(B("MutedBrush"), 1) { DashStyle = DashStyles.Dot };
            dc.DrawEllipse(null, dotted, center, radius / 2, radius / 2);
            dc.DrawLine(dotted, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
            dc.DrawLine(dotted, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
            dc.PushOpacity(.18);
            dc.DrawEllipse(B("AccentBrush"), null, center, radius * _settings.DeadZone, radius * _settings.DeadZone);
            dc.Pop();
            dc.DrawEllipse(null, new Pen(B("AccentBrush"), 1.5), center, radius * _settings.DeadZone, radius * _settings.DeadZone);
            dc.DrawEllipse(B("InputBrush"), new Pen(B("PanelBrush"), 2),
                new Point(center.X + radius * _state.LeftX / 32768d, center.Y - radius * _state.LeftY / 32768d), 6, 6);
            DrawLabel(dc, full, center.X - full.Width / 2, 0);
            DrawLabel(dc, half, center.X + 6, center.Y - radius / 2 - half.Height);
        }
        else
        {
            var fast = Label("Fast"); var still = Label("Still"); var zero = Label("0%");
            double left = Math.Max(fast.Width, still.Width) + zero.Width / 2 + 12;
            double top = fast.Height / 2 + 4;
            double right = Math.Max(left + 1, ActualWidth - full.Width / 2 - 4);
            double bottom = Math.Max(top + 1, ActualHeight - full.Height - 14);
            double width = right - left, height = bottom - top;
            for (int i = 0; i < 3; i++)
                dc.DrawLine(pen, new Point(left, top + i * height / 2), new Point(right, top + i * height / 2));
            dc.PushOpacity(.12);
            dc.DrawRectangle(B("AccentBrush"), null, new Rect(left, top, width * _settings.DeadZone, height));
            dc.Pop();
            var curve = new StreamGeometry();
            using (var c = curve.Open())
            {
                c.BeginFigure(new Point(left, bottom), false, false);
                for (int i = 1; i <= 100; i++)
                {
                    double value = InputProcessor.Shape((short)(32767 * i / 100), 0, _settings.DeadZone, _settings.ResponseCurve).X;
                    c.LineTo(new Point(left + width * i / 100d, bottom - height * value), true, false);
                }
            }
            dc.DrawGeometry(null, new Pen(B("AccentBrush"), 2.5), curve);
            double mag = Math.Min(1, Math.Sqrt(Math.Pow(_state.LeftX / 32768d, 2) + Math.Pow(_state.LeftY / 32768d, 2)));
            double shaped = Math.Pow(Math.Max(0, (mag - _settings.DeadZone) / (1 - _settings.DeadZone)), _settings.ResponseCurve);
            dc.DrawEllipse(B("InputBrush"), new Pen(B("PanelBrush"), 2), new Point(left + width * mag, bottom - height * shaped), 6, 6);
            DrawLabel(dc, fast, 0, top - fast.Height / 2);
            DrawLabel(dc, still, 0, bottom - still.Height / 2);
            DrawLabel(dc, zero, left - zero.Width / 2, bottom + 10);
            DrawLabel(dc, full, right - full.Width / 2, bottom + 10);
            if (width >= zero.Width + half.Width + full.Width + 24)
                DrawLabel(dc, half, left + width / 2 - half.Width / 2, bottom + 10);
        }
    }
}
