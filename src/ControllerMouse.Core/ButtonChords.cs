namespace ControllerMouse.Core;

public sealed record ButtonChord(Control First, Control Second, ControlBinding Action)
{
    public int Mask => (1 << (int)First) | (1 << (int)Second);
    public bool IsValid => Enum.IsDefined(First) && Enum.IsDefined(Second) && First != Second &&
        !(First is Control.Back or Control.Start && Second is Control.Back or Control.Start) &&
        Action is { IsValid: true, Kind: BindingKind.Shortcut or BindingKind.Keyboard } &&
        !Action.Repeat &&
        !(Action.Kind == BindingKind.Shortcut && Action.Shortcut == Shortcut.None);
    public static bool ValidList(ButtonChord[]? chords) => chords == null ||
        chords.Length <= 32 && chords.All(c => c is { IsValid: true }) && chords.Select(c => c.Mask).Distinct().Count() == chords.Length;
}

// Pending buttons cannot produce individual actions until the pairing window expires.
// Once an individual action has started it cannot be converted into a chord.
public sealed class ButtonChordRouter
{
    private readonly double[] _age = new double[16];
    private int _physical, _pending, _committed;
    private readonly List<int> _latched = [];
    public void Reset() { _physical = _pending = _committed = 0; _latched.Clear(); Array.Clear(_age); }
    public Gamepad Route(Gamepad pad, IReadOnlyList<ButtonChord> chords, double window, double dt, Action<ControlBinding> fire)
    {
        if (chords.Count == 0) return pad;
        int physical = 0, participating = 0;
        foreach (var c in chords) participating |= c.Mask;
        physical = InputStateManager.HeldMask(pad, _physical);
        _latched.RemoveAll(mask => (physical & mask) == 0);
        int suppressed = _latched.Aggregate(0, (mask, next) => mask | next);
        int released = _physical & ~physical;
        int tapped = _pending & released & ~suppressed;
        _pending &= physical; _committed &= physical;
        for (int i = 0; i < 16; i++)
        {
            int bit = 1 << i;
            if ((physical & ~_physical & participating & ~suppressed & bit) != 0) { _pending |= bit; _age[i] = 0; }
            else if ((_pending & bit) != 0) _age[i] += dt;
        }
        foreach (var chord in chords)
        {
            int mask = chord.Mask;
            if ((_pending & mask) != mask || (suppressed & mask) != 0 ||
                _age[(int)chord.First] > window || _age[(int)chord.Second] > window) continue;
            _pending &= ~mask; suppressed |= mask; _latched.Add(mask); fire(chord.Action);
        }
        for (int i = 0; i < 16; i++)
        {
            int bit = 1 << i;
            if ((_pending & bit) != 0 && _age[i] >= window) { _pending &= ~bit; _committed |= bit; }
        }
        int effective = (physical & ~participating) | ((_committed | tapped) & ~suppressed);
        _physical = physical;
        var result = pad with { Buttons = Buttons.None, LeftTrigger = 0, RightTrigger = 0 };
        foreach (var option in ControlMapping.Controls)
        {
            if ((effective & (1 << (int)option.Control)) == 0) continue;
            if (option.Control == Control.LeftTrigger) result = result with { LeftTrigger = 255 };
            else if (option.Control == Control.RightTrigger) result = result with { RightTrigger = 255 };
            else result = result with { Buttons = result.Buttons | ControlMapping.ButtonFlag(option.Control) };
        }
        return result;
    }
}
