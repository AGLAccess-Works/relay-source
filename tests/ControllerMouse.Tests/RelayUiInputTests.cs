using ControllerMouse.Core;
using ControllerMouse.App;

internal static class RelayUiInputTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }
    private static Gamepad Pad(Buttons buttons = Buttons.None, short x = 0, short y = 0, short rx = 0, short ry = 0) =>
        new(buttons, 0, 0, x, y, rx, ry);
    private static (InputProcessor Engine, Recorder Sink, List<RelayNavigationCommand> Commands) Ready(Settings? settings = null)
    {
        var sink = new Recorder(); var commands = new List<RelayNavigationCommand>();
        var engine = new InputProcessor(sink)
        {
            Settings = settings ?? new Settings { Smoothing = 0 },
            RelayWindowActive = true,
            RelayNavigationRequested = commands.Add
        };
        engine.Process(Pad(), .008);
        return (engine, sink, commands);
    }
    private static void Tap(InputProcessor engine, Buttons buttons)
    {
        engine.Process(Pad(buttons), .008); engine.Process(Pad(), .008);
    }

    public static void Run(Action<string, Action> test)
    {
        foreach (double interval in new[] { .008, .02, .1 })
            test($"Relay D-pad sends exactly one internal step per held press at {interval * 1000:0}ms polling", () =>
            {
                var (engine, sink, commands) = Ready();
                for (int frame = 0; frame < Math.Ceiling(2 / interval); frame++) engine.Process(Pad(Buttons.Down), interval);
                Check(commands.SequenceEqual([RelayNavigationCommand.Down]) && sink.Events.Count == 0);
                engine.Process(Pad(), interval); engine.Process(Pad(Buttons.Down), interval);
                Check(commands.SequenceEqual([RelayNavigationCommand.Down, RelayNavigationCommand.Down]) && sink.Events.Count == 0);
            });
        test("Relay rapid D-pad presses preserve every edge in physical order without native keys", () =>
        {
            var (engine, sink, commands) = Ready();
            foreach (var button in new[] { Buttons.Down, Buttons.Down, Buttons.Down, Buttons.Up, Buttons.Up, Buttons.Down }) Tap(engine, button);
            Check(commands.SequenceEqual([RelayNavigationCommand.Down, RelayNavigationCommand.Down, RelayNavigationCommand.Down,
                RelayNavigationCommand.Up, RelayNavigationCommand.Up, RelayNavigationCommand.Down]));
            Check(sink.Events.Count == 0 && engine.Armed);
        });
        test("Relay select back previous and next each have a single navigation owner", () =>
        {
            var (engine, sink, commands) = Ready(); Tap(engine, Buttons.Up); commands.Clear();
            foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder })
            {
                for (int frame = 0; frame < 150; frame++) engine.Process(Pad(button), .008);
                engine.Process(Pad(), .008);
            }
            Check(commands.SequenceEqual([RelayNavigationCommand.Select, RelayNavigationCommand.Back,
                RelayNavigationCommand.Previous, RelayNavigationCommand.Next]) && sink.Events.Count == 0);
        });
        test("Relay navigation does not suppress simultaneous pointer movement and right-stick scrolling", () =>
        {
            var (engine, sink, commands) = Ready();
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.Down, x: 32767, ry: 32767), .008);
            Check(commands.SequenceEqual([RelayNavigationCommand.Down]));
            Check(Math.Abs(sink.X - 1100) <= 1 && sink.Vertical > 800 && engine.Armed && engine.KeyboardNavigationActive);
            Check(!sink.Events.Contains("Down") && !sink.Events.Any(value => value.StartsWith("Keys:")));
        });
        test("Relay stick movement near its normal dead zone restores pointer clicking without rearming", () =>
        {
            var (engine, sink, commands) = Ready(); Tap(engine, Buttons.Down); commands.Clear();
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(x: 8000), .008);
            Check(sink.X > 0 && engine.Armed && !engine.KeyboardNavigationActive);
            Tap(engine, Buttons.A);
            Check(commands.Count == 0 && sink.Events.Contains("Left:True") && sink.Events.Last() == "Left:False");
        });
        test("Relay physical pointer use restores click bindings and never replays a held navigation switch", () =>
        {
            var (engine, sink, commands) = Ready(); Tap(engine, Buttons.Down);
            engine.Process(Pad(Buttons.A), .008); commands.Clear(); sink.Events.Clear();
            engine.NotifyPointerInteraction();
            Check(engine.Armed && !engine.KeyboardNavigationActive);
            for (int frame = 0; frame < 20; frame++) engine.Process(Pad(Buttons.A, x: 32767), .008);
            Check(sink.X > 0 && !sink.Events.Contains("Left:True") && commands.Count == 0);
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(sink.Events.TakeLast(2).SequenceEqual(["Left:True", "Left:False"]));
        });
        test("Queued Relay Select is discarded after physical mouse focus changes without disarming", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            Tap(engine, Buttons.Down); queued.Clear();
            engine.Process(Pad(Buttons.A), .008);
            Check(queued.Count == 1 && queued[0].Command == RelayNavigationCommand.Select);
            long selectionGeneration = queued[0].Generation;
            engine.NotifyPointerInteraction();
            Check(engine.Armed && !engine.KeyboardNavigationActive && engine.NavigationGeneration != selectionGeneration,
                "A pending selection remained valid after the physical mouse changed focus.");
            Check(queued.All(command => command.Generation != engine.NavigationGeneration));
            long pointerGeneration = engine.NavigationGeneration;
            engine.NotifyPointerInteraction();
            Check(engine.NavigationGeneration == pointerGeneration, "Idle pointer observations invalidated unrelated fresh input.");
            for (int frame = 0; frame < 20; frame++) engine.Process(Pad(Buttons.A), .008);
            Check(!sink.Events.Contains("Left:True"), "The old held Select became a pointer click.");
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(sink.Events.TakeLast(2).SequenceEqual(["Left:True", "Left:False"]) && engine.Armed);
        });
        test("Queued Relay Select is discarded after stick returns to pointer control without suppressing movement", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            Tap(engine, Buttons.Down); queued.Clear();
            engine.Process(Pad(Buttons.A), .008);
            Check(queued.Count == 1 && queued[0].Command == RelayNavigationCommand.Select);
            long selectionGeneration = queued[0].Generation;
            for (int frame = 0; frame < 20; frame++) engine.Process(Pad(Buttons.A, x: 32767), .008);
            Check(engine.Armed && !engine.KeyboardNavigationActive && sink.X > 0 &&
                engine.NavigationGeneration != selectionGeneration && !sink.Events.Contains("Left:True"),
                "Stick return replayed a queued/held Select, disarmed input or stopped pointer movement.");
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(sink.Events.TakeLast(2).SequenceEqual(["Left:True", "Left:False"]) && engine.Armed);
        });
        test("Relay rapid released directions share a valid generation until their input mode changes", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            foreach (var button in new[] { Buttons.Down, Buttons.Down, Buttons.Down, Buttons.Up, Buttons.Up, Buttons.Down })
                Tap(engine, button);
            var current = queued.Where(command => engine.IsRelayNavigationCurrent(command.Command, command.Generation)).Select(command => command.Command);
            Check(current.SequenceEqual([RelayNavigationCommand.Down, RelayNavigationCommand.Down, RelayNavigationCommand.Down,
                RelayNavigationCommand.Up, RelayNavigationCommand.Up, RelayNavigationCommand.Down]) &&
                sink.Events.Count == 0 && engine.Armed && engine.KeyboardNavigationActive,
                "An ordinary released D-pad edge invalidated or lost an earlier queued direction.");
        });
        test("Relay navigation survives pre-existing stick jitter across its pointer dead zone", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            engine.Process(Pad(Buttons.Down, x: 6500), .008);
            foreach (short jitter in Enumerable.Repeat(new short[] { 5700, 5900, 6100, 6500, 5600, 6000, 6200 }, 25).SelectMany(values => values))
                engine.Process(Pad(x: jitter), .008);
            Check(queued.Count == 1 && queued[0].Command == RelayNavigationCommand.Down &&
                engine.IsRelayNavigationCurrent(queued[0].Command, queued[0].Generation) && engine.KeyboardNavigationActive && engine.Armed,
                "Existing jitter around the default 18% dead zone cancelled a queued direction or changed button ownership.");
            // Short above-threshold samples may be less than one pixel, and a
            // below-threshold sample correctly clears that fractional movement.
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(x: 6500), .008);
            Check(sink.X > 0, "Navigation suppressed pointer motion above the ordinary pointer dead zone.");
        });
        test("Relay navigation survives direction release with an already-held strong pointer stick", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            for (int frame = 0; frame < 20; frame++) engine.Process(Pad(x: 20000), .008);
            int before = sink.X;
            engine.Process(Pad(Buttons.Down, x: 20000), .008);
            for (int frame = 0; frame < 20; frame++) engine.Process(Pad(x: 20000), .008);
            Check(queued.Count == 1 && engine.IsRelayNavigationCurrent(queued[0].Command, queued[0].Generation) && engine.KeyboardNavigationActive &&
                engine.Armed && sink.X > before, "Existing pointer movement invalidated released D-pad navigation before delayed delivery.");
            Tap(engine, Buttons.A);
            Check(queued.Last().Command == RelayNavigationCommand.Select && !sink.Events.Contains("Left:True"));
        });
        test("Relay navigation neutral hysteresis rearms a fresh deliberate stick gesture and suppresses held Select", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            engine.Process(Pad(Buttons.Down, x: 20000), .008);
            engine.Process(Pad(x: 20000), .008);
            engine.Process(Pad(x: 4500), .008); // Below the 14% neutral band, rather than merely below 18%.
            engine.Process(Pad(Buttons.A), .008);
            long selectGeneration = queued.Last().Generation;
            Check(queued.Last().Command == RelayNavigationCommand.Select && engine.KeyboardNavigationActive);
            engine.Process(Pad(Buttons.A, x: 8000), .008);
            Check(!engine.KeyboardNavigationActive && engine.Armed && engine.NavigationGeneration != selectGeneration &&
                !sink.Events.Contains("Left:True"), "A fresh stick gesture did not return to pointer control safely.");
            engine.Process(Pad(), .008); Tap(engine, Buttons.A);
            Check(sink.Events.TakeLast(2).SequenceEqual(["Left:True", "Left:False"]));
        });
        test("Relay rapid released directions with unchanged stick drift survive delayed dispatcher delivery", () =>
        {
            var (engine, sink, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            foreach (var button in new[] { Buttons.Down, Buttons.Down, Buttons.Down, Buttons.Up, Buttons.Up, Buttons.Down })
            {
                engine.Process(Pad(button, x: 6500), .008);
                engine.Process(Pad(x: 6500), .008);
            }
            var deliverable = queued.Where(command => engine.IsRelayNavigationCurrent(command.Command, command.Generation)).Select(command => command.Command);
            Check(deliverable.SequenceEqual([RelayNavigationCommand.Down, RelayNavigationCommand.Down, RelayNavigationCommand.Down,
                RelayNavigationCommand.Up, RelayNavigationCommand.Up, RelayNavigationCommand.Down]) &&
                engine.Armed && engine.KeyboardNavigationActive && !sink.Events.Any(value => value.StartsWith("Keys:")),
                "Unchanged stick drift caused rapid released directions to vanish before queued focus delivery.");
        });
        foreach (var (command, button) in new (RelayNavigationCommand, Buttons)[] {
            (RelayNavigationCommand.Up, Buttons.Up), (RelayNavigationCommand.Down, Buttons.Down),
            (RelayNavigationCommand.Left, Buttons.Left), (RelayNavigationCommand.Right, Buttons.Right),
            (RelayNavigationCommand.Next, Buttons.RightShoulder), (RelayNavigationCommand.Previous, Buttons.LeftShoulder)
        })
            test($"Queued Relay {command} survives fresh stick movement and release before dispatcher delivery", () =>
            {
                var (engine, sink, _) = Ready();
                var queued = new List<(RelayNavigationCommand Command, long Generation)>();
                engine.RelayNavigationRequested = current => queued.Add((current, engine.CaptureRelayNavigationGeneration(current)));
                Tap(engine, Buttons.Down); queued.Clear();
                long actionGeneration = engine.CaptureRelayNavigationGeneration(RelayNavigationCommand.Select);
                engine.Process(Pad(button), .008);
                for (int frame = 0; frame < 20; frame++) engine.Process(Pad(button, x: 20000), .008);
                engine.Process(Pad(x: 20000), .008);
                Check(queued.Count == 1 && queued[0].Command == command &&
                    engine.IsRelayNavigationCurrent(command, queued[0].Generation) && engine.Armed &&
                    !engine.KeyboardNavigationActive && sink.X > 0,
                    "A produced focus step vanished when a fresh stick gesture returned button ownership before queued delivery.");
                Check(!engine.IsRelayNavigationCurrent(RelayNavigationCommand.Select, actionGeneration),
                    "Focus-step preservation also preserved an unsafe stale selection.");
            });
        test("Physical mouse movement cancels queued Select and Back but preserves an accepted focus step", () =>
        {
            var (engine, _, _) = Ready();
            var queued = new List<(RelayNavigationCommand Command, long Generation)>();
            engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
            Tap(engine, Buttons.Down); Tap(engine, Buttons.A); Tap(engine, Buttons.B);
            engine.NotifyPointerInteraction();
            Check(engine.Armed && !engine.KeyboardNavigationActive && queued.Count == 3);
            Check(engine.IsRelayNavigationCurrent(queued[0].Command, queued[0].Generation) &&
                queued.Skip(1).All(command => !engine.IsRelayNavigationCurrent(command.Command, command.Generation)),
                "A plain mouse move cancelled a focus edge or retargeted a queued Select/Back action.");
        });
        foreach (bool automaticNavigation in new[] { true, false })
            test($"Physical mouse click invalidates every queued command while {(automaticNavigation ? "navigation" : "pointer")} mode remains armed", () =>
            {
                var settings = automaticNavigation ? new Settings { Smoothing = 0 } :
                    AdaptiveAccessibilityProfiles.All.Single(profile => profile.Id == "access-adaptive-switches").Settings;
                var (engine, sink, _) = Ready(settings);
                var queued = new List<(RelayNavigationCommand Command, long Generation)>();
                engine.RelayNavigationRequested = command => queued.Add((command, engine.CaptureRelayNavigationGeneration(command)));
                if (automaticNavigation) { Tap(engine, Buttons.Down); Tap(engine, Buttons.A); }
                else { Tap(engine, Buttons.B); Tap(engine, Buttons.A); }
                Check(queued.Count == 2 && engine.KeyboardNavigationActive == automaticNavigation);
                engine.NotifyPointerInteraction(clicked: true);
                Check(engine.Armed && !engine.KeyboardNavigationActive && sink.Events.Count == 0 &&
                    queued.All(command => !engine.IsRelayNavigationCurrent(command.Command, command.Generation)),
                    "A physical click left a queued focus/selection command valid after it could focus another control.");
            });
        test("Relay pointer motion remains continuous while D-pad button ownership changes", () =>
        {
            var (engine, sink, commands) = Ready(new Settings { Smoothing = .025 });
            for (int frame = 0; frame < 100; frame++) engine.Process(Pad(x: 32767), .008);
            int before = sink.X;
            engine.Process(Pad(Buttons.Down, x: 32767), .008);
            int entering = sink.X - before; before = sink.X;
            engine.Process(Pad(x: 32767), .008);
            Check(entering >= 8 && sink.X - before >= 8 && engine.Armed && commands.Count == 1);
        });
        test("Fixed Adaptive switches route safe navigation internally without changing saved meanings", () =>
        {
            var preset = AdaptiveAccessibilityProfiles.All.Single(profile => profile.Id == "access-adaptive-switches");
            var (engine, sink, commands) = Ready(preset.Settings);
            foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.X }) Tap(engine, button);
            Check(commands.SequenceEqual([RelayNavigationCommand.Select, RelayNavigationCommand.Next, RelayNavigationCommand.Previous]));
            Check(sink.Events.Count == 0 && !engine.KeyboardNavigationActive && !engine.Settings.NarratorNavigation);
        });
        test("Relay preserves non-navigation shortcuts and modified keyboard mappings", () =>
        {
            var (engine, sink, commands) = Ready(new Settings
            {
                NarratorNavigation = false,
                ButtonMappings = new()
                {
                    [Control.X] = new(BindingKind.Shortcut, Shortcut: Shortcut.VoiceTyping),
                    [Control.Y] = new(BindingKind.Keyboard, Key: 38, Modifiers: 1),
                    [Control.LeftStick] = new(BindingKind.Keyboard, Key: 9, Modifiers: 2)
                }
            });
            foreach (var button in new[] { Buttons.X, Buttons.Y, Buttons.LeftStick }) Tap(engine, button);
            Check(commands.Count == 0 && sink.Events.SequenceEqual(["VoiceTyping", "Keys:17,38", "Keys:18,9"]));
        });
        test("A saved repeating navigation key is edge-only in Relay and keeps its Windows repeat outside", () =>
        {
            var (engine, sink, commands) = Ready(new Settings
            {
                NarratorNavigation = false,
                ButtonMappings = new()
                {
                    [Control.X] = new(BindingKind.Keyboard, Key: 9) { Repeat = true }
                }
            });
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.X), .008);
            Check(commands.SequenceEqual([RelayNavigationCommand.Next]) && sink.Events.Count == 0);
            engine.Process(Pad(), .008); commands.Clear(); engine.RelayWindowActive = false;
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.X), .008);
            Check(commands.Count == 0 && sink.Events.Count(value => value == "Keys:9") > 1);
        });
        test("Relay typing keyboard retains its separate navigation and cursor-key repeat", () =>
        {
            var (engine, sink, commands) = Ready(); engine.SetKeyboardOpen(true); engine.Process(Pad(), .008);
            Tap(engine, Buttons.Up); Tap(engine, Buttons.A);
            for (int frame = 0; frame < 100; frame++) engine.Process(Pad(Buttons.LeftShoulder), .008);
            Check(commands.Count == 0 && sink.Events.Take(2).SequenceEqual(["KeyboardUp", "KeyboardSelect"]));
            Check(sink.Events.Count(value => value == "Keys:37") > 1);
        });
        test("Outside Relay Narrator arrow repeats and pointer switching retain their existing behavior", () =>
        {
            var (engine, sink, commands) = Ready(); engine.RelayWindowActive = false; engine.NarratorRunning = true;
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.Down, x: 32767), .008);
            Check(commands.Count == 0 && sink.Events.Count(value => value == "Down") > 1 && sink.X == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(x: 32767), .02);
            Check(!engine.KeyboardNavigationActive && sink.X > 0);
        });
        test("Relay disconnect pause and unsafe reconnect retain neutral safeguards", () =>
        {
            var (engine, sink, commands) = Ready(); Tap(engine, Buttons.Down); commands.Clear();
            engine.Process(Pad(Buttons.Start | Buttons.Back), .008);
            Check(!engine.Enabled && !engine.Armed && commands.Count == 0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.Start | Buttons.Back), .008);
            Check(engine.Enabled && !engine.Armed && commands.Count == 0);
            engine.Process(Pad(Buttons.Down), .008); Check(commands.Count == 0);
            engine.Process(Pad(), .008); Tap(engine, Buttons.Down); Check(commands.Count == 1);
            engine.Process(null, .008); commands.Clear(); sink.Events.Clear();
            engine.Process(Pad(Buttons.Down, x: 32767), .008);
            Check(!engine.Armed && commands.Count == 0 && sink.Events.Count == 0);
            engine.Process(Pad(), .008); Tap(engine, Buttons.Down); Check(commands.Count == 1 && engine.Armed);
        });
        test("Relay mouse tagging preserves signed movement wheel data and owned release retry", () =>
        {
            var submitted = new List<NativeMethods.Input>(); bool rejectRelease = true;
            var sink = new WindowsInputSink(inputs =>
            {
                submitted.AddRange(inputs);
                return inputs[0].Type == 0 && inputs[0].Data.Mouse.Flags == 4 && rejectRelease ? 0u : (uint)inputs.Length;
            });
            sink.Move(7, -4); sink.Scroll(-120, 240); sink.SetMouseButton(MouseButton.Left, true);
            sink.SetMouseButton(MouseButton.Left, false);
            Check(sink.CleanupPending && sink.HasHeldInputs);
            int beforeBlockedMove = submitted.Count; sink.Move(100, 100);
            Check(submitted.Skip(beforeBlockedMove).All(input => input.Data.Mouse.Flags == 4),
                "A tagged pointer move bypassed owned-release quarantine.");
            rejectRelease = false; sink.RetryReleases();
            Check(!sink.HasHeldInputs && !sink.CleanupPending);
            Check(submitted.Select(input => input.Data.Mouse.Flags).SequenceEqual(new uint[] { 1, 0x0800, 0x1000, 2, 4, 4, 4 }));
            Check(submitted.All(input => input.Type == 0 && input.Data.Mouse.Extra.ToUInt64() == WindowsInputSink.RelayMouseMarker));
            Check(submitted[0].Data.Mouse.X == 7 && submitted[0].Data.Mouse.Y == -4);
            Check(unchecked((int)submitted[1].Data.Mouse.Data) == -120 && submitted[2].Data.Mouse.Data == 240);
            submitted.Clear(); sink.InvokeKeys([13]);
            Check(submitted.Count == 2 && submitted.All(input => input.Type == 1 && input.Data.Keyboard.Extra == UIntPtr.Zero));
        });
        foreach (var (name, cancel) in new (string, Action<InputProcessor>)[] {
            ("pause and resume", engine => { engine.SetEnabled(false); engine.SetEnabled(true); }),
            ("profile settings", engine => engine.Settings = new Settings()),
            ("explicit reset", engine => engine.Reset()),
            ("typing keyboard open and close", engine => { engine.SetKeyboardOpen(true); engine.SetKeyboardOpen(false); }),
            ("disconnect and reconnect", engine => { engine.Process(null, .008); engine.Process(Pad(), .008); }),
            ("owned-input quarantine", engine => engine.InvalidateRelayNavigation())
        })
            test($"Queued Relay navigation is invalidated by {name} even after input becomes ready again", () =>
            {
                var (engine, _, _) = Ready(); var generations = new List<long>();
                engine.RelayNavigationRequested = command => generations.Add(engine.CaptureRelayNavigationGeneration(command));
                Tap(engine, Buttons.Down); Check(generations.Count == 1);
                long queued = generations[0]; cancel(engine); engine.Process(Pad(), .008);
                Check(engine.Enabled && engine.Armed && !engine.IsRelayNavigationCurrent(RelayNavigationCommand.Down, queued));
                Tap(engine, Buttons.Down);
                Check(generations.Count == 2 && engine.IsRelayNavigationCurrent(RelayNavigationCommand.Down, generations[1]) && generations[1] != queued);
            });
    }
}
