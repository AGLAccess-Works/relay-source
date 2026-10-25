namespace ControllerMouse.Core;

public enum HapticCue { Scroll, Target, Magnifier, Boundary }
public readonly record struct MotorOutput(ushort Left, ushort Right);

/// <summary>One expiring pattern, no queued or blocking vibration work.</summary>
public sealed class HapticScheduler
{
    private HapticCue? _cue;
    private double _start, _strength, _last = double.NegativeInfinity;
    public bool Request(HapticCue cue, double now, double strength)
    {
        if (!double.IsFinite(now) || !double.IsFinite(strength) || strength <= 0) return false;
        bool active = _cue != null && now - _start < Duration(_cue.Value);
        if ((active && cue <= _cue) || now - _last < .25) return false;
        _cue = cue; _start = _last = now; _strength = Math.Clamp(strength, 0, 1);
        return true;
    }
    public MotorOutput Sample(double now)
    {
        if (_cue == null) return default;
        double age = now - _start;
        if (!double.IsFinite(age) || age < 0 || age >= Duration(_cue.Value)) { _cue = null; return default; }
        if (_cue == HapticCue.Boundary && age is >= .055 and < .13) return default;
        var (left, right) = _cue switch
        {
            HapticCue.Scroll => (0d, .6),
            HapticCue.Target => (.18, .65),
            HapticCue.Boundary => (.35, .5),
            _ => (.21, .36)
        };
        return new((ushort)Math.Round(left * _strength * ushort.MaxValue), (ushort)Math.Round(right * _strength * ushort.MaxValue));
    }
    public void Stop() { _cue = null; _last = double.NegativeInfinity; }
    private static double Duration(HapticCue cue) => cue switch { HapticCue.Scroll => .05, HapticCue.Target => .085, HapticCue.Boundary => .185, _ => .07 };
}

/// <summary>Only emits a boundary on observed movement to an end, never merely because nothing moved.</summary>
public sealed class NavigationCueTracker
{
    private string? _hover, _announced, _region;
    private double _hoverSince, _vertical = -1, _horizontal = -1;
    private int _vDirection, _hDirection;
    public void Reset() { _hover = _announced = _region = null; _vertical = _horizontal = -1; _vDirection = _hDirection = 0; }
    public bool Hover(string? identity, double now)
    {
        if (identity != _hover) { _hover = identity; _hoverSince = now; _announced = null; }
        if (identity == null || identity == _announced || now - _hoverSince < .12) return false;
        _announced = identity; return true;
    }
    public bool Boundary(string? region, double vertical, double horizontal, int verticalDirection, int horizontalDirection)
    {
        bool same = region != null && region == _region;
        bool hit = same && ((_vDirection == verticalDirection && Reached(_vertical, vertical, verticalDirection, true)) ||
            (_hDirection == horizontalDirection && Reached(_horizontal, horizontal, horizontalDirection, false)));
        _region = region; _vertical = vertical; _horizontal = horizontal;
        _vDirection = verticalDirection; _hDirection = horizontalDirection;
        return hit;
    }
    private static bool Reached(double before, double after, int direction, bool vertical)
    {
        if (direction == 0 || !double.IsFinite(before) || !double.IsFinite(after) || before < -.000001 || before > 100.000001 || after < -.000001 || after > 100.000001) return false;
        bool decreasing = vertical ? direction > 0 : direction < 0;
        return decreasing ? before > .01 && after <= .01 : before < 99.99 && after >= 99.99;
    }
}
