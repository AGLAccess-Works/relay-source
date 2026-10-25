using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerMouse.App;

internal enum ControllerPartShape { Face, LargePad, Shoulder, Trigger, Stick, StickClick, DpadUp, DpadDown, DpadLeft, DpadRight, Xbox, View, Menu, Profile, Port, Usb }

/// <summary>An actual Button that renders its own vector component and keeps focus separate from selection.</summary>
public sealed class ControllerPartButton : Button
{
    private readonly ControllerSurface _surface;
    private readonly ControllerPartShape _shape;
    public string Id { get; }
    public string Code { get; }
    public bool HardwareActive { get; private set; }
    public bool ConfigurationSelected => _surface.SelectedInput == Id;
    public Vector AxisPosition { get; private set; }
    public double AnalogValue { get; private set; }
    public bool IsDpad => _shape is ControllerPartShape.DpadUp or ControllerPartShape.DpadDown or ControllerPartShape.DpadLeft or ControllerPartShape.DpadRight;
    private Point _artCenter;
    private double _artRadius;
    private Point? _dpadCenter, _excludedDpadCenter;
    private double _dpadRadius, _excludedDpadRadius;
    private bool _geometryReady;
    private InputPresentation? _presentation;

    internal ControllerPartButton(ControllerSurface surface, string id, string code, ControllerPartShape shape)
    {
        _surface = surface; Id = id; Code = code; _shape = shape;
        OverridesDefaultStyle = true; FocusVisualStyle = null;
        MinWidth = MinHeight = 44; Margin = new Thickness(0); Padding = new Thickness(0);
        Cursor = System.Windows.Input.Cursors.Hand;
        // A minimal template lets OnRender draw the real component instead of the global settings button chrome.
        var root = new FrameworkElementFactory(typeof(Border)); root.SetValue(IsHitTestVisibleProperty, false);
        Template = new ControlTemplate(typeof(Button)) { VisualTree = root };
        SetResourceReference(BackgroundProperty, "ControlBrush"); SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        SetResourceReference(BorderBrushProperty, "BorderStrongBrush");
        AutomationProperties.SetAutomationId(this, "ControllerPart_" + id);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Off);
        GotKeyboardFocus += (_, _) => { InvalidateVisual(); BringIntoView(); };
        LostKeyboardFocus += (_, _) => InvalidateVisual();
        MouseEnter += (_, _) => InvalidateVisual(); MouseLeave += (_, _) => InvalidateVisual();
        Refresh();
    }

    internal void Refresh()
    {
        _presentation = _surface.Presentation(Id);
        HardwareActive = _surface.HardwareActive(Id);
        var pad = _surface.State;
        AxisPosition = !_surface.Connected ? default : Id == "LeftAxis"
            ? new Vector(Normalize(pad.LeftX), Normalize(pad.LeftY)) : Id == "RightAxis"
            ? new Vector(Normalize(pad.RightX), Normalize(pad.RightY)) : default;
        AnalogValue = !_surface.Connected ? 0 : Id == "LeftTrigger" ? pad.LeftTrigger / 255d : Id == "RightTrigger" ? pad.RightTrigger / 255d : 0;
        string action = _presentation?.Action ?? (Id is "Xbox" or "Profile" ? "Not available to Relay" : "No assignment");
        string name = _surface.InputName(Id) + ", " + action;
        if (AutomationProperties.GetName(this) != name) AutomationProperties.SetName(this, name);
        bool port = _shape is ControllerPartShape.Port or ControllerPartShape.Usb;
        string state = !_surface.Connected ? "Controller not connected" : port
            ? HardwareActive ? "Matching logical input active. Accessory connection not reported" : "Logical input inactive. Accessory connection not reported"
            : _presentation?.State ?? (HardwareActive ? "Input active" : "Ready");
        AutomationProperties.SetItemStatus(this, (ConfigurationSelected ? "Selected. " : "") + state);
        string help = "Select to inspect this input. This does not perform its assigned action.";
        if (port)
            help += " Any accessory name belongs to your saved setup. Physical accessory connection is not reported. Activity is the logical Xbox input received.";
        if (IsDpad) help += " This is the " + Code.ToLowerInvariant() + " direction of the D-pad. Full rectangular direction buttons are also available in the Hardware input list.";
        if (Id == "Xbox") help += " The Xbox button is reserved by Windows and is not reported through XInput.";
        if (Id == "Profile") help += " The physical profile button is managed by Xbox Accessories and is not reported to Relay.";
        if (_presentation?.Repeat == true) help += " The assigned action can repeat while held.";
        if (!string.IsNullOrWhiteSpace(_presentation?.AccessoryName)) help += " Saved accessory: " + _presentation.AccessoryName + ".";
        AutomationProperties.SetHelpText(this, help);
        ToolTip = name + "\n" + state + "\n" + help;
        InvalidateVisual();
    }

    private static double Normalize(short value) => value / (value < 0 ? 32768d : 32767d);
    internal void SetArtworkGeometry(Point center, double radius, Point? dpad, double dpadRadius, Point? excluded, double excludedRadius)
    {
        bool changed = !_geometryReady || _artCenter != center || _artRadius != radius || _dpadCenter != dpad || _dpadRadius != dpadRadius || _excludedDpadCenter != excluded || _excludedDpadRadius != excludedRadius;
        _geometryReady = true; _artCenter = center; _artRadius = radius; _dpadCenter = dpad; _dpadRadius = dpadRadius; _excludedDpadCenter = excluded; _excludedDpadRadius = excludedRadius;
        if (changed) InvalidateVisual();
    }
    public bool ContainsHitPoint(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight) return false;
        if (_dpadCenter is Point dpad)
        {
            // Each direction has a different local origin. Remove sub-pixel floating
            // transform noise before assigning their shared diagonal boundaries.
            var p = new Vector(Math.Round(point.X - dpad.X, 6), Math.Round(point.Y - dpad.Y, 6));
            if (p.LengthSquared > _dpadRadius * _dpadRadius) return false;
            // Inclusive boundaries belong to vertical directions; horizontal directions
            // use strict comparison. Exactly one control owns every shared wedge boundary.
            return _shape switch
            {
                ControllerPartShape.DpadUp => p.Y < 0 && Math.Abs(p.Y) >= Math.Abs(p.X),
                ControllerPartShape.DpadDown => p.Y >= 0 && Math.Abs(p.Y) >= Math.Abs(p.X),
                ControllerPartShape.DpadLeft => p.X < 0 && Math.Abs(p.X) > Math.Abs(p.Y),
                ControllerPartShape.DpadRight => p.X >= 0 && Math.Abs(p.X) > Math.Abs(p.Y),
                _ => false
            };
        }
        // Where a system-button target adjoins the compact D-pad, its enlarged target
        // yields to the directional disk. The physical button's centre stays selectable.
        if (_excludedDpadCenter is Point excluded && (point - excluded).LengthSquared <= _excludedDpadRadius * _excludedDpadRadius) return false;
        return true;
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters) => ContainsHitPoint(parameters.HitPoint) ? new PointHitTestResult(this, parameters.HitPoint) : null;
    private Brush B(string token, Brush fallback) => _surface.Brush(token, fallback);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        Brush edge = B("BorderStrongBrush", SystemColors.ControlTextBrush);
        Brush fill = B("SurfaceStrongBrush", SystemColors.ControlBrush);
        Brush ink = B("TextPrimaryBrush", SystemColors.ControlTextBrush);
        Brush cap = B("ControllerCapBrush", SystemColors.ControlBrush);
        Brush capInk = B("ControllerCapTextBrush", SystemColors.ControlTextBrush);
        double depth = HardwareActive && !_surface.MotionReduced && _shape is not (ControllerPartShape.Stick or ControllerPartShape.Port) ? 2 : 0;
        var pen = new Pen(edge, 1.8) { DashStyle = _surface.Connected ? DashStyles.Solid : DashStyles.Dash };
        var rect = new Rect(6, 6 + depth, Math.Max(1, width - 12), Math.Max(1, height - 12 - depth));
        var center = _geometryReady ? _artCenter : new Point(width / 2, height / 2);
        double radius = _artRadius > 0 ? _artRadius : Math.Max(8, Math.Min(rect.Width, rect.Height) / 2);
        bool artwork = _surface.ArtworkVisible;
        if (IsDpad)
        {
            DrawDpadState(dc, center, radius, !artwork); return;
        }
        if (_shape == ControllerPartShape.Stick) DrawStick(dc, center, radius, cap, capInk, pen, artwork);
        else if (_shape == ControllerPartShape.Port) DrawPort(dc, pen, ink);
        else if (_shape == ControllerPartShape.Usb) DrawUsb(dc, pen, ink, fill);
        else if (_shape == ControllerPartShape.Trigger) DrawTrigger(dc, pen, fill, ink);
        else if (_shape == ControllerPartShape.StickClick)
        {
            var pill = new Rect(4, height / 2 - 13, width - 8, 26);
            dc.DrawRoundedRectangle(artwork ? new LinearGradientBrush(Color.FromRgb(247, 249, 252), Color.FromRgb(212, 220, 231), 90) : fill, pen, pill, 13, 13);
            Text(dc, Code, new Point(width / 2, height / 2), 14, artwork ? Brushes.Black : ink);
        }
        else if (_shape == ControllerPartShape.Shoulder)
        {
            if (!artwork) dc.DrawRoundedRectangle(fill, pen, rect, 7, 7);
            Text(dc, Code, center, 14, _surface.Contrast ? ink : Application.Current is App { LightTheme: false } ? Brushes.White : Brushes.Black);
        }
        else if (!artwork)
        {
            dc.DrawEllipse(cap, pen, center, radius, radius);
            if (_shape == ControllerPartShape.Xbox) DrawXbox(dc, center, radius * .65, capInk);
            else if (_shape == ControllerPartShape.Menu) DrawMenu(dc, center, 9, capInk);
            else if (_shape == ControllerPartShape.View) DrawView(dc, center, capInk);
            else if (_shape == ControllerPartShape.Profile) DrawProfile(dc, center, capInk);
            else Text(dc, Code, center, _shape == ControllerPartShape.LargePad ? Math.Max(28, radius * .5) : 20, capInk);
        }
        else if (_shape == ControllerPartShape.LargePad) Text(dc, Code, center, Math.Max(28, radius * .40), Brushes.White);
        DrawState(dc, rect, center, radius);
    }

    private void DrawState(DrawingContext dc, Rect rect, Point center, double radius)
    {
        bool round = _shape is ControllerPartShape.Face or ControllerPartShape.LargePad or ControllerPartShape.Xbox or ControllerPartShape.View or ControllerPartShape.Menu or ControllerPartShape.Profile or ControllerPartShape.Stick or ControllerPartShape.Port;
        if (HardwareActive)
        {
            if (!_surface.Contrast) Glow(dc, center, radius + 2, B("InputActiveRingBrush", SystemColors.HighlightBrush));
            var pen = new Pen(B("InputActiveRingBrush", SystemColors.HighlightBrush), 4);
            if (round) dc.DrawEllipse(null, pen, center, radius + 2, radius + 2);
            else dc.DrawRoundedRectangle(null, pen, rect, 5, 5);
            // A filled square conveys active input even when colours cannot be distinguished.
            dc.DrawRectangle(B("InputActiveRingBrush", SystemColors.HighlightBrush), null, new Rect(ActualWidth - 10, ActualHeight - 10, 7, 7));
        }
        if (ConfigurationSelected)
        {
            var boundary = new Pen(B("SelectedInnerBrush", SystemColors.WindowTextBrush), 6);
            var pen = new Pen(B("SelectedRingBrush", SystemColors.HighlightBrush), 2.5);
            if (round) { dc.DrawEllipse(null, boundary, center, radius + 5, radius + 5); dc.DrawEllipse(null, pen, center, radius + 5, radius + 5); }
            else { var selected = new Rect(3, 3, Math.Max(1, ActualWidth - 6), Math.Max(1, ActualHeight - 6)); dc.DrawRoundedRectangle(null, boundary, selected, 6, 6); dc.DrawRoundedRectangle(null, pen, selected, 6, 6); }
            var mark = new Point(Math.Max(11, center.X - radius * .75), Math.Max(11, center.Y - radius * .75));
            dc.DrawRoundedRectangle(B("SurfaceRaisedBrush", SystemColors.WindowBrush),
                new Pen(B("SelectedInnerBrush", SystemColors.WindowTextBrush), 1), new Rect(mark.X - 9, mark.Y - 9, 18, 18), 3, 3);
            Text(dc, "✓", mark, 14, B("TextPrimaryBrush", SystemColors.WindowTextBrush));
        }
        if (IsKeyboardFocused)
        {
            // Dedicated rectangular focus stays distinct from the selected and physically pressed rings.
            DrawFocus(dc, new Rect(2.5, 2.5, Math.Max(1, ActualWidth - 5), Math.Max(1, ActualHeight - 5)), 5);
        }
        else if (IsMouseOver)
            dc.DrawRoundedRectangle(null, new Pen(B("BorderStrongBrush", SystemColors.WindowTextBrush), 1),
                new Rect(2, 2, Math.Max(1, ActualWidth - 4), Math.Max(1, ActualHeight - 4)), 5, 5);
    }

    private void DrawStick(DrawingContext dc, Point center, double radius, Brush cap, Brush ink, Pen edge, bool artwork)
    {
        var position = AxisPosition.Length > 1 ? AxisPosition / AxisPosition.Length : AxisPosition;
        var capCenter = center + (_surface.MotionReduced ? default : new Vector(position.X * radius * .24, -position.Y * radius * .24));
        if (!artwork || position != default)
        {
            Brush socket = artwork ? new RadialGradientBrush(Color.FromRgb(39, 43, 48), Color.FromRgb(10, 12, 15)) : B("SurfaceStrongBrush", SystemColors.ControlBrush);
            dc.DrawEllipse(socket, edge, center, radius * .98, radius * .98);
            var capImage = artwork ? _surface.StickCap(Id) : null;
            if (capImage != null)
            {
                dc.PushClip(new EllipseGeometry(capCenter, radius * .76, radius * .76));
                dc.DrawImage(capImage, new Rect(capCenter.X - radius * .76, capCenter.Y - radius * .76, radius * 1.52, radius * 1.52)); dc.Pop();
                dc.DrawEllipse(null, new Pen(Brushes.Black, 1.2), capCenter, radius * .76, radius * .76);
            }
            else { dc.DrawEllipse(cap, edge, capCenter, radius * .72, radius * .72); dc.DrawEllipse(null, new Pen(ink, 1), capCenter, radius * .56, radius * .56); }
        }
        if (_surface.ShowDeadZones)
        {
            double zone = Id == "RightAxis" ? _surface.ScrollDeadZone : _surface.DeadZone;
            zone = double.IsFinite(zone) ? Math.Clamp(zone, 0, 1) : .18;
            dc.DrawEllipse(null, new Pen(B("BorderStrongBrush", SystemColors.ControlTextBrush), 1.5) { DashStyle = DashStyles.Dash },
                center, radius * .9 * zone, radius * .9 * zone);
        }
    }

    private void DrawTrigger(DrawingContext dc, Pen edge, Brush fill, Brush ink)
    {
        double w = ActualWidth, h = ActualHeight;
        var socket = new Rect(8, 7, w - 16, h - 30);
        dc.DrawRoundedRectangle(B("ControllerSocketBrush", SystemColors.ControlBrush), edge, socket, 8, 8);
        double travel = _surface.MotionReduced ? 0 : AnalogValue * Math.Min(9, h * .14);
        var body = new StreamGeometry();
        using (var c = body.Open())
        {
            c.BeginFigure(new Point(12, 7 + travel), true, true);
            c.LineTo(new Point(w - 12, 7 + travel), true, false);
            c.LineTo(new Point(w - 20, h - 29), true, false); c.LineTo(new Point(20, h - 29), true, false);
        }
        Brush plastic = _surface.Contrast ? fill : new LinearGradientBrush(Color.FromRgb(77, 81, 87), Color.FromRgb(16, 18, 22), 90);
        dc.DrawGeometry(plastic, edge, body);
        Text(dc, Code, new Point(w / 2, 19 + travel / 2), 14, _surface.Contrast ? ink : Brushes.White);
        Text(dc, Math.Round(AnalogValue * 100).ToString(CultureInfo.CurrentCulture) + "%", new Point(w / 2, h - 11), 14, ink);
    }

    private void DrawPort(DrawingContext dc, Pen edge, Brush ink)
    {
        var center = _geometryReady ? _artCenter : new Point(ActualWidth / 2, 22);
        var capInk = B("ControllerCapTextBrush", SystemColors.ControlTextBrush);
        Brush metal = _surface.Contrast ? B("ControllerCapBrush", SystemColors.ControlBrush) : new LinearGradientBrush(Color.FromRgb(249, 250, 252), Color.FromRgb(116, 126, 137), 45);
        double r = Math.Max(11, _artRadius);
        dc.DrawEllipse(metal, edge, center, r, r);
        dc.DrawEllipse(_surface.Contrast ? B("ControllerSocketBrush", SystemColors.WindowBrush) : Brushes.Black, new Pen(capInk, .8), center, r * .64, r * .64);
        if (HardwareActive) dc.DrawEllipse(null, new Pen(B("InputActiveRingBrush", SystemColors.HighlightBrush), 3), center, r + 2, r + 2);
        Text(dc, Code, new Point(ActualWidth / 2, ActualHeight - 30), 14, _surface.Contrast ? ink : B("ArtworkInkBrush", SystemColors.WindowTextBrush));
        if (!string.IsNullOrWhiteSpace(_presentation?.AccessoryName))
            Text(dc, "Saved", new Point(ActualWidth / 2, ActualHeight - 10), 14, _surface.Contrast ? ink : B("ArtworkInkBrush", SystemColors.WindowTextBrush));
    }

    private void DrawUsb(DrawingContext dc, Pen edge, Brush ink, Brush fill)
    {
        var r = new Rect(8, 5, ActualWidth - 16, 27);
        dc.DrawRoundedRectangle(fill, edge, r, 3, 3);
        dc.DrawRoundedRectangle(B("ControllerSocketBrush", SystemColors.ControlBrush), edge,
            new Rect(ActualWidth / 2 - 28, 11, 56, 15), 2, 2);
        Text(dc, Code, new Point(ActualWidth / 2, ActualHeight - 14), 14, ink);
    }

    private void Glow(DrawingContext dc, Point center, double radius, Brush colour)
    {
        foreach (var (width, opacity) in new[] { (14d, .08), (9d, .12), (6d, .16) })
        { var brush = colour.CloneCurrentValue(); brush.Opacity = opacity; dc.DrawEllipse(null, new Pen(brush, width), center, radius, radius); }
    }
    private void DrawDpadState(DrawingContext dc, Point center, double radius, bool fallback)
    {
        radius = Math.Max(24, radius); double q = radius * .30;
        var arm = _shape switch
        {
            ControllerPartShape.DpadUp => new Rect(center.X - q, center.Y - radius * .90, q * 2, radius * .64),
            ControllerPartShape.DpadDown => new Rect(center.X - q, center.Y + radius * .26, q * 2, radius * .64),
            ControllerPartShape.DpadLeft => new Rect(center.X - radius * .90, center.Y - q, radius * .64, q * 2),
            _ => new Rect(center.X + radius * .26, center.Y - q, radius * .64, q * 2)
        };
        if (fallback)
        {
            dc.DrawRoundedRectangle(B("ControllerCapBrush", SystemColors.ControlBrush), null, arm, 3, 3);
            DrawDirection(dc, new Point(arm.X + arm.Width / 2, arm.Y + arm.Height / 2), Math.Min(arm.Width, arm.Height) * .24, B("ControllerCapTextBrush", SystemColors.ControlTextBrush));
        }
        if (HardwareActive)
        {
            var active = B("InputActiveRingBrush", SystemColors.HighlightBrush);
            var fill = active.CloneCurrentValue(); fill.Opacity = _surface.Contrast ? 1 : .68;
            dc.DrawRoundedRectangle(fill, new Pen(active, 3), arm, 4, 4);
            dc.DrawRectangle(active, null, new Rect(arm.Right - 7, arm.Bottom - 7, 6, 6));
        }
        if (ConfigurationSelected)
        {
            var selected = B("SelectedRingBrush", SystemColors.HighlightBrush);
            var selection = new Rect(arm.X - 2, arm.Y - 2, arm.Width + 4, arm.Height + 4);
            dc.DrawRoundedRectangle(null, new Pen(B("SelectedInnerBrush", SystemColors.WindowTextBrush), 6), selection, 4, 4);
            dc.DrawRoundedRectangle(null, new Pen(selected, 2.5), selection, 4, 4);
            Text(dc, "✓", new Point(arm.X - 5, arm.Y - 5), 14, B("SelectedInnerBrush", SystemColors.WindowTextBrush));
        }
        if (IsKeyboardFocused)
        {
            // Directional focus follows this arm, rather than outlining the whole pad.
            DrawFocus(dc, arm, 4);
            DrawDirection(dc, new Point(arm.X + arm.Width / 2, arm.Y + arm.Height / 2), Math.Min(arm.Width, arm.Height) * .22, B("FocusInnerBrush", SystemColors.WindowTextBrush));
        }
        else if (IsMouseOver) dc.DrawRoundedRectangle(null, new Pen(B("BorderStrongBrush", SystemColors.WindowTextBrush), 1), arm, 4, 4);
    }

    private void DrawFocus(DrawingContext dc, Rect bounds, double radius)
    {
        double outer = TryFindResource("FocusStrokeWidth") is double size ? size : 3;
        dc.DrawRoundedRectangle(null, new Pen(B("FocusRingBrush", SystemColors.HighlightBrush), outer), bounds, radius, radius);
        double inset = outer / 2 + 1;
        var inner = new Rect(bounds.X + inset, bounds.Y + inset, Math.Max(1, bounds.Width - inset * 2), Math.Max(1, bounds.Height - inset * 2));
        dc.DrawRoundedRectangle(null, new Pen(B("FocusInnerBrush", SystemColors.WindowTextBrush), 2), inner, Math.Max(1, radius - 1), Math.Max(1, radius - 1));
    }

    private void DrawDirection(DrawingContext dc, Point p, double r, Brush ink)
    {
        var points = _shape switch
        {
            ControllerPartShape.DpadUp => new[] { new Point(p.X, p.Y - r), new Point(p.X - r, p.Y + r), new Point(p.X + r, p.Y + r) },
            ControllerPartShape.DpadDown => new[] { new Point(p.X, p.Y + r), new Point(p.X - r, p.Y - r), new Point(p.X + r, p.Y - r) },
            ControllerPartShape.DpadLeft => new[] { new Point(p.X - r, p.Y), new Point(p.X + r, p.Y - r), new Point(p.X + r, p.Y + r) },
            _ => new[] { new Point(p.X + r, p.Y), new Point(p.X - r, p.Y - r), new Point(p.X - r, p.Y + r) }
        };
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(points[0], true, true); c.LineTo(points[1], true, false); c.LineTo(points[2], true, false); }
        dc.DrawGeometry(ink, null, g);
    }
    private static void DrawXbox(DrawingContext dc, Point p, double r, Brush ink)
    {
        var pen = new Pen(ink, 2.5);
        dc.DrawLine(pen, new Point(p.X - r * .62, p.Y - r * .62), new Point(p.X + r * .62, p.Y + r * .62));
        dc.DrawLine(pen, new Point(p.X + r * .62, p.Y - r * .62), new Point(p.X - r * .62, p.Y + r * .62));
        dc.DrawEllipse(null, new Pen(ink, 1.3), p, r, r);
    }
    private static void DrawMenu(DrawingContext dc, Point p, double width, Brush ink)
    {
        var pen = new Pen(ink, 2);
        foreach (double y in new[] { -5d, 0, 5 }) dc.DrawLine(pen, new Point(p.X - width, p.Y + y), new Point(p.X + width, p.Y + y));
    }
    private static void DrawView(DrawingContext dc, Point p, Brush ink)
    {
        var pen = new Pen(ink, 1.6);
        dc.DrawRectangle(null, pen, new Rect(p.X - 9, p.Y - 7, 12, 10)); dc.DrawRectangle(null, pen, new Rect(p.X - 2, p.Y - 2, 12, 10));
    }
    private static void DrawProfile(DrawingContext dc, Point p, Brush ink)
    {
        var pen = new Pen(ink, 2);
        foreach (double y in new[] { -6d, 0, 6 }) dc.DrawLine(pen, new Point(p.X - 8, p.Y + y), new Point(p.X + 5, p.Y + y));
    }
    private void Text(DrawingContext dc, string value, Point center, double size, Brush ink)
    {
        var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(VisualAccessTheme.InterfaceFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            Math.Max(14, size), ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }
}
