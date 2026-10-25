namespace ControllerMouse.Core;

public enum SetupInputMethod { Controller, Switches, Pedals }

public sealed record SetupChoices
{
    public SetupInputMethod InputMethod { get; init; } = SetupInputMethod.Controller;
    // Null keeps an existing reading preference unchanged when setup is reapplied.
    public bool? SimpleControls { get; init; }
    public bool LargerText { get; init; }
    public bool Magnifier { get; init; }
    public bool ReadAloud { get; init; }
    public bool SteadyMovement { get; init; }
    public bool OneButton { get; init; }
    public bool Keyboard { get; init; } = true;
    public bool Captions { get; init; }
    public bool VoiceTyping { get; init; }
    public bool Quiet { get; init; }
    public bool LearnMovement { get; init; }
}

public static class SetupPlan
{
    public static Settings Build(SetupChoices choices, Settings preferences)
    {
        if (choices.InputMethod is SetupInputMethod.Switches or SetupInputMethod.Pedals)
            return BuildAdaptive(choices, preferences);
        var mappings = new Dictionary<Control, ControlBinding>();
        var chords = new List<ButtonChord>();
        void Tool(Control control, Shortcut action) => mappings[control] = new(BindingKind.Shortcut, Shortcut: action);
        void Pair(Control first, Control second, Shortcut action) => chords.Add(new(first, second, new(BindingKind.Shortcut, Shortcut: action)));
        Tool(Control.Back, Shortcut.AccessibilityHub);
        Tool(Control.LeftTrigger, choices.Magnifier ? Shortcut.ZoomOut : Shortcut.None);
        Tool(Control.RightTrigger, choices.Magnifier ? Shortcut.ZoomIn : Shortcut.None);
        Tool(Control.LeftStick, choices.Keyboard ? Shortcut.OnScreenKeyboard : Shortcut.None);
        Tool(Control.RightStick, choices.ReadAloud ? Shortcut.Narrator : Shortcut.AccessibilityHub);
        Tool(Control.Y, choices.Magnifier ? Shortcut.CloseMagnifier : choices.Keyboard ? Shortcut.OnScreenKeyboard : Shortcut.AccessibilityHub);
        if (choices.OneButton)
        {
            if (choices.Magnifier) { Tool(Control.Y, Shortcut.MagnifierView); Tool(Control.Back, Shortcut.CloseMagnifier); }
            if (choices.Captions) Tool(Control.LeftShoulder, Shortcut.LiveCaptions);
            if (choices.VoiceTyping) Tool(Control.RightShoulder, Shortcut.VoiceTyping);
        }
        else
        {
            if (choices.Keyboard) Pair(Control.A, Control.B, Shortcut.OnScreenKeyboard);
            if (choices.Magnifier) Pair(Control.LeftShoulder, Control.RightShoulder, Shortcut.MagnifierView);
            if (choices.Captions) Pair(Control.X, Control.Y, Shortcut.LiveCaptions);
            if (choices.VoiceTyping) Pair(Control.LeftStick, Control.RightStick, Shortcut.VoiceTyping);
        }
        var settings = new Settings
        {
            ButtonMappings = mappings,
            ButtonChords = chords.ToArray(),
            PointerSpeed = choices.SteadyMovement || choices.Magnifier || choices.OneButton ? 700 : 1000,
            ScrollSpeed = 4,
            ScrollMode = choices.OneButton ? ScrollMode.Step : ScrollMode.Smooth,
            ScrollAxisLock = true,
            ScrollPrecision = choices.OneButton,
            TremorStabilization = choices.SteadyMovement,
            StabilizationStrength = .55,
            ScrollStabilization = choices.SteadyMovement,
            ClickHoldMilliseconds = 0,
            Haptics = !choices.Quiet,
            MagnifierHaptics = !choices.Quiet,
            MotionLearning = choices.LearnMovement ? LearningMode.Learn : LearningMode.Off,
            ControllerType = preferences.ControllerType,
            AdaptiveAccessories = preferences.AdaptiveAccessories,
            ChordWindowMilliseconds = 300
        }.WithInterfacePreferences(preferences);
        return WithDisplayChoices(settings, choices);
    }

