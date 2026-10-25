namespace ControllerMouse.Core;

public enum Control { A, B, X, Y, LeftStick, RightStick, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, Up, Down, Left, Right, Back, Start }
public enum BindingKind { None, Mouse, Shortcut, Keyboard, Precision, Boost, Pointer }

public sealed record ControlBinding(BindingKind Kind = BindingKind.None, MouseButton Mouse = MouseButton.Left,
    Shortcut Shortcut = Shortcut.None, int Key = 0, int Modifiers = 0)
{
    public bool Repeat { get; init; }
    // Modifiers: Ctrl=1, Alt=2, Shift=4, Windows=8. Keyboard mappings send a tap.
    public bool IsValid => Enum.IsDefined(Kind) && Enum.IsDefined(Mouse) && Enum.IsDefined(Shortcut) &&
        (Kind != BindingKind.Keyboard || (Key is >= 8 and <= 254 && Key is not (16 or 17 or 18 or 91 or 92 or >= 160 and <= 165) && Modifiers is >= 0 and <= 15)) &&
        (Kind != BindingKind.Pointer || Key is >= 37 and <= 40) && (!Repeat || RelayActions.CanRepeat(this));
    public ushort[] Keys() => new[] { (Modifiers & 1) != 0 ? 0x11 : 0, (Modifiers & 2) != 0 ? 0x12 : 0,
        (Modifiers & 4) != 0 ? 0x10 : 0, (Modifiers & 8) != 0 ? 0x5B : 0, Key }.Where(k => k != 0).Select(k => (ushort)k).ToArray();
}

public sealed record ControlOption(Control Control, string Code, string Name);

public static class ControlMapping
{
    public static Buttons ButtonFlag(Control c) => c switch
    {
        Control.A => Buttons.A,
        Control.B => Buttons.B,
        Control.X => Buttons.X,
        Control.Y => Buttons.Y,
        Control.LeftStick => Buttons.LeftStick,
        Control.RightStick => Buttons.RightStick,
        Control.LeftShoulder => Buttons.LeftShoulder,
        Control.RightShoulder => Buttons.RightShoulder,
        Control.Up => Buttons.Up,
        Control.Down => Buttons.Down,
        Control.Left => Buttons.Left,
        Control.Right => Buttons.Right,
        Control.Back => Buttons.Back,
        Control.Start => Buttons.Start,
        _ => Buttons.None
    };
    public static IReadOnlyList<ControlOption> Controls { get; } = new ControlOption[]
    {
        new(Control.A,"A","A"), new(Control.B,"B","B"), new(Control.X,"X","X"), new(Control.Y,"Y","Y"),
        new(Control.LeftStick,"L3","Left stick click"), new(Control.RightStick,"R3","Right stick click"),
        new(Control.LeftShoulder,"LB","Left bumper"), new(Control.RightShoulder,"RB","Right bumper"),
        new(Control.LeftTrigger,"LT","Left trigger"), new(Control.RightTrigger,"RT","Right trigger"),
        new(Control.Up,"↑","D-pad up"), new(Control.Down,"↓","D-pad down"),
        new(Control.Left,"←","D-pad left"), new(Control.Right,"→","D-pad right"),
        new(Control.Back,"View","View / Back"), new(Control.Start,"Menu","Menu / Start")
    };
    public static ControlBinding Resolve(Settings settings, Control control) =>
        settings.ButtonMappings?.GetValueOrDefault(control) ?? Default(settings, control);

    public static ControlBinding Default(Settings s, Control c) => c switch
    {
        Control.Up => new(BindingKind.Pointer, Key: 38),
        Control.Down => new(BindingKind.Pointer, Key: 40),
        Control.Left => new(BindingKind.Pointer, Key: 37),
        Control.Right => new(BindingKind.Pointer, Key: 39),
        Control.A => new(BindingKind.Mouse, MouseButton.Left),
        Control.B => new(BindingKind.Mouse, MouseButton.Right),
        Control.X => new(BindingKind.Mouse, MouseButton.Middle),
        Control.LeftShoulder => new(BindingKind.Precision),
        Control.RightShoulder => new(BindingKind.Boost),
        _ => new(BindingKind.Shortcut, Shortcut: c switch
        {
            Control.Y => s.YAction,
            Control.LeftStick => s.LeftStickAction,
            Control.RightStick => s.RightStickAction,
            Control.LeftTrigger => Shortcut.ZoomOut,
            Control.RightTrigger => Shortcut.ZoomIn,
            Control.Up => Shortcut.Up,
            Control.Down => Shortcut.Down,
            Control.Left => Shortcut.Left,
            Control.Right => Shortcut.Right,
            Control.Back => Shortcut.Accessibility,
            Control.Start => Shortcut.StartMenu,
            _ => Shortcut.None
        })
    };
    public static bool Down(Gamepad pad, Control c) => c switch
    {
        Control.LeftTrigger => pad.LeftTrigger > 50,
        Control.RightTrigger => pad.RightTrigger > 50,
        _ => pad.Buttons.HasFlag(c switch
        {
            Control.A => Buttons.A,
            Control.B => Buttons.B,
            Control.X => Buttons.X,
            Control.Y => Buttons.Y,
            Control.LeftStick => Buttons.LeftStick,
            Control.RightStick => Buttons.RightStick,
            Control.LeftShoulder => Buttons.LeftShoulder,
            Control.RightShoulder => Buttons.RightShoulder,
            Control.Up => Buttons.Up,
            Control.Down => Buttons.Down,
            Control.Left => Buttons.Left,
            Control.Right => Buttons.Right,
            Control.Back => Buttons.Back,
            Control.Start => Buttons.Start,
            _ => Buttons.None
        })
    };
    public static bool Modifier(Gamepad pad, Settings s, BindingKind kind) => Controls.Any(c =>
        c.Control is not (Control.Back or Control.Start) && Down(pad, c.Control) && Resolve(s, c.Control).Kind == kind);
    public static bool MouseActive(Gamepad pad, Settings s) => Controls.Any(c => Down(pad, c.Control) && Resolve(s, c.Control).Kind == BindingKind.Mouse);
}
