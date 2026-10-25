namespace ControllerMouse.Core;

/// <summary>Suggested socket layouts. These do not record or detect attached accessories.</summary>
public static class AdaptiveAccessibilityProfiles
{
    public static IReadOnlyList<AccessibilityPreset> All { get; } =
    [
        new("access-adaptive-switches", "Adaptive: switches for menus",
            "Use switches in the A, B, X and Y sockets. A selects. B moves to the next item. X moves to the previous item. Y goes back. The D-pad uses arrow keys. No joystick or button combinations are needed. Open Set up accessories to record your switches.",
            NavigationSettings()),
        new("access-adaptive-pointer", "Adaptive: joystick and click switch",
            "Use a compatible joystick in the left USB port and a click switch in the A socket. Move the joystick to move the pointer. A clicks; hold A to drag. B is right-click. X moves to the next item; Y moves to the previous item. The D-pad moves the pointer slowly. Open Set up accessories to record your joystick and switch.",
            PointerSettings()),
        new("access-adaptive-pedals", "Adaptive: pedals for reading",
            "Use the LT socket for a zoom-out pedal and the RT socket for a zoom-in pedal. Open Magnifier in Relay, then press a pedal once for each zoom step. A selects. B moves to the next item. X moves to the previous item. Y goes back. The D-pad uses arrow keys. Open Set up accessories to record your pedals.",
            NavigationSettings() with { ButtonMappings = ReadingBindings() })
    ];

    private static Settings BaseSettings() => new()
    {
        // The recorded accessory list stays empty until the user supplies their actual setup.
        ControllerType = ControllerPreference.Adaptive,
        NarratorNavigation = false,
        InterfaceScale = 1.5,
        PointerSpeed = 550,
        DpadSpeed = 90,
        ScrollSpeed = 3,
        ScrollMode = ScrollMode.Step,
        ScrollPrecision = true,
        ScrollAxisLock = true,
        ResponseCurve = 2,
        ClickHoldMilliseconds = 0,
        MotionLearning = LearningMode.Off,
        TargetAssist = false,
        Haptics = false,
        MagnifierHaptics = false,
        ScrollHaptics = false,
        TargetHaptics = false,
        BoundaryHaptics = false,
        ButtonChords = []
    };

    private static Dictionary<Control, ControlBinding> NavigationBindings() => new()
    {
        [Control.A] = new(BindingKind.Keyboard, Key: 13),
        [Control.B] = new(BindingKind.Keyboard, Key: 9),
        [Control.X] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
        [Control.Y] = new(BindingKind.Keyboard, Key: 27),
        [Control.Up] = new(BindingKind.Shortcut, Shortcut: Shortcut.Up),
        [Control.Down] = new(BindingKind.Shortcut, Shortcut: Shortcut.Down),
        [Control.Left] = new(BindingKind.Shortcut, Shortcut: Shortcut.Left),
        [Control.Right] = new(BindingKind.Shortcut, Shortcut: Shortcut.Right),
        [Control.LeftShoulder] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
        [Control.RightShoulder] = new(BindingKind.Keyboard, Key: 9),
        [Control.LeftStick] = new(BindingKind.Shortcut, Shortcut: Shortcut.OnScreenKeyboard),
        [Control.RightStick] = new(BindingKind.Shortcut, Shortcut: Shortcut.AccessibilityHub),
        [Control.LeftTrigger] = new(),
        [Control.RightTrigger] = new(),
        [Control.Back] = new(BindingKind.Shortcut, Shortcut: Shortcut.AccessibilityHub),
        [Control.Start] = new(BindingKind.Shortcut, Shortcut: Shortcut.StartMenu)
    };

    private static Settings NavigationSettings() => BaseSettings() with { ButtonMappings = NavigationBindings() };

    private static Dictionary<Control, ControlBinding> ReadingBindings()
    {
        var bindings = NavigationBindings();
        bindings[Control.LeftTrigger] = new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomOut);
        bindings[Control.RightTrigger] = new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn);
        bindings[Control.RightStick] = new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierRead);
        return bindings;
    }

    private static Settings PointerSettings()
    {
        var bindings = NavigationBindings();
        bindings[Control.A] = new(BindingKind.Mouse, MouseButton.Left);
        bindings[Control.B] = new(BindingKind.Mouse, MouseButton.Right);
        bindings[Control.X] = new(BindingKind.Keyboard, Key: 9);
        bindings[Control.Y] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4);
        bindings[Control.Up] = new(BindingKind.Pointer, Key: 38);
        bindings[Control.Down] = new(BindingKind.Pointer, Key: 40);
        bindings[Control.Left] = new(BindingKind.Pointer, Key: 37);
        bindings[Control.Right] = new(BindingKind.Pointer, Key: 39);
        return BaseSettings() with { ButtonMappings = bindings };
    }
}
