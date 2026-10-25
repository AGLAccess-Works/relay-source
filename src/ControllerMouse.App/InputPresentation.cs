namespace ControllerMouse.App;

/// <summary>A read-only view of the existing input engine for Relay's selectable diagrams.</summary>
public sealed record InputPresentation(
    string Id, string Name, string Action, string State, string Type,
    bool IsActive, bool Connected, bool CanMap, bool Repeat,
    string? Port = null, string? AccessoryName = null,
    double X = 0, double Y = 0, double Value = 0);
