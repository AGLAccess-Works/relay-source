using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControllerMouse.Core;

public sealed record Settings
{
    public AdaptiveAccessory[]? AdaptiveAccessories { get; init; }
    public ControllerPreference ControllerType { get; init; }
    public bool NarratorNavigation { get; init; } = true;
    public LearningMode MotionLearning { get; init; }
    public double DpadSpeed { get; init; } = 140;
    public double RepeatDelayMilliseconds { get; init; } = 400;
    public double RepeatIntervalMilliseconds { get; init; } = 80;
    public ButtonChord[]? ButtonChords { get; init; }
    public double ChordWindowMilliseconds { get; init; } = 220;
    public Dictionary<Control, ControlBinding>? ButtonMappings { get; init; }
    public double PointerSpeed { get; init; } = 1100;
    public double ScrollSpeed { get; init; } = 8;
    public ScrollMode ScrollMode { get; init; } = ScrollMode.Smooth;
    public double ScrollDeadZone { get; init; } = .18;
    public double ScrollCurve { get; init; } = 1.3;
    public double ScrollSmoothing { get; init; } = .025;
    public bool ScrollAxisLock { get; init; }
    public bool ScrollStabilization { get; init; }
    public bool ScrollModifiers { get; init; } = true;
    public bool ScrollPrecision { get; init; }
    public double HapticStrength { get; init; } = .5;
    public bool MagnifierHaptics { get; init; } = true;
    public bool ScrollHaptics { get; init; }
    public bool TargetHaptics { get; init; }
    public bool BoundaryHaptics { get; init; }
    public double DeadZone { get; init; } = 0.18;
    public double ResponseCurve { get; init; } = 1.65;
    public double Smoothing { get; init; } = 0.025;
    public bool TremorStabilization { get; init; }
    public double StabilizationStrength { get; init; } = .65;
    public double ClickHoldMilliseconds { get; init; }
    public bool TargetAssist { get; init; }
    public double TargetAssistStrength { get; init; } = .6;
    public bool RememberTargets { get; init; }
    public bool ShowTargetHint { get; init; } = true;
    public Shortcut YAction { get; init; } = Shortcut.CloseMagnifier;
    public Shortcut LeftStickAction { get; init; } = Shortcut.OnScreenKeyboard;
    public Shortcut RightStickAction { get; init; } = Shortcut.AccessibilityHub;
    public bool InvertScroll { get; init; }
    public bool Haptics { get; init; } = true;
    public bool LightTheme { get; init; } = true;
    // Null retains the legacy light/dark and motion preferences until explicitly changed.
    public VisualAccessPreferences? VisualAccess { get; init; }
    public bool ShowControllerLabels { get; init; }
    public bool ReduceInterfaceMotion { get; init; }
    public bool InputAnnouncements { get; init; }
    public bool ReadHoveredText { get; init; } = true;
    public double InterfaceScale { get; init; } = 1;
    public bool MinimizeToTray { get; init; } = true;

    [JsonIgnore]
    public VisualAccessPreferences EffectiveVisualAccess => (VisualAccess ??
        (VisualAccessPreferences.ForScheme(LightTheme ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark) with
        { Motion = ReduceInterfaceMotion ? VisualMotion.Reduced : VisualMotion.Standard })).Validate();

    public Settings WithInterfacePreferences(Settings preferences) => this with
    {
        InterfaceScale = preferences.InterfaceScale,
        LightTheme = preferences.LightTheme,
        VisualAccess = preferences.EffectiveVisualAccess,
        MinimizeToTray = preferences.MinimizeToTray,
        ShowControllerLabels = preferences.ShowControllerLabels,
        ReduceInterfaceMotion = preferences.ReduceInterfaceMotion,
        InputAnnouncements = preferences.InputAnnouncements,
        ReadHoveredText = preferences.ReadHoveredText
    };

