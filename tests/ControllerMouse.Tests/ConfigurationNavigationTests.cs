using ControllerMouse.Core;

internal static class ConfigurationNavigationTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }
    private static Gamepad Pad(Buttons buttons = Buttons.None, byte lt = 0, byte rt = 0,
        short x = 0, short y = 0, short rx = 0, short ry = 0) => new(buttons, lt, rt, x, y, rx, ry);
    private static (InputProcessor Engine, Recorder Sink) Ready(Settings? settings = null)
    {
        var sink = new Recorder();
        var engine = new InputProcessor(sink) { Settings = settings ?? new(), RelayWindowActive = true, ConfigurationMode = true };
        engine.Process(Pad(), .008); return (engine, sink);
    }
    private static void Tap(InputProcessor engine, Gamepad pad)
    {
        engine.Process(pad, .008); engine.Process(Pad(), .008);
    }

    public static void Run(Action<string, Action> test)
    {
        test("Configuration mode supplies keyboard navigation without pointer movement or Narrator", () =>
        {
            var (engine, sink) = Ready(new Settings { NarratorNavigation = false });
            foreach (var button in new[] { Buttons.Up, Buttons.Down, Buttons.Left, Buttons.Right,
                Buttons.A, Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder }) Tap(engine, Pad(button));
            Check(sink.Events.SequenceEqual(["Keys:38", "Keys:40", "Keys:37", "Keys:39", "Keys:13", "Keys:27", "Keys:16,9", "Keys:9"]));
            Check(engine.KeyboardNavigationActive && !engine.NarratorRunning && sink.X == 0 && sink.Y == 0);
        });
        test("Configuration keeps safe fixed switch Enter Tab ShiftTab Escape and arrow mappings", () =>
        {
            var (engine, sink) = Ready(new Settings
            {
                NarratorNavigation = false,
                ButtonMappings = new()
                {
                    [Control.A] = new(BindingKind.Keyboard, Key: 13),
                    [Control.B] = new(BindingKind.Keyboard, Key: 9),
                    [Control.X] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
                    [Control.Y] = new(BindingKind.Keyboard, Key: 27),
                    [Control.LeftTrigger] = new(BindingKind.Keyboard, Key: 38)
                }
            });
            foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.X, Buttons.Y }) Tap(engine, Pad(button));
            Tap(engine, Pad(lt: 100));
            Check(sink.Events.SequenceEqual(["Keys:13", "Keys:9", "Keys:16,9", "Keys:27", "Keys:38"]));
        });
        test("Configuration suppresses Windows shortcuts unsafe keyboard chords stick pointer and scrolling", () =>
        {
            var settings = new Settings
            {
                ButtonMappings = ControlMapping.Controls.ToDictionary(c => c.Control,
                _ => new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.Narrator))
            };
            var (engine, sink) = Ready(settings);
            foreach (var button in new[] { Buttons.X, Buttons.Y, Buttons.LeftStick, Buttons.RightStick, Buttons.Back, Buttons.Start })
                Tap(engine, Pad(button));
            Tap(engine, Pad(lt: 100, rt: 100, x: 32767, y: 32767, rx: 32767, ry: 32767));
            Check(sink.Events.Count == 0 && sink.X == 0 && sink.Y == 0 && sink.Vertical == 0 && sink.Horizontal == 0 && !engine.ScrollActive);
            engine.Settings = new Settings
            {
                ButtonMappings = new()
                {
                    [Control.X] = new(BindingKind.Keyboard, Key: 13, Modifiers: 3),
                    [Control.Y] = new(BindingKind.Keyboard, Key: 9, Modifiers: 1),
                    [Control.LeftStick] = new(BindingKind.Keyboard, Key: 9, Modifiers: 8),
                    [Control.RightStick] = new(BindingKind.Keyboard, Key: 37, Modifiers: 4)
                }
            };
            engine.Process(Pad(), .008);
            foreach (var button in new[] { Buttons.X, Buttons.Y, Buttons.LeftStick, Buttons.RightStick }) Tap(engine, Pad(button));
            Check(sink.Events.Count == 0);
        });
        test("Configuration bypasses custom combination actions and pending single-button delays", () =>
        {
            var (engine, sink) = Ready(new Settings
            {
                ChordWindowMilliseconds = 500,
                ButtonChords = [
                new ButtonChord(Control.A, Control.B, new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator)),
                new ButtonChord(Control.X, Control.Y, new(BindingKind.Keyboard, Key: 72, Modifiers: 8))
            ]
            });
            engine.Process(Pad(Buttons.A), .008); Check(sink.Events.SequenceEqual(["Keys:13"]));
            engine.Process(Pad(Buttons.A | Buttons.B), .008);
            engine.Process(Pad(Buttons.X | Buttons.Y), .008); engine.Process(Pad(), .008);
            Check(sink.Events.SequenceEqual(["Keys:13", "Keys:27"]));
        });
        test("Configuration entry and exit release drag and require neutral before any replay", () =>
        {
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = new Settings { NarratorNavigation = false } };
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008);
            Check(sink.Events.SequenceEqual(["Left:True"]));
            engine.ConfigurationMode = true; engine.Reset(); engine.RelayWindowActive = true;
            Check(!engine.Armed && sink.Events.Last() == "Left:False"); sink.Events.Clear();
            engine.Process(Pad(Buttons.A, x: 32767), .008); Check(!engine.Armed && sink.Events.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008); Check(sink.Events.SequenceEqual(["Keys:13"]));
            engine.ConfigurationMode = false; engine.Reset(); sink.Events.Clear();
            engine.Process(Pad(Buttons.A), .008); Check(!engine.Armed && sink.Events.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008); Check(sink.Events.SequenceEqual(["Left:True"]));
        });
        test("Configuration navigation has priority over an open Relay typing keyboard", () =>
        {
            var (engine, sink) = Ready(); engine.SetKeyboardOpen(true); engine.Process(Pad(), .008);
            foreach (var button in new[] { Buttons.Up, Buttons.A, Buttons.B, Buttons.LeftShoulder }) Tap(engine, Pad(button));
            Tap(engine, Pad(lt: 100, rt: 100, x: 32767, ry: 32767));
            Check(sink.Events.SequenceEqual(["Keys:38", "Keys:13", "Keys:27", "Keys:16,9"]));
            Check(engine.KeyboardOpen && sink.X == 0 && sink.Vertical == 0);
        });
        test("Configuration focus leaving Relay stops output and requires neutral inside Relay", () =>
        {
            var (engine, sink) = Ready(); engine.RelayWindowActive = false;
            engine.Process(Pad(Buttons.A | Buttons.Down, x: 32767, ry: 32767), .008);
            engine.Process(Pad(), .008); // An outside-Relay neutral frame must not re-arm output.
            Check(sink.Events.Count == 0);
            engine.RelayWindowActive = true; engine.Process(Pad(Buttons.A), .008);
            Check(!engine.Armed && sink.Events.Count == 0, "A held before returning to Relay activated a control.");
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008);
            Check(sink.Events.SequenceEqual(["Keys:13"]));
        });
        test("Configuration preserves the reserved pause chord and safe neutral resume", () =>
        {
            var (engine, sink) = Ready();
            engine.Process(Pad(Buttons.Back | Buttons.Start), .008);
            Check(!engine.Enabled && !engine.Armed && sink.Events.Count == 0);
            for (int frame = 0; frame < 100; frame++) engine.Process(Pad(Buttons.Back | Buttons.Start), .008);
            Check(!engine.Enabled && sink.Events.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.Back | Buttons.Start), .008);
            Check(engine.Enabled && !engine.Armed && sink.Events.Count == 0);
            engine.Process(Pad(Buttons.A), .008); Check(sink.Events.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008); Check(sink.Events.SequenceEqual(["Keys:13"]));
        });
    }
}
