using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControllerMouse.Core;

namespace ControllerMouse.App;

/// <summary>Natural controller artwork with independent accessible input controls.</summary>
public sealed class ControllerSurface : UserControl
{
    // Rear-facing order checked against Microsoft's rear product photograph, not enum order.
    // https://www.microsoft.com/en-gb/p/xbox-adaptive-controller/8nsdbhz1n3d8
    // Socket types: https://assets.xboxservices.com/assets/b8/05/b8055a56-827c-48f8-b8ec-2c7aa66ef069.pdf
    public static IReadOnlyList<string> RearPortOrder { get; } = Array.AsReadOnly(new[]
    {
        "Y", "X", "B", "A", "RightStick", "RightShoulder", "RightTrigger", "LeftTrigger", "Start", "Back",
        "X2", "X1", "Xbox", "LeftShoulder", "LeftStick", "Right", "Up", "Down", "Left"
    });

    private readonly DiagramPanel _diagram;
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<string, Button> _parts = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, Button> _readOnlyParts;
    private Uri? _layoutArtworkUri;
    public IReadOnlyDictionary<string, Button> Parts => _readOnlyParts;
    public Button? GetPart(string id) => _parts.GetValueOrDefault(id);
    public event Action<string>? InputSelected;
    public BitmapSource? ArtworkSource => ControllerArtwork.For(Mode).Source;
    public Uri? ArtworkResourceUri => ControllerArtwork.For(Mode).ResourceUri;
    public bool ArtworkVisible => !Contrast && TryFindResource("UseControllerArtwork") is not false && ArtworkSource != null &&
        ArtworkSupportsDpi(VisualTreeHelper.GetDpi(this));
    internal bool ArtworkSupportsDpi(DpiScale dpi) => ArtworkSource is { } source &&
        _diagram.RenderedArtworkSize.Width * dpi.DpiScaleX <= source.PixelWidth + 1 &&
        _diagram.RenderedArtworkSize.Height * dpi.DpiScaleY <= source.PixelHeight + 1;
    internal bool MotionReduced => ReducedMotion || !SystemParameters.ClientAreaAnimation ||
        Application.Current is App { CurrentVisualAccess.Motion: not VisualMotion.Standard };
    public Rect ArtworkRenderedBounds => _diagram.ArtworkBounds;
    internal BitmapSource? StickCap(string id) => id == "LeftAxis" ? ControllerArtwork.For(0).LeftCap : ControllerArtwork.For(0).RightCap;

    private static DependencyProperty Property<T>(string name, T defaultValue, PropertyChangedCallback changed) =>
        DependencyProperty.Register(name, typeof(T), typeof(ControllerSurface), new FrameworkPropertyMetadata(defaultValue, changed));
    public static readonly DependencyProperty StateProperty = Property(nameof(State), default(Gamepad), StateChanged);
    public static readonly DependencyProperty ConnectedProperty = Property(nameof(Connected), false, StateChanged);
    public static readonly DependencyProperty AdaptiveProperty = Property(nameof(Adaptive), false, LayoutChanged);
    public static readonly DependencyProperty PortsViewProperty = Property(nameof(PortsView), false, LayoutChanged);
    public static readonly DependencyProperty ShowLabelsProperty = Property(nameof(ShowLabels), false, LayoutChanged);
    public static readonly DependencyProperty ReducedMotionProperty = Property(nameof(ReducedMotion), false, StateChanged);
    public static readonly DependencyProperty DeadZoneProperty = Property(nameof(DeadZone), .18, StateChanged);
    public static readonly DependencyProperty ScrollDeadZoneProperty = Property(nameof(ScrollDeadZone), .18, StateChanged);
    public static readonly DependencyProperty ShowDeadZonesProperty = Property(nameof(ShowDeadZones), false, StateChanged);
    public static readonly DependencyProperty AccessoriesProperty = Property(nameof(Accessories), Array.Empty<AdaptiveAccessory>(), StateChanged);
    public static readonly DependencyProperty SelectedInputProperty = Property(nameof(SelectedInput), "A", StateChanged);
    public static readonly DependencyProperty InputsProperty = DependencyProperty.Register(nameof(Inputs),
        typeof(IReadOnlyDictionary<string, InputPresentation>), typeof(ControllerSurface),
        new FrameworkPropertyMetadata(new ReadOnlyDictionary<string, InputPresentation>(new Dictionary<string, InputPresentation>()), StateChanged));

