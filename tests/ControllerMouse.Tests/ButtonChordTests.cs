using ControllerMouse.Core;
using System.Text.Json;

internal static class ButtonChordTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool value) { if (!value) throw new Exception("Button combination assertion failed."); }
        static ButtonChord Pair(Control first = Control.A, Control second = Control.B) => new(first, second, new(BindingKind.Shortcut, Shortcut: Shortcut.OnScreenKeyboard));
        static Gamepad Pad(params Control[] controls)
        {
            var pad = default(Gamepad);
            foreach (var c in controls) pad = c switch
            {
                Control.LeftTrigger => pad with { LeftTrigger = 255 },
                Control.RightTrigger => pad with { RightTrigger = 255 },
                _ => pad with { Buttons = pad.Buttons | ControlMapping.ButtonFlag(c) }
            };
            return pad;
        }
        static (InputProcessor Engine, Recorder Sink) Ready(params ButtonChord[] chords)
        {
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = new Settings { ButtonChords = chords } };
            engine.Process(default(Gamepad), .01); return (engine, sink);
        }
        test("simultaneous and staggered combinations fire once without individual mouse clicks", () =>
        {
            foreach (bool staggered in new[] { false, true })
            {
                var (engine, sink) = Ready(Pair());
                if (staggered) for (int i = 0; i < 10; i++) engine.Process(Pad(Control.A), .01);
                for (int i = 0; i < 100; i++) engine.Process(Pad(Control.A, Control.B), .01);
                Check(sink.Events.SequenceEqual(new[] { "OnScreenKeyboard" }));
                engine.Process(Pad(Control.A), .01); engine.Process(Pad(Control.A, Control.B), .01);
                Check(sink.Events.Count == 1);
                engine.Process(default(Gamepad), .01); engine.Process(Pad(Control.A, Control.B), .01);
                Check(sink.Events.Count == 2);
            }
        });
        test("every supported control can participate and reserved pause wins", () =>
        {
            foreach (var option in ControlMapping.Controls)
            {
                var other = option.Control == Control.A ? Control.B : Control.A;
                var (engine, sink) = Ready(Pair(option.Control, other));
                engine.Process(Pad(option.Control, other), .01); engine.Process(default(Gamepad), .01); engine.Process(default(Gamepad), .01);
                Check(sink.Events.SequenceEqual(new[] { "OnScreenKeyboard" }));
            }
            var (paused, recorder) = Ready(Pair(Control.A, Control.Back));
            paused.Process(Pad(Control.Back), .01); paused.Process(Pad(Control.Back, Control.Start, Control.A), .01);
            Check(!paused.Enabled && recorder.Events.Count == 0);
        });
        test("single tap is retained and a hold starts after pairing time", () =>
        {
            var (engine, sink) = Ready(Pair());
            engine.Process(Pad(Control.A), .01); engine.Process(default(Gamepad), .01); engine.Process(default(Gamepad), .01);
            Check(sink.Events.SequenceEqual(new[] { "Left:True", "Left:False" })); sink.Events.Clear();
            for (int i = 0; i < 30; i++) engine.Process(Pad(Control.A), .01);
            Check(sink.Events.SequenceEqual(new[] { "Left:True" }));
            engine.Process(Pad(Control.A, Control.B), .01);
            Check(!sink.Events.Contains("OnScreenKeyboard"));
            engine.Process(default(Gamepad), .01); engine.Process(default(Gamepad), .01);
            Check(sink.Events.Contains("Left:False"));
        });
        test("nonparticipant mappings stay immediate and bumpers are suppressed in a combination", () =>
        {
            var (engine, sink) = Ready(Pair(Control.LeftShoulder, Control.RightShoulder));
            engine.Process(Pad(Control.A), .01); Check(sink.Events.Single() == "Left:True");
            engine.Process(default(Gamepad), .01); sink.Events.Clear();
            engine.Settings = engine.Settings with { Smoothing = 0 }; engine.Process(default(Gamepad), .01);
            for (int i = 0; i < 100; i++) engine.Process(Pad(Control.LeftShoulder, Control.RightShoulder) with { LeftX = 32767 }, .01);
            Check(sink.Events.Count(e => e == "OnScreenKeyboard") == 1 && Math.Abs(sink.X - 1100) < 2);
        });
        test("pending combinations are discarded on pause disconnect and settings changes", () =>
        {
            foreach (int reset in new[] { 0, 1, 2 })
            {
                var (engine, sink) = Ready(Pair()); engine.Process(Pad(Control.A), .01);
                if (reset == 0) { engine.SetEnabled(false); engine.SetEnabled(true); }
                if (reset == 1) engine.Process(null, .01);
                if (reset == 2) engine.Settings = engine.Settings;
                engine.Process(Pad(Control.A, Control.B), .01); engine.Process(default(Gamepad), .01); engine.Process(default(Gamepad), .01);
                Check(sink.Events.Count == 0);
            }
        });
        test("trigger hysteresis retains a pending partner and a consumed chord", () =>
        {
            var (engine, sink) = Ready(Pair(Control.LeftTrigger, Control.RightTrigger));
            engine.Process(Pad(Control.LeftTrigger), .01);
            engine.Process(Pad(Control.RightTrigger) with { LeftTrigger = 40 }, .01);
            for (int i = 0; i < 10; i++) engine.Process(default(Gamepad) with { LeftTrigger = 40, RightTrigger = 40 }, .01);
            Check(sink.Events.SequenceEqual(new[] { "OnScreenKeyboard" }));
        });
        test("overlapping chords use stable saved order and disjoint chords can both fire", () =>
        {
            var router = new ButtonChordRouter(); var fired = new List<ControlBinding>();
            var first = Pair(); var second = new ButtonChord(Control.A, Control.X, new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierView));
            router.Route(Pad(Control.A, Control.B, Control.X), new[] { first, second }, .22, .01, fired.Add);
            Check(fired.Count == 1 && fired[0] == first.Action);
            router.Reset(); fired.Clear();
            second = second with { First = Control.Y };
            router.Route(Pad(Control.A, Control.B, Control.X, Control.Y), new[] { first, second }, .22, .01, fired.Add);
            Check(fired.Count == 2);
        });
        test("chord keyboard action sends one complete tap and rapid single taps stay distinct", () =>
        {
            var (engine, sink) = Ready(new ButtonChord(Control.A, Control.B, new(BindingKind.Keyboard, Key: 65, Modifiers: 1)));
            engine.Process(Pad(Control.A, Control.B), .01); engine.Process(default(Gamepad), .01);
            Check(sink.Events.Single() == "Keys:17,65"); sink.Events.Clear();
            for (int i = 0; i < 3; i++) { engine.Process(Pad(Control.A), .01); engine.Process(default(Gamepad), .01); }
            engine.Process(default(Gamepad), .01);
            Check(sink.Events.Count == 6);
        });
        test("new accessibility actions use the documented key sequences", () =>
        {
            foreach (var (action, last) in new[] { (Shortcut.MagnifierFullScreen, 0x46), (Shortcut.MagnifierLens, 0x4C),
                (Shortcut.MagnifierDocked, 0x44), (Shortcut.MagnifierInvert, 0x49) })
                Check(AccessibilityCatalog.Keys(action).SequenceEqual(new ushort[] { 0x11, 0x12, (ushort)last }));
            Check(AccessibilityCatalog.Keys(Shortcut.ScreenSnipping).SequenceEqual(new ushort[] { 0x5B, 0x10, 0x53 }));
        });
        test("profile exports retain combinations and invalid imported combinations are rejected", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "relay-chord-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ProfileStore.Export(path, new("test", "Test", new Settings { ButtonChords = [Pair()], ChordWindowMilliseconds = 380 }));
                var imported = ProfileStore.Import(path);
                Check(imported.Settings.ButtonChords!.Single() == Pair() && imported.Settings.ChordWindowMilliseconds == 380);
                File.WriteAllText(path, JsonSerializer.Serialize(new ProfileExport(1, "Invalid", new Settings { ButtonChords = [Pair(Control.Back, Control.Start)] })));
                bool rejected = false; try { ProfileStore.Import(path); } catch (JsonException) { rejected = true; }
                Check(rejected);
            }
            finally { File.Delete(path); }
        });
        test("chord validation rejects duplicates reversed pairs reserved and unsupported actions", () =>
        {
            Check(!Pair(Control.A, Control.A).IsValid && !Pair(Control.Back, Control.Start).IsValid);
            Check(!new ButtonChord(Control.A, Control.B, new(BindingKind.Mouse)).IsValid);
            Check(!ButtonChord.ValidList([Pair(), Pair(Control.B, Control.A)]));
            var cleaned = new Settings { ButtonChords = [Pair(), Pair(Control.B, Control.A), Pair(Control.Back, Control.Start)], ChordWindowMilliseconds = double.NaN }.Validate();
            Check(cleaned.ButtonChords!.Length == 1 && cleaned.ChordWindowMilliseconds == 220);
            var json = JsonSerializer.Serialize(cleaned); var restored = JsonSerializer.Deserialize<Settings>(json)!;
            Check(restored.ButtonChords!.Single() == Pair());
        });
    }
}