    private static Settings AdaptiveBase(SetupInputMethod inputMethod) => AdaptiveAccessibilityProfiles.All.Single(
        preset => preset.Id == (inputMethod == SetupInputMethod.Pedals ? "access-adaptive-pedals" : "access-adaptive-switches")).Settings;

    private static Settings BuildAdaptive(SetupChoices choices, Settings preferences)
    {
        var baseline = AdaptiveBase(choices.InputMethod);
        var mappings = new Dictionary<Control, ControlBinding>(baseline.ButtonMappings!);
        // Four navigation switches stay fixed. Additional tools are available in
        // Relay's Accessibility Hub; optional spare inputs never replace A/B/X/Y.
        mappings[Control.LeftStick] = new();
        mappings[Control.RightStick] = new();
        bool pedalZoom = choices.InputMethod == SetupInputMethod.Pedals || choices.Magnifier;
        mappings[Control.LeftTrigger] = pedalZoom ? new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomOut) : new();
        mappings[Control.RightTrigger] = pedalZoom ? new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn) : new();
        var menuTool = choices.ReadAloud ? Shortcut.Narrator : choices.Keyboard ? Shortcut.OnScreenKeyboard :
            choices.VoiceTyping ? Shortcut.VoiceTyping : choices.Captions ? Shortcut.LiveCaptions :
            choices.Magnifier ? Shortcut.MagnifierStart : Shortcut.StartMenu;
        mappings[Control.Start] = new(BindingKind.Shortcut, Shortcut: menuTool);
        var settings = (baseline with
        {
            ButtonMappings = mappings,
            ButtonChords = [],
            MotionLearning = LearningMode.Off,
            TargetAssist = false,
            TremorStabilization = choices.SteadyMovement,
            ScrollStabilization = choices.SteadyMovement,
            ControllerType = preferences.ControllerType,
            AdaptiveAccessories = preferences.AdaptiveAccessories
        }).WithInterfacePreferences(preferences);
        return WithDisplayChoices(settings, choices);
    }

    private static Settings WithDisplayChoices(Settings settings, SetupChoices choices)
    {
        if (choices.SimpleControls is bool simple)
            settings = settings with { VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = simple } };
        return (settings with { InterfaceScale = choices.LargerText ? Math.Max(1.5, settings.InterfaceScale) : settings.InterfaceScale }).Validate();
    }

    public static Settings Navigation(Settings preferences, SetupInputMethod inputMethod = SetupInputMethod.Controller)
    {
        if (inputMethod is SetupInputMethod.Switches or SetupInputMethod.Pedals)
        {
            var baseline = AdaptiveBase(inputMethod);
            // Setup itself uses only selection, tabbing and arrows. Pedals must
            // not operate Magnifier, nor may a spare input launch tools here.
            var mappings = baseline.ButtonMappings!.ToDictionary(pair => pair.Key, pair =>
                pair.Value.Kind == BindingKind.Shortcut && pair.Value.Shortcut is not (Shortcut.Up or Shortcut.Down or Shortcut.Left or Shortcut.Right)
                    ? new ControlBinding() : pair.Value);
            return (baseline with
            {
                ButtonMappings = mappings,
                ButtonChords = [],
                MotionLearning = LearningMode.Off,
                TargetAssist = false,
                ControllerType = preferences.ControllerType,
                AdaptiveAccessories = preferences.AdaptiveAccessories
            }).WithInterfacePreferences(preferences).Validate();
        }
        return new Settings
        {
            PointerSpeed = 700,
            Haptics = false,
            MagnifierHaptics = false,
            ButtonMappings = ControlMapping.Controls.ToDictionary(c => c.Control, c => c.Control switch
            {
                Control.A => new ControlBinding(BindingKind.Mouse),
                Control.B => new ControlBinding(BindingKind.Mouse, MouseButton.Right),
                Control.Up => new ControlBinding(BindingKind.Pointer, Key: 38),
                Control.Down => new ControlBinding(BindingKind.Pointer, Key: 40),
                Control.Left => new ControlBinding(BindingKind.Pointer, Key: 37),
                Control.Right => new ControlBinding(BindingKind.Pointer, Key: 39),
                _ => new ControlBinding()
            }),
            ControllerType = preferences.ControllerType,
            AdaptiveAccessories = preferences.AdaptiveAccessories
        }.WithInterfacePreferences(preferences).Validate();
    }
}