    public Gamepad State { get => (Gamepad)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool Connected { get => (bool)GetValue(ConnectedProperty); set => SetValue(ConnectedProperty, value); }
    public bool Adaptive { get => (bool)GetValue(AdaptiveProperty); set => SetValue(AdaptiveProperty, value); }
    public bool PortsView { get => (bool)GetValue(PortsViewProperty); set => SetValue(PortsViewProperty, value); }
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ReducedMotion { get => (bool)GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    public double DeadZone { get => (double)GetValue(DeadZoneProperty); set => SetValue(DeadZoneProperty, value); }
    public double ScrollDeadZone { get => (double)GetValue(ScrollDeadZoneProperty); set => SetValue(ScrollDeadZoneProperty, value); }
    public bool ShowDeadZones { get => (bool)GetValue(ShowDeadZonesProperty); set => SetValue(ShowDeadZonesProperty, value); }
    public AdaptiveAccessory[] Accessories { get => (AdaptiveAccessory[])GetValue(AccessoriesProperty); set => SetValue(AccessoriesProperty, value ?? []); }
    public string SelectedInput { get => (string)GetValue(SelectedInputProperty); set => SetValue(SelectedInputProperty, value ?? "A"); }
    public IReadOnlyDictionary<string, InputPresentation> Inputs
    {
        get => (IReadOnlyDictionary<string, InputPresentation>)GetValue(InputsProperty);
        set => SetValue(InputsProperty, value ?? new ReadOnlyDictionary<string, InputPresentation>(new Dictionary<string, InputPresentation>()));
    }

    internal bool Contrast => SystemParameters.HighContrast || Application.Current is App { ContrastActive: true };
    internal Brush Brush(string token, Brush fallback) => Contrast ? token switch
    {
        "FocusRingBrush" or "SelectedRingBrush" or "InputActiveRingBrush" => SystemColors.HighlightBrush,
        "TextPrimaryBrush" or "TextSecondaryBrush" or "BorderStrongBrush" or "FocusInnerBrush" or "SelectedInnerBrush" or "ArtworkInkBrush" => SystemColors.WindowTextBrush,
        "ControllerCapTextBrush" => SystemColors.ControlTextBrush,
        "ControllerCapBrush" => SystemColors.ControlBrush,
        _ => SystemColors.WindowBrush
    } : TryFindResource(token) as Brush ?? fallback;

    public ControllerSurface()
    {
        Focusable = false;
        _readOnlyParts = new(_parts);
        _diagram = new(this);
        _scroll = new ScrollViewer
        {
            Content = _diagram,
            Focusable = false,
            CanContentScroll = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0)
        };
        KeyboardNavigation.SetTabNavigation(_scroll, KeyboardNavigationMode.Continue);
        Content = _scroll;
        AutomationProperties.SetName(this, "Interactive controller inputs");
        AutomationProperties.SetHelpText(this, "Choose an input to inspect its assignment. Selecting a drawing does not perform its Windows action. Equivalent input controls are available in the input list.");
        Loaded += (_, _) => UpdateViewport();
        SizeChanged += (_, _) => UpdateViewport();
        _scroll.ScrollChanged += (_, e) =>
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdateViewport();
        };
        Rebuild();
    }

