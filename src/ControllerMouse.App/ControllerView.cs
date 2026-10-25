using System.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public sealed class ControllerView : FrameworkElement
{
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new DiagramAutomationPeer(this, "Live controller illustration. Current assignments are listed in text beside this diagram; connection and input status appear in the Control page.");
    private static readonly Geometry Body = Geometry.Parse("M177,151 C191,106 233,104 292,117 C358,130 416,130 476,118 C536,104 580,109 598,155 C621,213 638,291 645,355 C651,412 632,443 604,443 C571,443 553,416 535,389 C511,355 490,349 460,348 L313,348 C280,348 256,355 235,388 C215,419 199,443 169,443 C140,443 124,416 129,368 C134,311 151,219 177,151 Z");
    private Gamepad _state;
    private double _lx, _ly, _rx, _ry, _last;
    private bool _connected, _paused, _adaptive;
    private AdaptiveAccessory[] _accessories = [];
    public AdaptiveAccessory[] Accessories { get => _accessories; set { if (ReferenceEquals(_accessories, value)) return; _accessories = value; InvalidateVisual(); } }
    public bool Adaptive { get => _adaptive; set { if (_adaptive == value) return; _adaptive = value; InvalidateVisual(); } }
    public string CalloutCode { get; set; } = "";
    private string _callout = "";
    public Gamepad State { get => _state; set { _state = value; InvalidateVisual(); } }
    public bool Connected { get => _connected; set { _connected = value; InvalidateVisual(); } }
    public bool Paused { get => _paused; set { _paused = value; InvalidateVisual(); } }
    public string Callout { get => _callout; set { _callout = value; InvalidateVisual(); } }
    private bool Light => Application.Current.Resources["BackgroundBrush"] is SolidColorBrush b && b.Color.R > 150;
    private string Ink => Light ? "#4C535E" : "#C3C8D0";
    private string Edge => Light ? "#A5ADB9" : "#59616D";
    private string Input => _paused ? Ink : Light ? "#A55D00" : "#FFB443";
    public ControllerView()
    {
        Focusable = false;
        Loaded += (_, _) => { _last = 0; CompositionTarget.Rendering += Animate; };
        Unloaded += (_, _) => CompositionTarget.Rendering -= Animate;
    }
    private void Animate(object? sender, EventArgs args)
    {
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        double dt = _last == 0 ? .016 : Math.Clamp(now - _last, 0, .05); _last = now;
        double blend = SystemParameters.ClientAreaAnimation ? 1 - Math.Exp(-dt / .035) : 1;
        double lx = _state.LeftX / 32768d, ly = _state.LeftY / 32768d, rx = _state.RightX / 32768d, ry = _state.RightY / 32768d;
        if (Math.Abs(lx - _lx) + Math.Abs(ly - _ly) + Math.Abs(rx - _rx) + Math.Abs(ry - _ry) < .001) return;
        _lx += (lx - _lx) * blend; _ly += (ly - _ly) * blend; _rx += (rx - _rx) * blend; _ry += (ry - _ry) * blend;
        InvalidateVisual();
    }
    private static SolidColorBrush B(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static LinearGradientBrush G(string top, string bottom) => new((Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), 90);
    private Pen Outline(string? color = null, double width = 1)
    {
        var pen = new Pen(B(color ?? Edge), width);
        if (_paused && color == Input) pen.DashStyle = DashStyles.Dash;
        return pen;
    }
    private void Text(DrawingContext dc, string text, double x, double y, double size, string? color = null)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            size, ((App)Application.Current).ContrastActive ? SystemColors.WindowTextBrush : B(color ?? Ink), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = Math.Min(ActualWidth / 800, ActualHeight / 500);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - 800 * scale) / 2, (ActualHeight - 500 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (Adaptive) { DrawAdaptive(dc); dc.Pop(); dc.Pop(); return; }
        if (((App)Application.Current).ContrastActive)
        {
            DrawContrast(dc); dc.Pop(); dc.Pop(); return;
        }
        if (_connected)
        {
            var shadow = new RadialGradientBrush();
            shadow.GradientStops.Add(new GradientStop(Color.FromArgb(85, 0, 0, 0), 0));
            shadow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            dc.DrawEllipse(shadow, null, new Point(389, 453), 250, 24);
        }
        dc.DrawGeometry(_connected ? B(Light ? "#CCD1D8" : "#282C32") : null, Outline(), Geometry.Parse("M204,113 C214,77 246,73 272,91 L274,118 Z M494,116 L496,92 C524,72 560,79 570,115 Z"));
        Shoulder(dc, "M179,150 C187,111 227,96 307,120 L307,135 Z", Buttons.LeftShoulder);
        Shoulder(dc, "M465,135 L465,120 C540,97 582,111 596,150 Z", Buttons.RightShoulder);
        dc.DrawGeometry(_connected ? G(Light ? "#FFFFFF" : "#454842", Light ? "#E5E5DD" : "#30332E") : B(Light ? "#E8E8E1" : "#30332E"), Outline(), Body);
        dc.DrawGeometry(null, Outline(Light ? "#CCD1D8" : "#4E555F"), Geometry.Parse("M183,160 C216,107 258,126 304,131 C359,140 417,140 472,132 C528,123 565,111 592,158 M244,395 C268,363 290,358 323,357 L454,357 C490,357 517,369 531,396"));
        Trigger(dc, 146, _state.LeftTrigger, "LT");
        Trigger(dc, 625, _state.RightTrigger, "RT");
        Stick(dc, 270, 205, _lx, _ly, _state.Buttons.HasFlag(Buttons.LeftStick));
        Stick(dc, 452, 287, _rx, _ry, _state.Buttons.HasFlag(Buttons.RightStick));
        dc.DrawEllipse(_connected ? B(Light ? "#E0E4EA" : "#24282E") : null, null, new Point(321, 286), 38, 38);
        dc.DrawGeometry(_connected ? B(Light ? "#F6F7FA" : "#292E36") : null, Outline(), Geometry.Parse("M311,258 Q311,254 315,254 L327,254 Q331,254 331,258 L331,276 L349,276 Q353,276 353,280 L353,292 Q353,296 349,296 L331,296 L331,314 Q331,318 327,318 L315,318 Q311,318 311,314 L311,296 L293,296 Q289,296 289,292 L289,280 Q289,276 293,276 L311,276 Z"));
        foreach (var d in new[] { (Buttons.Up, 321d, 264d, "▲"), (Buttons.Down, 321d, 308d, "▼"), (Buttons.Left, 299d, 286d, "◀"), (Buttons.Right, 343d, 286d, "▶") })
        {
            bool down = _state.Buttons.HasFlag(d.Item1);
            if (down && _connected) dc.DrawRoundedRectangle(B(Input), null, new Rect(d.Item2 - 8, d.Item3 - 8, 16, 16), 3, 3);
            Text(dc, d.Item4, d.Item2, d.Item3, 10, down ? "#202328" : Ink);
        }
        dc.DrawEllipse(_connected ? B(Light ? "#F4F6F9" : "#292E35") : null, Outline(), new Point(386, 169), 17, 17);
        dc.DrawEllipse(null, Outline(), new Point(386, 169), 10, 10);
        Small(dc, 345, 212, Buttons.Back, "▣"); Small(dc, 427, 212, Buttons.Start, "☰");
        Face(dc, 504, 174, "Y", Light ? "#866000" : "#E6C576", Buttons.Y);
        Face(dc, 535, 205, "B", Light ? "#A6372E" : "#ECA39B", Buttons.B);
        Face(dc, 504, 236, "A", Light ? "#247345" : "#8ED4A5", Buttons.A);
        Face(dc, 473, 205, "X", Light ? "#245BA0" : "#99BFF4", Buttons.X);
        if (_connected && !string.IsNullOrEmpty(_callout))
        {
            var origin = CalloutCode switch
            {
                "A" => new Point(522, 236),
                "X" => new Point(491, 205),
                "Y" => new Point(522, 174),
                "LS" or "L3" => new Point(308, 205),
                "RS" or "R3" => new Point(490, 287),
                "LB" => new Point(275, 121),
                "RB" => new Point(538, 121),
                "LT" => new Point(150, 115),
                "RT" => new Point(629, 115),
                "D" => new Point(354, 286),
                "View" => new Point(355, 212),
                "Menu" => new Point(437, 212),
                _ => new Point(552, 205)
            };
            dc.DrawLine(Outline(Input, 1.5), origin, new Point(659, 205));
            dc.DrawRoundedRectangle(B(Light ? "#F7F8FA" : "#272B31"), Outline(Input, 2), new Rect(659, 184, 136, 42), 21, 21);
            Text(dc, _callout.Length > 21 ? _callout[..19] + "…" : _callout, 727, 205, 12, Input);
        }
        dc.Pop(); dc.Pop();
    }
    private void DrawAdaptive(DrawingContext dc)
    {
        var brush = (Brush)Application.Current.Resources["PanelBrush"];
        var ink = (Brush)Application.Current.Resources["TextBrush"];
        var accent = (Brush)Application.Current.Resources["AccentBrush"];
        var pen = new Pen(ink, 2);
        dc.DrawRoundedRectangle(brush, pen, new Rect(120, 115, 560, 285), 24, 24);
        Text(dc, "Xbox Adaptive Controller", 400, 86, 22);
        foreach (var face in new[] { (350d, "A", Buttons.A), (550d, "B", Buttons.B) })
        {
            bool down = _connected && _state.Buttons.HasFlag(face.Item3);
            dc.DrawEllipse(down ? accent : brush, pen, new Point(face.Item1, 265), 77, 77);
            Text(dc, face.Item2, face.Item1, 265, 36);
        }
        foreach (var direction in new[]{(Buttons.Up,205d,235d,"▲"),(Buttons.Down,205d,295d,"▼"),
            (Buttons.Left,175d,265d,"◀"),(Buttons.Right,235d,265d,"▶")})
        {
            bool down = _connected && _state.Buttons.HasFlag(direction.Item1);
            dc.DrawRoundedRectangle(down ? accent : brush, pen, new Rect(direction.Item2 - 14, direction.Item3 - 14, 28, 28), 4, 4);
            Text(dc, direction.Item4, direction.Item2, direction.Item3, 15);
        }
        Small(dc, 185, 160, Buttons.Back, "View"); Small(dc, 235, 160, Buttons.Start, "Menu");
        var ports = AdaptiveAccessory.Ports.ToArray();
        for (int i = 0; i < ports.Length; i++)
        {
            bool usb = ports[i].Id.StartsWith("USB");
            double x = usb ? (ports[i].Id == "USBLeft" ? 95 : 705) : 135 + i * 28;
            double y = usb ? 220 : 140;
            var accessory = _accessories.FirstOrDefault(a => a.Port == ports[i].Id);
            bool active = _connected && accessory?.Signals.Any(s => AdaptiveAccessory.Active(s, _state)) == true;
            dc.DrawRoundedRectangle(active ? accent : brush, new Pen(accessory != null ? accent : ink, accessory != null ? 3 : 1), new Rect(x - 10, y - 10, 20, 20), 3, 3);
            string label = ports[i].Id switch { "USBLeft" => "USB L", "USBRight" => "USB R", "LeftStick" => "L3", "RightStick" => "R3", "LeftShoulder" => "LB", "RightShoulder" => "RB", "LeftTrigger" => "LT", "RightTrigger" => "RT", "Back" => "View", "Start" => "Menu", "Xbox" => "Xbox", _ => ports[i].Id };
            if (accessory != null || usb) Text(dc, label, x, y - 24, 14);
        }
        Text(dc, _accessories.Length == 0 ? "Add your switches and joysticks with Set up accessories" : $"{_accessories.Length} saved accessories · highlighted sockets belong to your setup", 400, 430, 16);
        Text(dc, _callout.Length == 0 ? (_accessories.Length == 0 ? "Socket labels are schematic; match the label on your controller" : string.Join(" · ", _accessories.Take(3).Select(a => a.Name.Length > 22 ? a.Name[..22] + "…" : a.Name))) : _callout, 400, 463, 15);
    }
    private void DrawContrast(DrawingContext dc)
    {
        var line = new Pen(SystemColors.WindowTextBrush, 2);
        if (!_connected) line.DashStyle = DashStyles.Dash;
        dc.DrawGeometry(SystemColors.WindowBrush, line, Body);
        foreach (var face in new[] {(504d,174d,"Y",Buttons.Y),(535d,205d,"B",Buttons.B),
            (504d,236d,"A",Buttons.A),(473d,205d,"X",Buttons.X),(345d,212d,"View",Buttons.Back),(427d,212d,"Menu",Buttons.Start)})
        {
            bool pressed = _connected && _state.Buttons.HasFlag(face.Item4);
            dc.DrawEllipse(pressed ? SystemColors.HighlightBrush : SystemColors.WindowBrush, line, new Point(face.Item1, face.Item2), 17, 17);
            Text(dc, face.Item3, face.Item1, face.Item2, face.Item3.Length > 1 ? 9 : 15);
        }
        foreach (var stick in new[] { (270d, 205d, _lx, _ly), (452d, 287d, _rx, _ry) })
        {
            dc.DrawEllipse(null, line, new Point(stick.Item1, stick.Item2), 38, 38);
            dc.DrawEllipse(SystemColors.HighlightBrush, line, new Point(stick.Item1 + stick.Item3 * 10, stick.Item2 - stick.Item4 * 10), 23, 23);
        }
        Text(dc, "◀ ▲ ▼ ▶", 321, 286, 15);
        Text(dc, $"LT {_state.LeftTrigger * 100 / 255}%", 146, 70, 14);
        Text(dc, $"RT {_state.RightTrigger * 100 / 255}%", 625, 70, 14);
        Text(dc, _connected ? _paused ? "Paused · inputs not sent" : "Live controller inputs" : "No controller", 386, 470, 16);
    }
    private void Shoulder(DrawingContext dc, string path, Buttons button)
    {
        bool down = _connected && _state.Buttons.HasFlag(button);
        dc.DrawGeometry(_connected ? B(down ? Input : Light ? "#D7DCE2" : "#343941") : null, Outline(down ? Input : Edge), Geometry.Parse(path));
    }
    private void Trigger(DrawingContext dc, double x, byte value, string label)
    {
        if (!_connected) return;
        Text(dc, $"{label} {value * 100 / 255}%", x, 57, 13);
        dc.DrawRoundedRectangle(B(Light ? "#CDD2DA" : "#121518"), Outline(), new Rect(x - 4, 75, 8, 76), 4, 4);
        if (value > 0) dc.DrawRoundedRectangle(B(Input), null, new Rect(x - 4, 75, 8, Math.Max(2, 76 * value / 255d)), 4, 4);
        dc.DrawLine(new Pen(B(Ink), 1), new Point(x - 10, 90), new Point(x + 10, 90));
    }
    private void Stick(DrawingContext dc, double x, double y, double ax, double ay, bool pressed)
    {
        bool moved = _connected && Math.Sqrt(ax * ax + ay * ay) > .18;
        dc.DrawEllipse(_connected ? G(Light ? "#BFC6D0" : "#14171B", Light ? "#D9DDE3" : "#20252B") : null, Outline(moved ? Input : Edge, moved ? 2 : 1), new Point(x, y), 38, 38);
        var center = new Point(x + ax * 10, y - ay * 10);
        dc.DrawEllipse(_connected ? G(Light ? "#F8F9FC" : "#545D68", Light ? "#CFD4DD" : "#333A44") : null, Outline(), center, 28, 28);
        var dotted = Outline(Light ? "#8F99A6" : "#7D8897", 1.6); dotted.DashStyle = DashStyles.Dot;
        dc.DrawEllipse(null, dotted, center, 23, 23);
        dc.DrawEllipse(_connected && pressed ? B(Input) : null, Outline(), center, 17, 17);
    }
    private void Face(DrawingContext dc, double x, double y, string label, string color, Buttons button)
    {
        bool down = _connected && _state.Buttons.HasFlag(button);
        double radius = down && SystemParameters.ClientAreaAnimation ? 13 : 14;
        dc.DrawEllipse(_connected ? B(down ? Input : Light ? "#F4F6F9" : "#292E35") : null, Outline(down ? Input : Edge), new Point(x, y), radius, radius);
        if (down) dc.DrawEllipse(null, Outline(Input, 2), new Point(x, y), 18, 18);
        Text(dc, label, x, y, 16, down ? Light ? "#FFFFFF" : "#202328" : _connected ? color : Edge);
    }
    private void Small(DrawingContext dc, double x, double y, Buttons button, string label)
    {
        bool down = _connected && _state.Buttons.HasFlag(button);
        dc.DrawEllipse(_connected ? B(down ? Input : Light ? "#F4F6F9" : "#292E35") : null, Outline(), new Point(x, y), 9, 9);
        Text(dc, label, x, y, 11, down ? "#202328" : Ink);
    }
}
