namespace ControllerMouse.Core;

public enum ScrollMode { Smooth, Compatible, Step }
public sealed record ScrollModeOption(ScrollMode Mode, string Name);
public readonly record struct ScrollDelta(int Vertical, int Horizontal);

/// <summary>Right-stick rate control, independent of pointer filtering and Windows.</summary>
public sealed class ScrollProcessor
{
    private bool _active;
    private int _axis;
    private readonly Axis _x = new(), _y = new();
    public bool Active => _active;
    public void Reset() { _active = false; _axis = 0; _x.Reset(); _y.Reset(); }

    public ScrollDelta Process(Gamepad pad, Settings settings, double elapsed)
    {
        double dt = double.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05) : 0;
        double x = pad.RightX / (pad.RightX < 0 ? 32768d : 32767d);
        double y = pad.RightY / (pad.RightY < 0 ? 32768d : 32767d);
        double magnitude = Math.Sqrt(x * x + y * y);
        double engage = settings.ScrollMode == ScrollMode.Step ? Math.Max(.35, settings.ScrollDeadZone) : settings.ScrollDeadZone;
        double release = Math.Max(.01, engage - .04);
        if (magnitude <= release || (!_active && magnitude <= engage)) { Reset(); return default; }
        _active = true;
        if (settings.ScrollAxisLock)
        {
            int axis = _axis;
            if (_axis == 0) _axis = Math.Abs(y) >= Math.Abs(x) ? 2 : 1;
            else if (_axis == 2 && Math.Abs(x) > Math.Abs(y) * 1.8) _axis = 1;
            else if (_axis == 1 && Math.Abs(y) > Math.Abs(x) * 1.8) _axis = 2;
            if (axis != _axis) { _x.Reset(); _y.Reset(); }
            if (_axis == 2) x = 0; else y = 0;
        }
        // Stable mapping across the hysteresis band; no gain jump at the engage threshold.
        double shaped = Math.Pow(Math.Clamp((magnitude - release) / (1 - release), 0, 1), settings.ScrollCurve);
        x = x / magnitude * shaped; y = y / magnitude * shaped;
        double multiplier = settings.ScrollPrecision || (settings.ScrollModifiers && ControlMapping.Modifier(pad, settings, BindingKind.Precision))
            ? .35 : settings.ScrollModifiers && ControlMapping.Modifier(pad, settings, BindingKind.Boost) ? 1.8 : 1;
        double rate = settings.ScrollSpeed * 120 * multiplier * (settings.InvertScroll ? -1 : 1);
        double tau = settings.ScrollSmoothing + (settings.ScrollStabilization ? .12 * (1 - .5 * Math.Min(1, magnitude)) : 0);
        return new ScrollDelta(_y.Process(y * rate, dt, tau, settings.ScrollMode),
            _x.Process(x * rate, dt, tau, settings.ScrollMode));
    }

    private sealed class Axis
    {
        private double _velocity, _fraction, _repeat;
        private int _direction;
        public void Reset() { _velocity = _fraction = _repeat = 0; _direction = 0; }
        public int Process(double target, double dt, double tau, ScrollMode mode)
        {
            int direction = Math.Sign(target);
            if (direction == 0) { Reset(); return 0; }
            bool fresh = direction != _direction;
            if (fresh) { Reset(); _direction = direction; }
            if (dt == 0) return 0;
            if (mode == ScrollMode.Step)
            {
                if (fresh) { _repeat = .35; return direction * 120; }
                _repeat -= dt;
                if (_repeat > 0) return 0;
                _repeat += 1 / Math.Clamp(Math.Abs(target) / 120, 1, 12);
                return direction * 120;
            }
            double blend = tau <= 0 ? 1 : 1 - Math.Exp(-dt / tau);
            _fraction += target * dt + (_velocity - target) * tau * blend;
            _velocity += (target - _velocity) * blend;
            int quantum = mode == ScrollMode.Compatible ? 120 : 1;
            int delta = (int)(_fraction / quantum) * quantum;
            _fraction -= delta;
            return delta;
        }
    }
}
