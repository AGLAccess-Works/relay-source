namespace ControllerMouse.Core;

public readonly record struct InputState(Gamepad Raw, int Held, int Pressed, int Released, bool Connected)
{
    public bool IsDown(Control control) => (Held & (1 << (int)control)) != 0;
    public bool WasPressed(Control control) => (Pressed & (1 << (int)control)) != 0;
    public bool WasReleased(Control control) => (Released & (1 << (int)control)) != 0;
}

/// <summary>Logical XInput controls, independent of the shape of the physical device.</summary>
public sealed class InputStateManager
{
    public InputState Current { get; private set; }
    public InputState Previous { get; private set; }
    public InputState Update(Gamepad? pad)
    {
        Previous = Current;
        int held = pad.HasValue ? HeldMask(pad.Value, Previous.Held) : 0;
        return Current = new(pad ?? default, held, held & ~Previous.Held, Previous.Held & ~held, pad.HasValue);
    }
    public void Reset() { Current = Previous = default; }
    public static int HeldMask(Gamepad pad, int previous)
    {
        int mask = 0;
        foreach (var option in ControlMapping.Controls)
        {
            int bit = 1 << (int)option.Control;
            bool down = option.Control switch
            {
                Control.LeftTrigger => pad.LeftTrigger > 50 || (previous & bit) != 0 && pad.LeftTrigger >= 35,
                Control.RightTrigger => pad.RightTrigger > 50 || (previous & bit) != 0 && pad.RightTrigger >= 35,
                _ => ControlMapping.Down(pad, option.Control)
            };
            if (down) mask |= bit;
        }
        return mask;
    }
}

public sealed class RepeatScheduler
{
    private readonly Dictionary<Control, double> _remaining = [];
    public void Reset() => _remaining.Clear();
    public bool ShouldFire(Control control, bool down, bool pressed, double elapsed, double delay, double interval)
    {
        if (!down) { _remaining.Remove(control); return false; }
        if (pressed) { _remaining[control] = delay; return true; }
        if (!_remaining.TryGetValue(control, out var remaining)) return false;
        remaining -= double.IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;
        bool due = remaining <= 1e-9;
        // At most one repeat per observation; skip missed deadlines instead of replaying a burst.
        if (due) remaining += (Math.Floor(-remaining / interval) + 1) * interval;
        _remaining[control] = remaining;
        return due;
    }
}