    private static void StateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ControllerSurface)d).Refresh();
    private static void LayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ControllerSurface)d).Rebuild();

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _diagram.InvalidateVisual();
        foreach (var part in _parts.Values) part.InvalidateVisual();
    }

    private void Rebuild()
    {
        if (_diagram == null) return;
        // Labels do not change parts or focus; changing hardware views necessarily changes the set.
        if (_diagram.Mode == Mode && _parts.Count > 0) { UpdateViewport(); Refresh(); return; }
        _parts.Clear(); _diagram.Children.Clear(); _diagram.Layouts.Clear(); _diagram.Mode = Mode;
        _diagram.DpadCenter = null; _diagram.DpadRadius = 0;
        if (Mode == 2) BuildPorts(); else if (Mode == 1) BuildAdaptive(); else BuildStandard();
        _layoutArtworkUri = ControllerArtwork.For(Mode).ResourceUri;
        UpdateViewport(); Refresh();
    }

    private int Mode => Adaptive ? PortsView ? 2 : 1 : 0;
    private void Add(string id, string code, ControllerPartShape shape, Rect bounds, Point? center = null, double radius = 0)
    {
        var layout = new ControllerPartLayout(bounds, center ?? new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), radius);
        if (_parts.TryGetValue(id, out var existing)) { _diagram.Layouts[(ControllerPartButton)existing] = layout; return; }
        var button = new ControllerPartButton(this, id, code, shape);
        button.Click += (_, _) =>
        {
            SelectedInput = id; InputSelected?.Invoke(id);
        };
        _parts.Add(id, button); _diagram.Layouts.Add(button, layout); _diagram.Children.Add(button);
    }

    private void BuildStandard()
    {
        var art = ControllerArtwork.For(0); var image = _diagram.WorldArtwork;
        Point At(double x, double y) => art.Map(new(x, y), image);
        void Physical(string id, string code, ControllerPartShape shape, double x, double y, double nativeRadius, double target = 55)
        { var p = At(x, y); Add(id, code, shape, new(p.X - target / 2, p.Y - target / 2, target, target), p, art.Length(nativeRadius, image)); }
        var left = At(399, 365); var right = At(943, 585);
        Add("LeftTrigger", "LT", ControllerPartShape.Trigger, new(left.X - 38, 0, 76, 56));
        Add("RightTrigger", "RT", ControllerPartShape.Trigger, new(At(1134, 260).X - 38, 0, 76, 56));
        var lb = At(399, 118); var rb = At(1134, 118);
        Add("LeftShoulder", "LB", ControllerPartShape.Shoulder, new(lb.X - 63, lb.Y - 27.5, 126, 55), lb);
        Add("RightShoulder", "RB", ControllerPartShape.Shoulder, new(rb.X - 63, rb.Y - 27.5, 126, 55), rb);
        Physical("Xbox", "Xbox", ControllerPartShape.Xbox, 766, 225, 57);
        Physical("Back", "View", ControllerPartShape.View, 661, 362, 33);
        Physical("Start", "Menu", ControllerPartShape.Menu, 867, 362, 33);
        Physical("LeftAxis", "Move", ControllerPartShape.Stick, 399, 365, 98, 110);
        Physical("RightAxis", "Scroll", ControllerPartShape.Stick, 943, 585, 98, 110);
        Add("LeftStick", "L3", ControllerPartShape.StickClick, new(left.X - 27.5, left.Y + 62, 55, 55));
        Add("RightStick", "R3", ControllerPartShape.StickClick, new(right.X - 27.5, right.Y + 62, 55, 55));
        Dpad(At(565, 596), art.Length(124, image));
        Physical("Y", "Y", ControllerPartShape.Face, 1134, 260, 55);
        Physical("X", "X", ControllerPartShape.Face, 1028, 365, 55);
        Physical("B", "B", ControllerPartShape.Face, 1241, 365, 55);
        Physical("A", "A", ControllerPartShape.Face, 1134, 463, 56);
    }

    private void Dpad(Point center, double visibleRadius)
    {
        double r = Math.Max(55, visibleRadius);
        _diagram.DpadCenter = center; _diagram.DpadRadius = r; _diagram.DpadVisibleRadius = visibleRadius;
        foreach (var (id, shape, bounds) in new[] {
            ("Up", ControllerPartShape.DpadUp, new Rect(center.X-r,center.Y-r,r*2,r)),
            ("Down", ControllerPartShape.DpadDown, new Rect(center.X-r,center.Y,r*2,r)),
            ("Left", ControllerPartShape.DpadLeft, new Rect(center.X-r,center.Y-r,r,r*2)),
            ("Right", ControllerPartShape.DpadRight, new Rect(center.X,center.Y-r,r,r*2)) })
        {
            Add(id, id, shape, bounds, center, visibleRadius);
            _diagram.Layouts[(ControllerPartButton)_parts[id]] = new(bounds, center, visibleRadius, center, r);
        }
    }

    private void BuildAdaptive()
    {
        var art = ControllerArtwork.For(1); var image = _diagram.WorldArtwork;
        Point At(double x, double y) => art.Map(new(x, y), image);
        void System(string id, string code, ControllerPartShape shape, Point physical, Point target, double r)
            => Add(id, code, shape, new(target.X - 27.5, target.Y - 27.5, 55, 55), physical, art.Length(r, image));
        var xbox = At(321, 165); var view = At(247, 267); var menu = At(389, 267); var profile = At(389, 364);
        System("Xbox", "Xbox", ControllerPartShape.Xbox, xbox, new(xbox.X, Math.Min(xbox.Y, menu.Y - 61)), 46);
        System("Back", "View", ControllerPartShape.View, view, new(view.X, menu.Y), 32);
        System("Start", "Menu", ControllerPartShape.Menu, menu, menu, 32);
        System("Profile", "Profile", ControllerPartShape.Profile, profile, new(profile.X, menu.Y + 55), 32);
        Dpad(At(311, 507), art.Length(99, image));
        foreach (var (id, x) in new[] { ("A", 782d), ("B", 1463d) })
        { var p = At(x, 382); double r = art.Length(261, image); Add(id, id, ControllerPartShape.LargePad, new(p.X - r, p.Y - r, r * 2, r * 2), p, r); }
    }

    private void BuildPorts()
    {
        for (int i = 0; i < RearPortOrder.Count; i++)
        {
            string id = RearPortOrder[i];
            Add(id, PortCode(id), ControllerPartShape.Port, new(72 + i * 55, 136, 55, 94), new(99.5 + i * 55, 160), 15);
        }
        Add("USBLeft", "Left USB", ControllerPartShape.Usb, new(62, 276, 170, 64));
        Add("USBRight", "Right USB", ControllerPartShape.Usb, new(1068, 276, 170, 64));
    }

    private static string PortCode(string id) => id switch
    {
        "RightStick" => "R3",
        "LeftStick" => "L3",
        "RightShoulder" => "RB",
        "LeftShoulder" => "LB",
        "RightTrigger" => "RT",
        "LeftTrigger" => "LT",
        "Back" => "View",
        "Start" => "Menu",
        "Up" => "Up",
        "Down" => "Down",
        "Right" => "Right",
        "Left" => "Left",
        _ => id
    };

    private void UpdateViewport()
    {
        double minimumWidth = Mode == 2 ? 1120 : 640;
        double minimumHeight = Mode == 2 ? 310 : Mode == 1 ? 320 : 420;
        // The actual viewport already accounts for the theme's scrollbar width (including
        // the larger accessibility scrollbar). Reserving space when no bar is needed can
        // create a horizontal and vertical scrollbar which keep one another visible.
        // Cap it at our own bounds so a parent measured at infinity cannot grow the board.
        double width = double.IsFinite(_scroll.ViewportWidth) && _scroll.ViewportWidth > 0
            ? Math.Min(ActualWidth, _scroll.ViewportWidth) : ActualWidth;
        double boardWidth = Math.Max(minimumWidth, Math.Floor(Math.Max(0, width)));
        double labels = ShowLabels ? _diagram.MeasureLabels(boardWidth) : 0;
        bool labelsChanged = _diagram.LabelHeight != labels;
        _diagram.LabelHeight = labels;
        // A previous labels/view measurement must never become the next hero height.
        // Each view has a deterministic base; optional wrapped labels add their own space.
        double boardHeight = minimumHeight + _diagram.LabelHeight;
        // The page owns vertical scrolling. Only a narrow controller diagram needs
        // local horizontal scrolling to preserve its accessible input targets.
        Height = boardHeight + (boardWidth > width + 1 ? Math.Max(44, (double)(TryFindResource("ControlMinHeight") ?? 44d)) : 0);
        if (_diagram.Width != boardWidth) _diagram.Width = boardWidth;
        if (_diagram.Height != boardHeight) _diagram.Height = boardHeight;
        if (labelsChanged) _diagram.InvalidateArrange();
        _diagram.InvalidateVisual();
    }

    private void Refresh()
    {
        if (_diagram == null) return;
        var uri = ControllerArtwork.For(Mode).ResourceUri;
        if (_layoutArtworkUri != uri)
        {
            _diagram.Layouts.Clear(); _diagram.DpadCenter = null; _diagram.DpadRadius = 0;
            if (Mode == 2) BuildPorts(); else if (Mode == 1) BuildAdaptive(); else BuildStandard();
            _layoutArtworkUri = uri; _diagram.InvalidateArrange(); UpdateViewport();
        }
        foreach (var button in _parts.Values.OfType<ControllerPartButton>()) button.Refresh();
        if (ShowLabels) UpdateViewport();
        _diagram.InvalidateVisual();
    }

    internal InputPresentation? Presentation(string id)
    {
        var presentation = Inputs.GetValueOrDefault(id);
        if (Mode != 2) return presentation;
        var accessory = Accessories.FirstOrDefault(a => a.Port == id);
        if (presentation == null) return null;
        string action = accessory is { Signals.Length: > 0 }
            ? string.Join(", ", accessory.Signals.Select(signal => Inputs.GetValueOrDefault(signal.ToString())?.Action ?? AdaptiveAccessory.SignalName(signal)).Distinct())
            : presentation.Action;
        return presentation with
        {
            Name = AdaptiveAccessory.Ports.FirstOrDefault(port => port.Id == id)?.Name ?? presentation.Name,
            Action = action,
            AccessoryName = accessory?.Name
        };
    }
    internal bool HardwareActive(string id)
    {
        if (!Connected) return false;
        if (Mode == 2 && Accessories.FirstOrDefault(a => a.Port == id) is { Signals.Length: > 0 } accessory)
            return accessory.Signals.Any(s => AdaptiveAccessory.Active(s, State));
        if (Presentation(id) is { } input) return input.IsActive;
        if (id is "LeftAxis" or "X1" or "USBLeft") return InputProcessor.Shape(State.LeftX, State.LeftY, DeadZone, 1) != (0d, 0d);
        if (id is "RightAxis" or "X2" or "USBRight") return InputProcessor.Shape(State.RightX, State.RightY, ScrollDeadZone, 1) != (0d, 0d);
        return Enum.TryParse<ControllerMouse.Core.Control>(id, out var control) && ControlMapping.Down(State, control);
    }

    internal string InputName(string id) => Presentation(id)?.Name ?? (Mode == 2 ? AdaptiveAccessory.Ports.FirstOrDefault(port => port.Id == id)?.Name : null) ?? (id switch
    {
        "LeftAxis" => "Left stick movement",
        "RightAxis" => "Right stick movement",
        "Xbox" => "Xbox button",
        "Profile" => "Physical profile button",
        "USBLeft" => "Left USB port",
        "USBRight" => "Right USB port",
        "X1" => "X1 joystick socket",
        "X2" => "X2 joystick socket",
        _ => ControlMapping.Controls.FirstOrDefault(c => c.Control.ToString() == id)?.Name ?? id
    });

    private sealed class DiagramPanel : Panel
    {
        private readonly ControllerSurface _owner;
        internal readonly Dictionary<ControllerPartButton, ControllerPartLayout> Layouts = [];
        internal int Mode = -1;
        internal double LabelHeight;
        internal Point? DpadCenter;
        internal double DpadRadius, DpadVisibleRadius;
        private double _scale = 1, _offsetX, _offsetY;
        private double WorldWidth => Mode == 2 ? 1300 : 800;
        private double WorldHeight => Mode == 2 ? 360 : Mode == 1 ? 350 : 525;
        internal Rect WorldArtwork => ControllerArtwork.For(Mode).Fit(Mode == 2 ? new(12, 52, 1276, 204) : Mode == 1 ? new(8, 10, 784, 302) : new(20, 68, 760, 456));
        internal Size RenderedArtworkSize => new(WorldArtwork.Width * _scale, WorldArtwork.Height * _scale);
        internal Rect ArtworkBounds => !_owner.ArtworkVisible ? Rect.Empty : new(TranslatePoint(new Point(_offsetX + WorldArtwork.X * _scale, _offsetY + WorldArtwork.Y * _scale), _owner), new Size(WorldArtwork.Width * _scale, WorldArtwork.Height * _scale));
        private readonly List<(FormattedText Title, FormattedText Action, Point Position)> _labels = [];
        internal DiagramPanel(ControllerSurface owner)
        {
            _owner = owner; Focusable = false; VerticalAlignment = VerticalAlignment.Top; HorizontalAlignment = HorizontalAlignment.Left;
            Background = Brushes.Transparent;
            SetResourceReference(TagProperty, "ControllerBodyBrush");
            AutomationProperties.SetName(this, "Controller illustration");
        }
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == TagProperty) { _owner.Refresh(); InvalidateVisual(); }
        }
        protected override Size MeasureOverride(Size available)
        {
            foreach (var part in Layouts.Keys) part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(Width, Height);
        }
        protected override Size ArrangeOverride(Size final)
        {
            _scale = Math.Max(Mode == 2 ? 1120d / 1300 : .8,
                Math.Min(final.Width / WorldWidth, Math.Max(0, final.Height - LabelHeight) / WorldHeight));
            _offsetX = (final.Width - WorldWidth * _scale) / 2;
            _offsetY = Math.Max(0, (final.Height - LabelHeight - WorldHeight * _scale) / 2);
            foreach (var pair in Layouts)
            {
                var layout = pair.Value; var r = layout.Target;
                double width = Math.Max(44, r.Width * _scale), height = Math.Max(44, r.Height * _scale);
                var target = new Rect(_offsetX + (r.X + r.Width / 2) * _scale - width / 2,
                    _offsetY + (r.Y + r.Height / 2) * _scale - height / 2, width, height);
                pair.Key.Arrange(target);
                Point Local(Point p) => new(_offsetX + p.X * _scale - target.X, _offsetY + p.Y * _scale - target.Y);
                pair.Key.SetArtworkGeometry(Local(layout.Center), layout.Radius * _scale,
                    layout.DpadCenter is Point d ? Local(d) : null, layout.DpadRadius * _scale,
                    DpadCenter is Point excluded ? Local(excluded) : null, DpadRadius * _scale);
            }
            return final;
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Brush body = _owner.Brush("ControllerBodyBrush", SystemColors.ControlBrush);
            Brush border = _owner.Brush("BorderStrongBrush", SystemColors.ControlTextBrush);
            var pen = new Pen(border, 1.8) { DashStyle = _owner.Connected ? DashStyles.Solid : DashStyles.Dash };
            dc.PushTransform(new TranslateTransform(_offsetX, _offsetY)); dc.PushTransform(new ScaleTransform(_scale, _scale));
            var art = ControllerArtwork.For(Mode);
            if (_owner.ArtworkVisible)
            {
                dc.DrawImage(art.Source, WorldArtwork);
                if (Mode == 0 && art.ShellPatch != null)
                {
                    var p = art.Map(new(712, 406), WorldArtwork);
                    var patch = new Rect(p, new Size(art.Length(110, WorldArtwork), art.Length(72, WorldArtwork)));
                    dc.DrawRoundedRectangle(new ImageBrush(art.ShellPatch) { Stretch = Stretch.Fill }, null, patch, 18, 18);
                }
            }
            else if (Mode == 0)
            {
                dc.PushTransform(new TranslateTransform(WorldArtwork.X, WorldArtwork.Y));
                dc.PushTransform(new ScaleTransform(WorldArtwork.Width / 614, WorldArtwork.Height / 365));
                dc.PushTransform(new TranslateTransform(-93, -110));
                dc.DrawGeometry(body, pen, StandardBody); dc.DrawGeometry(null, new Pen(border, .8), StandardSeams);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            else if (Mode == 1)
            {
                dc.DrawRoundedRectangle(body, pen, WorldArtwork, 17, 17);
            }
            if (Mode == 2)
            {
                // Generated artwork supplies only the shell/pads. This opaque rear face
                // replaces every generated jack with the verified physical arrangement.
                Brush plastic = _owner.Contrast || !_owner.ArtworkVisible ? body : new LinearGradientBrush(Color.FromRgb(250, 251, 252), Color.FromRgb(198, 204, 211), 90);
                dc.DrawRoundedRectangle(plastic, pen, new Rect(24, 128, 1252, 118), 9, 9);
                Text(dc, "Rear sockets · facing the back", 24, 10, 18, true);
                Text(dc, "Logical input activity · physical connection is not reported", 24, 34, 16, false);
                Text(dc, "Side USB ports · left and right when facing the front", 350, 296, 16, false);
                // Sync, host USB-C and power are physical system connectors, not input mappings.
                dc.DrawEllipse(null, new Pen(border, 2), new Point(48, 160), 9, 9);
                dc.DrawRoundedRectangle(_owner.Contrast ? body : Brushes.Black, new Pen(border, 2), new Rect(1155, 151, 38, 15), 4, 4);
                dc.DrawEllipse(_owner.Contrast ? body : Brushes.Black, new Pen(border, 2), new Point(1240, 160), 13, 13);
                Text(dc, "Sync", 31, 206, 14, false, artworkLabel: true); Text(dc, "USB-C", 1151, 206, 14, false, artworkLabel: true); Text(dc, "Power", 1217, 206, 14, false, artworkLabel: true);
            }
            if (!_owner.ArtworkVisible && DpadCenter is Point dpad) DrawDpad(dc, dpad, DpadVisibleRadius, border);
            dc.Pop(); dc.Pop();
            if (_owner.ShowLabels) DrawLabels(dc);
        }
        internal double MeasureLabels(double availableWidth)
        {
            _labels.Clear();
            double font = TryFindResource("CaptionFontSize") is double size ? Math.Max(14, size) : 14;
            int columns = Math.Max(1, Math.Min(Mode == 2 ? 6 : 4, (int)(availableWidth / (210 * font / 14))));
            double width = availableWidth / columns, y = 8; int i = 0;
            var parts = Layouts.Keys.ToArray();
            for (int row = 0; row < Math.Ceiling(parts.Length / (double)columns); row++)
            {
                double height = 0;
                for (int column = 0; column < columns && i < parts.Length; column++, i++)
                {
                    var part = parts[i];
                    var title = Formatted(part.Code + " · " + _owner.InputName(part.Id), font, true, width - 20);
                    var action = Formatted(_owner.Presentation(part.Id)?.Action ?? "No assignment", font, false, width - 20);
                    _labels.Add((title, action, new(column * width + 10, y)));
                    height = Math.Max(height, title.Height + action.Height + 8);
                }
                y += height + 16;
            }
            return y;
        }
        private void DrawLabels(DrawingContext dc)
        { foreach (var label in _labels) { var p = new Point(label.Position.X, ActualHeight - LabelHeight + label.Position.Y); dc.DrawText(label.Title, p); dc.DrawText(label.Action, new Point(p.X, p.Y + label.Title.Height + 6)); } }
        private FormattedText Formatted(string value, double size, bool bold, double maximum) => new(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(VisualAccessTheme.InterfaceFont, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size,
            _owner.Brush("TextPrimaryBrush", SystemColors.WindowTextBrush), VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = maximum };
        private void DrawDpad(DrawingContext dc, Point p, double r, Brush edge)
        {
            var cross = new StreamGeometry(); using (var c = cross.Open())
            {
                double q = r * .3; c.BeginFigure(new(p.X - q, p.Y - r), true, true);
                foreach (var point in new[] { new Point(p.X + q, p.Y - r), new(p.X + q, p.Y - q), new(p.X + r, p.Y - q), new(p.X + r, p.Y + q), new(p.X + q, p.Y + q), new(p.X + q, p.Y + r), new(p.X - q, p.Y + r), new(p.X - q, p.Y + q), new(p.X - r, p.Y + q), new(p.X - r, p.Y - q), new(p.X - q, p.Y - q) }) c.LineTo(point, true, false);
            }
            dc.DrawGeometry(_owner.Brush("ControllerCapBrush", SystemColors.ControlBrush), new Pen(edge, 2), cross);
        }
        private void Text(DrawingContext dc, string value, double x, double y, double size, bool bold, double maximum = 900, bool artworkLabel = false)
        {
            var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(VisualAccessTheme.InterfaceFont, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                Math.Max(14 / Math.Max(.1, _scale), size), _owner.Brush(artworkLabel ? "ArtworkInkBrush" : "TextPrimaryBrush", SystemColors.WindowTextBrush), VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = maximum, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(text, new Point(x, y));
        }
        private static readonly Geometry StandardBody = Geometry.Parse(
            "M152,163 C166,113 206,109 260,124 C301,136 340,151 400,151 C460,151 499,136 540,124 C594,109 634,113 648,163 C679,241 711,351 707,409 C704,453 688,475 660,475 C627,475 606,437 577,408 C552,383 537,380 510,380 L290,380 C263,380 248,383 223,408 C194,437 173,475 140,475 C112,475 96,453 93,409 C89,351 121,241 152,163 Z");
        private static readonly Geometry StandardSeams = Geometry.Parse(
            "M144,187 C191,133 228,140 283,157 M517,157 C572,140 609,133 656,187 M146,445 C176,400 196,378 226,365 M574,365 C604,378 624,400 654,445");
    }
}
