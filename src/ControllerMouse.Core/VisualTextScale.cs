namespace ControllerMouse.Core;

/// <summary>Windows text size is a minimum, independent of Relay's saved reading preference.</summary>
public static class VisualTextScale
{
    public static double Resolve(double relay, double windows) => Math.Max(
        double.IsFinite(relay) ? Math.Clamp(relay, 1, 2) : 1,
        double.IsFinite(windows) ? Math.Clamp(windows, 1, 2.25) : 1);
}