    public Settings WithVisualAccessScheme(DisplayScheme scheme)
    {
        var previous = EffectiveVisualAccess;
        var visual = VisualAccessPreferences.ForScheme(scheme) with
        {
            CustomHighlight = previous.CustomHighlight,
            BackgroundColour = previous.BackgroundColour,
            SurfaceColour = previous.SurfaceColour,
            TextColour = previous.TextColour,
            SecondaryTextColour = previous.SecondaryTextColour
        };
        return (this with
        {
            VisualAccess = visual,
            LightTheme = !visual.IsDark,
            ReduceInterfaceMotion = visual.Motion != VisualMotion.Standard,
            InterfaceScale = visual.Scheme == DisplayScheme.CviSimplified ? Math.Max(1.5, Limit(InterfaceScale, 1, 2, 1)) : InterfaceScale
        }).Validate();
    }

    public Settings Validate() => this with
    {
        VisualAccess = VisualAccess?.Validate(),
        AdaptiveAccessories = AdaptiveAccessories == null ? null : AdaptiveAccessory.Validate(AdaptiveAccessories),
        ControllerType = Enum.IsDefined(ControllerType) ? ControllerType : ControllerPreference.Automatic,
        MotionLearning = Enum.IsDefined(MotionLearning) ? MotionLearning : LearningMode.Off,
        DpadSpeed = Limit(DpadSpeed, 40, 500, 140),
        RepeatDelayMilliseconds = Limit(RepeatDelayMilliseconds, 100, 2000, 400),
        RepeatIntervalMilliseconds = Limit(RepeatIntervalMilliseconds, 40, 1000, 80),
        ButtonChords = ButtonChords?.Where(c => c is { IsValid: true }).DistinctBy(c => c.Mask).Take(32).ToArray(),
        ChordWindowMilliseconds = Limit(ChordWindowMilliseconds, 80, 500, 220),
        InterfaceScale = Limit(InterfaceScale, 1, 2, 1),
        ButtonMappings = ButtonMappings?.Where(p => Enum.IsDefined(p.Key) && p.Value is { IsValid: true } &&
            !(p.Key is Control.Back or Control.Start && (p.Value.Kind is BindingKind.Precision or BindingKind.Boost or BindingKind.Pointer || p.Value.Repeat)))
            .ToDictionary(p => p.Key, p => p.Value),
        PointerSpeed = Limit(PointerSpeed, 200, 2600, 1100),
        ScrollSpeed = Limit(ScrollSpeed, 1, 24, 8),
        ScrollMode = Enum.IsDefined(ScrollMode) ? ScrollMode : ScrollMode.Smooth,
        ScrollDeadZone = Limit(ScrollDeadZone, .05, .4, .18),
        ScrollCurve = Limit(ScrollCurve, 1, 3, 1.3),
        ScrollSmoothing = Limit(ScrollSmoothing, 0, .15, .025),
        HapticStrength = Limit(HapticStrength, 0, 1, .5),
        DeadZone = Limit(DeadZone, 0.05, 0.4, 0.18),
        ResponseCurve = Limit(ResponseCurve, 1, 3, 1.65),
        Smoothing = Limit(Smoothing, 0, 0.1, 0.025),
        StabilizationStrength = Limit(StabilizationStrength, 0, 1, .65),
        ClickHoldMilliseconds = Limit(ClickHoldMilliseconds, 0, 500, 0),
        TargetAssistStrength = Limit(TargetAssistStrength, 0, 1, .6),
        YAction = AccessibilityCatalog.IsQuickAction(YAction) ? YAction : Shortcut.CloseMagnifier,
        LeftStickAction = AccessibilityCatalog.IsQuickAction(LeftStickAction) ? LeftStickAction : Shortcut.OnScreenKeyboard,
        RightStickAction = AccessibilityCatalog.IsQuickAction(RightStickAction) ? RightStickAction : Shortcut.AccessibilityHub
    };

    private static double Limit(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Path { get; } = path;
    public string? Warning { get; private set; }

    public Settings Load()
    {
        Warning = null;
        try
        {
            return File.Exists(Path)
                ? (JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? throw new JsonException()).Validate()
                : new Settings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Warning = "Your saved settings could not be read. Defaults are in use; saving will replace the settings file.";
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        string temporary = Path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Validate(), JsonOptions));
            File.Move(temporary, Path, true);
            Warning = null;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
