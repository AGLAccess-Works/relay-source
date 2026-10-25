namespace ControllerMouse.Core;

public enum AccessoryKind { Switch, Button, Pedal, Joystick, Other }
public enum AccessorySignal { A, B, X, Y, LeftStick, RightStick, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, Up, Down, Left, Right, Back, Start, LeftAxis, RightAxis, Xbox }
public sealed record AccessoryPort(string Id, string Name, AccessorySignal DefaultSignal);
public sealed record AdaptiveAccessory(string Port, string Name, AccessoryKind Kind, AccessorySignal[] Signals)
{
    public static IReadOnlyList<AccessoryPort> Ports { get; } = [
        ..ControlMapping.Controls.Select(c=>new AccessoryPort(c.Control.ToString(),c.Code+" socket",(AccessorySignal)c.Control)),
        new("Xbox","Xbox button socket",AccessorySignal.Xbox),new("X1","X1 joystick socket",AccessorySignal.LeftAxis),new("X2","X2 joystick socket",AccessorySignal.RightAxis),
        new("USBLeft","Left USB port",AccessorySignal.LeftAxis),new("USBRight","Right USB port",AccessorySignal.RightAxis)];
    public static string SignalName(AccessorySignal signal) => signal switch
    {
        AccessorySignal.LeftAxis => "Left stick movement",
        AccessorySignal.RightAxis => "Right stick movement",
        AccessorySignal.Xbox => "Xbox button (Windows reserved)",
        _ => ControlMapping.Controls.First(c => (int)c.Control == (int)signal).Name
    };
    public static bool Active(AccessorySignal signal, Gamepad pad) => signal switch
    {
        AccessorySignal.LeftAxis => InputProcessor.Shape(pad.LeftX, pad.LeftY, .2, 1) != (0d, 0d),
        AccessorySignal.RightAxis => InputProcessor.Shape(pad.RightX, pad.RightY, .2, 1) != (0d, 0d),
        AccessorySignal.Xbox => false,
        _ => (InputStateManager.HeldMask(pad, 0) & (1 << (int)signal)) != 0
    };
    public static AdaptiveAccessory[] Validate(IEnumerable<AdaptiveAccessory>? values) => values?.Where(a => a != null && Ports.Any(p => p.Id == a.Port) && Enum.IsDefined(a.Kind))
        .Select(a => a with
        {
            Name = string.IsNullOrWhiteSpace(a.Name) ? a.Kind.ToString() : new string(a.Name.Where(c => !char.IsControl(c)).Take(60).ToArray()).Trim(),
            Signals = (a.Signals ?? []).Where(s => Enum.IsDefined(s)).Distinct().Take(19).ToArray()
        })
        .DistinctBy(a => a.Port).Take(21).ToArray() ?? [];
}
/// <summary>Records observed logical inputs, never physical accessory presence or identity.</summary>
public sealed class AccessoryCapture
{
    public bool Armed { get; private set; }
    public AccessorySignal? Observe(Gamepad? input)
    {
        if (input is not { } pad) { Armed = false; return null; }
        var active = Enum.GetValues<AccessorySignal>().Where(s => AdaptiveAccessory.Active(s, pad)).ToArray();
        if (!Armed) { Armed = active.Length == 0; return null; }
        if (active.Length == 0) return null;
        Armed = false;
        return active.Length == 1 ? active[0] : null;
    }
}
