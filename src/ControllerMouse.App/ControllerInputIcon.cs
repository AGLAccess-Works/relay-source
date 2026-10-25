using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ControllerMouse.App;

/// <summary>Quiet input symbols; the containing control supplies the accessible name.</summary>
internal sealed class ControllerInputIcon : FrameworkElement
{
    private string _input = "A";
    private bool _adaptive;
    public string Input { get => _input; set { if (_input == value) return; _input = value; InvalidateVisual(); } }
    public bool Adaptive { get => _adaptive; set { if (_adaptive == value) return; _adaptive = value; InvalidateVisual(); } }

    public ControllerInputIcon()
    {
        Width = Height = 36; Focusable = false; IsHitTestVisible = false;
        SetResourceReference(TagProperty, "TextPrimaryBrush");
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TagProperty) InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double size = Math.Min(ActualWidth, ActualHeight); if (size <= 0) return;
        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        Brush ink = TryFindResource("TextPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
        string code = _input switch
        {
            "LeftShoulder" => "LB",
            "RightShoulder" => "RB",
            "LeftTrigger" => "LT",
            "RightTrigger" => "RT",
            "LeftStick" => "L3",
            "RightStick" => "R3",
            "LeftAxis" => "L",
            "RightAxis" => "R",
            "Back" => "▣",
            "Start" => "≡",
            "Up" or "Down" or "Left" or "Right" => "+",
            "USBLeft" or "USBRight" => "USB",
            "Xbox" => "×",
            "Profile" => "●",
            _ => _input
        };
        bool direction = _input is "Up" or "Down" or "Left" or "Right";
        if (!direction) dc.DrawEllipse(null, new Pen(ink, Math.Max(1.2, size / 40)), centre, size / 2 - 2, size / 2 - 2);
        double textSize = size * (direction ? .85 : code.Length > 2 ? .27 : code.Length > 1 ? .35 : .55);
        var formatted = new FormattedText(code, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(VisualAccessTheme.InterfaceFont, FontStyles.Normal, direction ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            Math.Max(14, textSize), ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(centre.X - formatted.Width / 2, centre.Y - formatted.Height / 2));
    }
}
