using ControllerMouse.Core;

internal static class PausedNavigationTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }
    private static Gamepad Pad(Buttons buttons = Buttons.None, short x = 0, short ry = 0, byte lt = 0, byte rt = 0) =>
        new(buttons, lt, rt, x, 0, 0, ry);
    private static Settings Switches() => new()
    {
        NarratorNavigation = false,
        ButtonMappings = new()
        {
            [Control.A] = new(BindingKind.Keyboard, Key: 13),
            [Control.B] = new(BindingKind.Keyboard, Key: 9),
            [Control.X] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
            [Control.Y] = new(BindingKind.Keyboard, Key: 27)
        }
    };
    private static (InputProcessor Engine, Recorder Sink, List<RelayNavigationCommand> Commands) Paused(Settings? settings = null)
    {
        var sink = new Recorder(); var commands = new List<RelayNavigationCommand>();
        var engine = new InputProcessor(sink)
        {
            Settings = settings ?? Switches(),
            RelayWindowActive = true,
            PausedNavigationWindowActive = true,
            RelayNavigationRequested = commands.Add
        };
        engine.SetEnabled(false); engine.Process(Pad(), .008);
        return (engine, sink, commands);
    }
    private static void Tap(InputProcessor engine, Buttons button)
    {
        engine.Process(Pad(button), .008); engine.Process(Pad(), .008);
    }

    public static void Run(Action<string, Action> test)
    {
        test("Paused four switches navigate only the owned Relay configuration UI without enabling output", () =>
        {
            var (engine, sink, commands) = Paused();
            foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.X, Buttons.Y })
            {
                for (int frame = 0; frame < 125; frame++) engine.Process(Pad(button), .008);
                engine.Process(Pad(), .008);
            }
            Check(commands.SequenceEqual([RelayNavigationCommand.Select, RelayNavigationCommand.Next,
                RelayNavigationCommand.Previous, RelayNavigationCommand.Back]));
            Check(!engine.Enabled && engine.Armed && engine.KeyboardNavigationActive && sink.Events.Count == 0);
        });
        test("Paused ordinary controller supports Dpad Select Back and previous next without Narrator", () =>
        {
            var (engine, sink, commands) = Paused(new Settings { NarratorNavigation = false });
            foreach (var button in new[] { Buttons.Up, Buttons.Down, Buttons.Left, Buttons.Right,
                Buttons.A, Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder }) Tap(engine, button);
            Check(commands.SequenceEqual([RelayNavigationCommand.Up, RelayNavigationCommand.Down,
                RelayNavigationCommand.Left, RelayNavigationCommand.Right, RelayNavigationCommand.Select,
                RelayNavigationCommand.Back, RelayNavigationCommand.Previous, RelayNavigationCommand.Next]));
            Check(!engine.Enabled && sink.Events.Count == 0);
        });
        test("Paused editor ignores mapped tools chords mouse movement scrolling and unsafe keys", () =>
        {
            var settings = new Settings
            {
                ButtonMappings = ControlMapping.Controls.ToDictionary(item => item.Control,
                _ => new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.VoiceTyping)),
                ButtonChords = [
                    new(Control.X, Control.Y, new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator)) ]
            };
            var (engine, sink, commands) = Paused(settings);
            foreach (var button in new[] { Buttons.X, Buttons.Y, Buttons.LeftStick, Buttons.RightStick,
                Buttons.Back, Buttons.Start, Buttons.X | Buttons.Y }) Tap(engine, button);
            engine.Process(Pad(x: 32767, ry: 32767, lt: 255, rt: 255), .05);
            Check(sink.Events.Count == 0 && commands.Count == 0 && !engine.ScrollActive && !engine.Enabled);
            engine.Settings = new Settings
            {
                ButtonMappings = new()
                {
                    [Control.X] = new(BindingKind.Keyboard, Key: 9, Modifiers: 8),
                    [Control.Y] = new(BindingKind.Keyboard, Key: 13, Modifiers: 1)
                }
            };
            engine.Process(Pad(), .008); Tap(engine, Buttons.X); Tap(engine, Buttons.Y);
            Check(sink.Events.Count == 0 && commands.Count == 0);
        });
        test("Paused navigation requires explicit WPF ownership and an internal callback with no key fallback", () =>
        {
            var (engine, sink, commands) = Paused();
            engine.PausedNavigationWindowActive = false;
            engine.Process(Pad(), .008); Tap(engine, Buttons.B);
            Check(commands.Count == 0 && sink.Events.Count == 0 && !engine.CanNavigateWhilePaused);
            engine.PausedNavigationWindowActive = true; engine.RelayNavigationRequested = null;
            engine.Process(Pad(), .008); Tap(engine, Buttons.A); Tap(engine, Buttons.B);
            Check(commands.Count == 0 && sink.Events.Count == 0 && !engine.CanNavigateWhilePaused);
            engine.RelayNavigationRequested = command => { commands.Add(command); engine.RelayNavigationRequested = null; };
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A | Buttons.B), .008);
            Check(commands.SequenceEqual([RelayNavigationCommand.Select]) && sink.Events.Count == 0,
                "The second binding fell back to a native key when its callback disappeared in the same frame.");
        });
        test("Paused foreground changes invalidate queued commands and require neutral on editor return", () =>
        {
            var (engine, sink, _) = Paused();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            Tap(engine, Buttons.B); Check(queued.Count == 1);
            engine.RelayWindowActive = false; engine.PausedNavigationWindowActive = false;
            engine.Process(Pad(Buttons.A, 32767), .008);
            engine.Process(Pad(), .008); // Neutral outside Relay is not an editor-entry neutral frame.
            Check(queued.All(item => !engine.IsRelayNavigationCurrent(item.Command, item.Generation)));
            engine.RelayWindowActive = true; engine.PausedNavigationWindowActive = true;
            engine.Process(Pad(Buttons.A), .008);
            Check(!engine.Armed && queued.Count == 1 && sink.Events.Count == 0);
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(queued.Count == 2 && queued[1].Command == RelayNavigationCommand.Select && !engine.Enabled);
        });
        test("Paused editor navigation remains internal when the typing keyboard is also open", () =>
        {
            var (engine, sink, commands) = Paused(); engine.SetKeyboardOpen(true); engine.Process(Pad(), .008);
            Tap(engine, Buttons.B); Tap(engine, Buttons.A); Tap(engine, Buttons.Y);
            Check(commands.SequenceEqual([RelayNavigationCommand.Next, RelayNavigationCommand.Select, RelayNavigationCommand.Back]));
            Check(engine.KeyboardOpen && !engine.Enabled && sink.Events.Count == 0);
            engine.PausedNavigationWindowActive = false; engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(commands.Count == 3 && sink.Events.Count == 0, "The typing window acquired disabled desktop output.");
        });
        test("Paused disconnect profile changes and resume cannot replay held selection", () =>
        {
            var (engine, sink, commands) = Paused();
            Tap(engine, Buttons.B); long generation = engine.FocusNavigationGeneration;
            engine.Process(null, .008); Check(!engine.Armed && engine.FocusNavigationGeneration != generation);
            engine.Process(Pad(Buttons.A), .008); Check(commands.Count == 1 && !engine.Armed);
            engine.Process(Pad(), .008); Tap(engine, Buttons.A); Check(commands.Count == 2);
            engine.Settings = Switches(); engine.Process(Pad(Buttons.A), .008);
            Check(commands.Count == 2 && !engine.Armed);
            engine.SetEnabled(true); engine.PausedNavigationWindowActive = false; engine.RelayWindowActive = false;
            engine.Process(Pad(Buttons.A), .008); Check(!engine.Armed && sink.Events.Count == 0);
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(sink.Events.SequenceEqual(["Keys:13"]) && engine.Enabled);
        });
        test("Paused safety chord remains usable outside Relay and toggles once per neutral cycle", () =>
        {
            var (engine, sink, commands) = Paused();
            engine.RelayWindowActive = false; engine.PausedNavigationWindowActive = false; engine.Process(Pad(), .008);
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.Back | Buttons.Start), .008);
            Check(engine.Enabled && !engine.Armed && commands.Count == 0 && sink.Events.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.Back | Buttons.Start), .008);
            Check(!engine.Enabled && !engine.Armed && sink.Events.Count == 0);
        });
    }
}
