using System.Text.Json;
using ControllerMouse.Core;

internal static class AdaptiveProfileTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    private static Gamepad Pad(Buttons buttons = Buttons.None, byte leftTrigger = 0, byte rightTrigger = 0,
        short x = 0, short y = 0) => new(buttons, leftTrigger, rightTrigger, x, y, 0, 0);

    private static (InputProcessor Engine, Recorder Sink) Ready(string id, bool narrator = false)
    {
        var preset = AdaptiveAccessibilityProfiles.All.Single(p => p.Id == id);
        var sink = new Recorder();
        var engine = new InputProcessor(sink) { Settings = preset.Settings, NarratorRunning = narrator };
        engine.Process(Pad(), .008);
        return (engine, sink);
    }

    public static void Run(Action<string, Action> test)
    {
        test("D-pad keeps ordinary desktop pointer movement when Relay and Narrator are inactive", () =>
        {
            var sink = new Recorder();
            var engine = new InputProcessor(sink);
            engine.Process(Pad(), .008);
            Check(!engine.RelayWindowActive);
            engine.Process(Pad(Buttons.Right), .04);
            Check(sink.X > 0 && !engine.KeyboardNavigationActive && !sink.Events.Contains("Right"));
        });

        test("Relay foreground enables arrow select and Tab navigation without Narrator or pointer movement", () =>
        {
            var sink = new Recorder();
            var engine = new InputProcessor(sink) { RelayWindowActive = true };
            engine.Process(Pad(), .008);
            foreach (var button in new[] { Buttons.Up, Buttons.Down, Buttons.Left, Buttons.Right,
                Buttons.A, Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder })
            {
                engine.Process(Pad(button), .008);
                engine.Process(Pad(), .008);
            }
            Check(engine.KeyboardNavigationActive && !engine.NarratorRunning);
            Check(sink.Events.SequenceEqual(new[] { "Up", "Down", "Left", "Right", "Keys:13", "Keys:27", "Keys:16,9", "Keys:9" }));
            Check(sink.X == 0 && sink.Y == 0);
        });

        test("Entering Relay navigation releases drag without turning a held switch into Enter", () =>
        {
            var sink = new Recorder();
            var engine = new InputProcessor(sink);
            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.A), .008);
            engine.RelayWindowActive = true;
            engine.Process(Pad(Buttons.A | Buttons.Up), .008);
            Check(engine.KeyboardNavigationActive && sink.Events.Contains("Left:False"));
            Check(!sink.Events.Contains("Keys:13") && sink.X == 0 && sink.Y == 0);
        });

        test("Leaving Relay foreground restores pointer control only after held navigation controls are released", () =>
        {
            var sink = new Recorder();
            var engine = new InputProcessor(sink) { RelayWindowActive = true };
            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.Up), .008);
            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.A), .008);
            sink.Events.Clear();
            engine.RelayWindowActive = false;
            engine.Process(Pad(Buttons.A | Buttons.Right), .04);
            Check(!engine.KeyboardNavigationActive && sink.Events.Count == 0);
            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.A | Buttons.Right), .04);
            Check(sink.X > 0 && sink.Events.Contains("Left:True") && !sink.Events.Contains("Keys:13"));
            engine.Process(Pad(), .008);
            Check(sink.Events.Last() == "Left:False");
        });

        test("Narrator continues keyboard navigation after Relay loses foreground", () =>
        {
            var sink = new Recorder();
            var engine = new InputProcessor(sink) { RelayWindowActive = true, NarratorRunning = true };
            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.Up), .008);
            engine.Process(Pad(), .008);
            sink.Events.Clear();
            engine.RelayWindowActive = false;
            engine.Process(Pad(Buttons.A), .008);
            Check(engine.KeyboardNavigationActive && sink.Events.SequenceEqual(new[] { "Keys:13" }));
        });

        test("Relay foreground does not override the fixed Adaptive starter switch meanings", () =>
        {
            foreach (var preset in AdaptiveAccessibilityProfiles.All)
            {
                var (engine, sink) = Ready(preset.Id);
                engine.RelayWindowActive = true;
                engine.Process(Pad(Buttons.Right), .04);
                Check(!engine.KeyboardNavigationActive);
                engine.Process(Pad(), .008);
                sink.Events.Clear();
                engine.Process(Pad(Buttons.B), .008);
                engine.Process(Pad(), .008);
                var expected = preset.Id == "access-adaptive-pointer" ? new[] { "Right:True", "Right:False" } : new[] { "Keys:9" };
                Check(sink.Events.SequenceEqual(expected));
            }
        });

        test("Safe navigation test bumpers match previous and next control order", () =>
        {
            var settings = AccessibilityProfiles.NavigationTest.Settings;
            Check(ControlMapping.Resolve(settings, Control.LeftShoulder).Keys().SequenceEqual(new ushort[] { 16, 9 }));
            Check(ControlMapping.Resolve(settings, Control.RightShoulder).Keys().SequenceEqual(new ushort[] { 9 }));
        });

        test("Adaptive starters keep existing preset order and unique stable identities", () =>
        {
            Check(AccessibilityProfiles.All.Take(4).Select(p => p.Id).SequenceEqual(
                new[] { "access-magnifier", "access-reader", "access-light", "access-quiet" }));
            Check(AccessibilityProfiles.All.Skip(4).SequenceEqual(AdaptiveAccessibilityProfiles.All));
            Check(AccessibilityProfiles.All.Select(p => p.Id).Distinct().Count() == AccessibilityProfiles.All.Count);
            Check(AdaptiveAccessibilityProfiles.All.Count == 3);
        });

        test("Adaptive starters validate with all controls explicit and no invented accessory presence", () =>
        {
            foreach (var preset in AdaptiveAccessibilityProfiles.All)
            {
                var validated = preset.Settings.Validate();
                Check(validated.ButtonMappings!.Count == ControlMapping.Controls.Count);
                Check(validated.ButtonMappings.All(p => p.Value == preset.Settings.ButtonMappings![p.Key]));
                Check(validated.ControllerType == ControllerPreference.Adaptive && validated.InterfaceScale >= 1.5);
                Check(validated.AdaptiveAccessories == null && validated.ButtonChords is { Length: 0 });
                Check(!validated.NarratorNavigation && !validated.Haptics && !validated.MagnifierHaptics);
                Check(!validated.ScrollHaptics && !validated.TargetHaptics && !validated.BoundaryHaptics);
                Check(!validated.TargetAssist && validated.MotionLearning == LearningMode.Off);
                Check(validated.ButtonMappings.Values.All(b => !b.Repeat && b.Kind is not (BindingKind.Precision or BindingKind.Boost)));
                Check(validated.ButtonMappings.Values.All(b => b.Shortcut is not (Shortcut.Narrator or Shortcut.NarratorStart or Shortcut.NarratorStop)));
            }
        });

        foreach (bool narrator in new[] { false, true })
            test($"Adaptive menu switches work without a stick and keep their meanings with Narrator {narrator}", () =>
            {
                var (engine, sink) = Ready("access-adaptive-switches", narrator);
                engine.Process(Pad(Buttons.Up), .008);
                Check(!engine.KeyboardNavigationActive);
                engine.Process(Pad(), .008);
                sink.Events.Clear();
                foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.X, Buttons.Y })
                {
                    for (int i = 0; i < 100; i++) engine.Process(Pad(button), .008);
                    engine.Process(Pad(), .008);
                }
                Check(sink.Events.SequenceEqual(new[] { "Keys:13", "Keys:9", "Keys:16,9", "Keys:27" }));
                Check(sink.X == 0 && sink.Y == 0);
            });

        test("Adaptive menu switches produce arrows instead of moving the old mouse pointer", () =>
        {
            var (engine, sink) = Ready("access-adaptive-switches", true);
            foreach (var button in new[] { Buttons.Up, Buttons.Down, Buttons.Left, Buttons.Right })
            {
                engine.Process(Pad(button), .008);
                engine.Process(Pad(), .008);
            }
            Check(sink.Events.SequenceEqual(new[] { "Up", "Down", "Left", "Right" }));
            Check(sink.X == 0 && sink.Y == 0);
        });

        test("Adaptive joystick preset preserves pointer and click switch when Narrator is running", () =>
        {
            var (engine, sink) = Ready("access-adaptive-pointer", true);
            engine.Process(Pad(Buttons.Right), .04);
            engine.Process(Pad(), .008);
            Check(sink.X > 0 && !engine.KeyboardNavigationActive);
            sink.Events.Clear();
            engine.Process(Pad(Buttons.A, x: 32767), .04);
            engine.Process(Pad(Buttons.A), .008);
            engine.Process(Pad(), .008);
            Check(sink.Events.Count(e => e == "Left:True") == 1 && sink.Events.Last() == "Left:False");
            Check(!sink.Events.Any(e => e.StartsWith("Keys:")));
        });

        test("Adaptive reading pedals zoom once per press with trigger hysteresis and no combinations", () =>
        {
            var (engine, sink) = Ready("access-adaptive-pedals", true);
            foreach (byte value in new byte[] { 100, 40, 70, 36 })
                engine.Process(Pad(leftTrigger: value, rightTrigger: value), .008);
            Check(sink.Events.SequenceEqual(new[] { "ZoomOut", "ZoomIn" }));
            engine.Process(Pad(), .008);
            engine.Process(Pad(leftTrigger: 100, rightTrigger: 100), .008);
            Check(sink.Events.SequenceEqual(new[] { "ZoomOut", "ZoomIn", "ZoomOut", "ZoomIn" }));
            Check(sink.X == 0 && sink.Y == 0 && !engine.KeyboardNavigationActive);
        });

        test("Adaptive presets round trip without losing deliberate switch mappings", () =>
        {
            foreach (var preset in AdaptiveAccessibilityProfiles.All)
            {
                var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(preset.Settings))!.Validate();
                Check(restored.ControllerType == preset.Settings.ControllerType && !restored.NarratorNavigation);
                Check(restored.ButtonMappings!.All(p => p.Value == preset.Settings.ButtonMappings![p.Key]));
                Check(restored.AdaptiveAccessories == null && restored.ButtonChords is { Length: 0 });
            }
        });

        test("Adding available Adaptive starters does not rewrite completed profile libraries", () =>
        {
            var custom = new Settings
            {
                InterfaceScale = 1.75,
                AdaptiveAccessories = [new("A", "My chin switch", AccessoryKind.Switch, [AccessorySignal.X])],
                ButtonMappings = new() { [Control.X] = new(BindingKind.Keyboard, Key: 32) }
            };
            var original = new Profile("my-switches", "My switches", custom);
            var library = new ProfileLibrary(1, original.Id, [original]) { AccessibilityRevision = 1, SetupCompleted = true };
            Check(ReferenceEquals(library.WithAccessibilityDefaults(), library));
            Check(library.Active.Settings.AdaptiveAccessories![0].Signals.SequenceEqual(new[] { AccessorySignal.X }));
            Check(library.Active.Settings.ButtonMappings![Control.X].Key == 32);
            Check(library.Profiles.Length == 1 && library.SetupCompleted);
        });
    }
}
