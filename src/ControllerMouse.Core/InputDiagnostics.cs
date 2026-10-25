namespace ControllerMouse.Core;

public enum DiagnosticLevel { Info, Warning, Error, Trace }
public sealed record InputDiagnosticEvent(DateTimeOffset Time, DiagnosticLevel Level, string Stage, string Detail);

/// <summary>Opt-in, bounded logical-input diagnostics. Never stores typed text or screen content.</summary>
public sealed class InputDiagnostics
{
    private readonly object _gate = new();
    private readonly Queue<InputDiagnosticEvent> _events = [];
    private bool _enabled;
    public const int Capacity = 1000;
    public bool Enabled { get { lock (_gate) return _enabled; } set { lock (_gate) _enabled = value; } }
    public void Write(DiagnosticLevel level, string stage, string detail)
    {
        lock (_gate)
        {
            if (!_enabled) return;
            if (_events.Count == Capacity) _events.Dequeue();
            _events.Enqueue(new(DateTimeOffset.Now, level, stage, detail));
        }
    }
    public InputDiagnosticEvent[] Snapshot() { lock (_gate) return _events.ToArray(); }
    public void Clear() { lock (_gate) _events.Clear(); }
}
