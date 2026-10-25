namespace ControllerMouse.Core;

public sealed record AccessibilityPreset(string Id, string Name, string Description, Settings Settings);

public static class AccessibilityProfiles
{
    public static AccessibilityPreset NavigationTest { get; } = new("access-test-navigation", "Safe navigation test",
        "Pointer on the primary stick; A/B left/right click; D-pad arrow keys; X Enter; Y Escape; left bumper previous control, right bumper next control. Left-stick press toggles Narrator; right-stick press starts Magnifier; triggers zoom once per press. No combinations or vibration.",
        new Settings
        {
            PointerSpeed = 700,
            ScrollSpeed = 4,
            ScrollAxisLock = true,
            Haptics = false,
            ButtonChords = [],
            ButtonMappings = new()
            {
                [Control.A] = new(BindingKind.Mouse, MouseButton.Left),
                [Control.B] = new(BindingKind.Mouse, MouseButton.Right),
                [Control.Up] = new(BindingKind.Shortcut, Shortcut: Shortcut.Up),
                [Control.Down] = new(BindingKind.Shortcut, Shortcut: Shortcut.Down),
                [Control.Left] = new(BindingKind.Shortcut, Shortcut: Shortcut.Left),
                [Control.Right] = new(BindingKind.Shortcut, Shortcut: Shortcut.Right),
                [Control.X] = new(BindingKind.Keyboard, Key: 13),
                [Control.Y] = new(BindingKind.Keyboard, Key: 27),
                [Control.LeftShoulder] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
                [Control.RightShoulder] = new(BindingKind.Keyboard, Key: 9),
                [Control.LeftStick] = new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator),
                [Control.RightStick] = new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierStart),
                [Control.LeftTrigger] = new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomOut),
                [Control.RightTrigger] = new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn),
                [Control.Back] = new(BindingKind.Shortcut, Shortcut: Shortcut.AccessibilityHub),
                [Control.Start] = new(BindingKind.Shortcut, Shortcut: Shortcut.StartMenu)
            }
        });
    public static IReadOnlyList<AccessibilityPreset> All { get; } =
    [
        new("access-magnifier", "Magnifier", "Slower pointer. Triggers zoom; Y closes Magnifier; right-stick press reads or pauses. Left-stick press opens the Relay keyboard.",
            new Settings { PointerSpeed = 650, ScrollSpeed = 4, ResponseCurve = 2, ScrollAxisLock = true,
                RightStickAction = Shortcut.MagnifierRead }),
        new("access-reader", "Screen reader navigation", "Bumpers send previous / next Tab, X sends Enter, and Y toggles Narrator. D-pad moves the mouse pointer. Selecting this profile does not start Narrator.",
            new Settings { PointerSpeed = 750, ScrollMode = ScrollMode.Step, ButtonMappings = new()
            {
                [Control.LeftShoulder] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
                [Control.RightShoulder] = new(BindingKind.Keyboard, Key: 9),
                [Control.X] = new(BindingKind.Keyboard, Key: 13),
                [Control.Y] = new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator)
            } }),
        new("access-light", "Light touch", "No extra click confirmation delay. Slow pointer and one scroll step per nudge, with scroll precision always on. Y opens the keyboard. Dragging still requires a held mouse button.",
            new Settings { PointerSpeed = 600, ScrollSpeed = 3, ResponseCurve = 2, ScrollMode = ScrollMode.Step,
                ScrollPrecision = true, ScrollAxisLock = true, ClickHoldMilliseconds = 0, YAction = Shortcut.OnScreenKeyboard }),
        new("access-quiet", "Quiet feedback", "Controller vibration is off. Visual status and target hints remain available.",
            new Settings { Haptics = false, MagnifierHaptics = false, ScrollHaptics = false, TargetHaptics = false, BoundaryHaptics = false }),
        ..AdaptiveAccessibilityProfiles.All
    ];
}
