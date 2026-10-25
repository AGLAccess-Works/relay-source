using ControllerMouse.Core;
using ControllerMouse.App;

static class AssistanceTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
        Gamepad Pad(Buttons buttons = Buttons.None, short x = 0, short y = 0) => new(buttons, 0, 0, x, y, 0, 0);
        (InputProcessor Engine, Recorder Sink) Ready(Settings settings)
        {
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = settings };
            engine.Process(Pad(), .008); return (engine, sink);
        }
        test("stabilization attenuates synthetic 8 Hz transverse jitter", () =>
        {
            double Trace(bool steady)
            {
                var (p, s) = Ready(new Settings { TremorStabilization = steady, StabilizationStrength = .8 });
                double travel = 0;
                for (int i = 0; i < 500; i++)
                {
                    int previous = s.Y;
                    p.Process(Pad(x: 21000, y: (short)(7000 * Math.Sin(2 * Math.PI * 8 * i * .008))), .008);
                    if (i > 125) travel += Math.Abs(s.Y - previous);
                }
                return travel;
            }
            double normal = Trace(false), stable = Trace(true);
            Check(stable < normal * .55, $"Jitter travel: normal {normal}, stabilized {stable}");
            Console.WriteLine($"  Synthetic transverse travel: {normal:0} px → {stable:0} px (not a clinical measure)");
        });
        test("stabilization stays time-correct at different poll rates", () =>
        {
            var a = Ready(new Settings { TremorStabilization = true });
            var b = Ready(new Settings { TremorStabilization = true });
            for (int i = 0; i < 50; i++) a.Engine.Process(Pad(x: 32767), .02);
            for (int i = 0; i < 125; i++) b.Engine.Process(Pad(x: 32767), .008);
            Check(Math.Abs(a.Sink.X - b.Sink.X) <= 1 && a.Sink.X > 950);
        });
        test("stabilization stops at neutral and never continues after pause", () =>
        {
            var (p, s) = Ready(new Settings { TremorStabilization = true, StabilizationStrength = 1 });
            for (int i = 0; i < 30; i++) p.Process(Pad(x: 32767), .008);
            int before = s.X;
            for (int i = 0; i < 100; i++) p.Process(Pad(), .008);
            Check(before == s.X);
            p.SetEnabled(false); p.Process(Pad(), .008); p.Process(Pad(x: 32767), .02); Check(before == s.X);
        });
        test("click hold rejects short repeated presses and releases immediately", () =>
        {
            var (p, s) = Ready(new Settings { ClickHoldMilliseconds = 100 });
            for (int i = 0; i < 12; i++) { p.Process(Pad(Buttons.A), .008); p.Process(Pad(), .008); }
            Check(s.Events.Count == 0);
            for (int i = 0; i < 12; i++) p.Process(Pad(Buttons.A), .008);
            Check(s.Events.Count == 0); p.Process(Pad(Buttons.A), .008);
            p.Process(Pad(), .008);
            Check(s.Events.SequenceEqual(new[] { "Left:True", "Left:False" }));
        });
        test("disconnect discards partially completed click hold", () =>
        {
            var (p, s) = Ready(new Settings { ClickHoldMilliseconds = 100 });
            for (int i = 0; i < 10; i++) p.Process(Pad(Buttons.A), .008);
            p.Process(null, .008); p.Process(Pad(), .008);
            for (int i = 0; i < 4; i++) p.Process(Pad(Buttons.A), .008);
            Check(s.Events.Count == 0);
        });
        test("all three tool buttons respect remapping and trigger only once", () =>
        {
            var (p, s) = Ready(new Settings { YAction = Shortcut.Narrator, LeftStickAction = Shortcut.ColorFilters, RightStickAction = Shortcut.VoiceTyping });
            for (int i = 0; i < 10; i++) p.Process(Pad(Buttons.Y | Buttons.LeftStick | Buttons.RightStick), .008);
            Check(s.Events.SequenceEqual(new[] { "Narrator", "ColorFilters", "VoiceTyping" }));
        });
        test("new settings clamp invalid values and recover unknown actions", () =>
        {
            var s = new Settings
            {
                StabilizationStrength = double.NaN,
                ClickHoldMilliseconds = 900,
                TargetAssistStrength = -5,
                YAction = (Shortcut)999,
                LeftStickAction = Shortcut.Up
            }.Validate();
            Check(s.StabilizationStrength == .65 && s.ClickHoldMilliseconds == 500 && s.TargetAssistStrength == 0);
            Check(s.YAction == Shortcut.CloseMagnifier && s.LeftStickAction == Shortcut.OnScreenKeyboard);
        });
        test("v1 settings migrate with assistance disabled", () =>
        {
            var s = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"PointerSpeed\":800,\"Haptics\":false}")!;
            Check(s.PointerSpeed == 800 && !s.TargetAssist && !s.TremorStabilization && !s.RememberTargets);
            Check(s.RightStickAction == Shortcut.AccessibilityHub);
        });
        var forward = new TargetCandidate(80, -20, 40, 40, new(100, 0), "Button");
        var behind = new TargetCandidate(-120, -20, 40, 40, new(-100, 0), "Hyperlink");
        test("prediction chooses targets ahead and responds to direction changes", () =>
        {
            Check(TargetPrediction.Select(new(0, 0), new(300, 0), new[] { behind, forward }) == forward);
            Check(TargetPrediction.Select(new(0, 0), new(-300, 0), new[] { behind, forward }) == behind);
        });
        test("prediction ignores idle, fast, distant, and invalid targets", () =>
        {
            Check(TargetPrediction.Select(default, default, new[] { forward }) == null);
            Check(TargetPrediction.Select(default, new(2000, 0), new[] { forward }) == null);
            Check(TargetPrediction.Select(default, new(double.NaN, 0), new[] { forward }) == null);
            Check(TargetPrediction.Select(default, new(300, 0), new[] { forward with { Left = 400, Aim = new(420, 0) } }) == null);
            Check(TargetPrediction.Select(default, new(300, 0), new[] { forward with { Width = double.NaN } }) == null);
        });
        test("prediction works at negative monitor coordinates", () =>
        {
            var candidate = forward with { Left = -1020, Aim = new(-1000, 0) };
            Check(TargetPrediction.Select(new(-1100, 0), new(300, 0), new[] { candidate }) == candidate);
        });
        test("learning breaks close ties but cannot pull toward a distant target", () =>
        {
            var close = forward with { Left = 78, Aim = new(98, 0), Kind = "Hyperlink" };
            var counts = new Dictionary<string, int> { ["Button"] = 100 };
            Check(TargetPrediction.Select(default, new(300, 0), new[] { close, forward }) == close);
            Check(TargetPrediction.Select(default, new(300, 0), new[] { close, forward }, counts) == forward);
            var near = close with { Left = 30, Aim = new(50, 0) };
            Check(TargetPrediction.Select(default, new(300, 0), new[] { near, forward }, counts) == near);
        });
        test("target assist never initiates motion, acts during drag, or overrides reversal", () =>
        {
            Check(TargetPrediction.Assist(default, default, forward, 1, false) == default);
            Check(TargetPrediction.Assist(default, new(8, 2), forward, 1, true) == new PointD(8, 2));
            Check(TargetPrediction.Assist(default, new(-8, 0), forward, 1, false) == new PointD(-8, 0));
            Check(TargetPrediction.Assist(default, new(150, 2), forward, 0, false) == new PointD(150, 2));
        });
        test("assistance bends gently without increasing speed or jumping to target", () =>
        {
            var delta = new PointD(8, 3);
            var result = TargetPrediction.Assist(default, delta, forward, 1, false);
            Check(result.Length <= delta.Length && result.X > 0 && result.Y >= 0 && result.Y < delta.Y);
            Check(result.Length < 10);
            Check(TargetPrediction.Assist(new(85, 0), new(5, 0), forward, 1, false).Length < 5);
        });
        test("target memory saves only known control counts and can be forgotten", () =>
        {
            var memory = new TargetMemory(); memory.Record("Button"); memory.Record("Button"); memory.Record("Hyperlink");
            memory.Record("PRIVATE SCREEN TEXT");
            string json = memory.Serialize(); Check(!json.Contains("PRIVATE"));
            var copy = TargetMemory.Deserialize(json); Check(copy.Samples == 3 && copy.Counts["Button"] == 2);
            copy.Clear(); Check(copy.Samples == 0 && copy.Serialize() == "{}");
            Check(TargetMemory.Deserialize("{broken").Samples == 0);
            Check(TargetMemory.Deserialize("{\"Unknown\":99,\"Button\":-5}").Samples == 0);
        });
        test("long-running target learning remains bounded", () =>
        {
            var memory = new TargetMemory();
            for (int i = 0; i < 10000; i++) memory.Record(i % 2 == 0 ? "Button" : "Hyperlink");
            Check(memory.Samples <= 1000 && TargetMemory.Deserialize(memory.Serialize()).Samples == memory.Samples);
        });
        test("accessibility catalogue covers vision hearing and interaction with unique settings", () =>
        {
            Check(AccessibilityCatalog.Features.Select(f => f.Category).Distinct().Count() == 3);
            Check(AccessibilityCatalog.Features.Select(f => f.Uri).Distinct().Count() == AccessibilityCatalog.Features.Count);
            Check(AccessibilityCatalog.Features.All(f => f.Uri.StartsWith("ms-settings:", StringComparison.Ordinal)));
        });
        test("tool shortcuts emit balanced key down and key up sequences", () =>
        {
            foreach (var action in AccessibilityCatalog.Actions)
            {
                var sent = new List<NativeMethods.Input>();
                var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return (uint)inputs.Length; });
                if (action.Action is Shortcut.Accessibility or Shortcut.AccessibilityHub) continue;
                sink.Invoke(action.Action); var keys = AccessibilityCatalog.Keys(action.Action);
                Check(sent.Count == keys.Length * 2, action.Name);
                Check(sent.Take(keys.Length).Select(i => i.Data.Keyboard.Key).SequenceEqual(keys));
                Check(sent.Skip(keys.Length).Select(i => i.Data.Keyboard.Key).SequenceEqual(keys.Reverse()));
                Check(!sink.HasHeldInputs);
            }
        });
        test("target learning callback runs only for accepted left presses", () =>
        {
            int count = 0; bool accept = false;
            var sink = new WindowsInputSink(inputs => accept ? (uint)inputs.Length : 0) { LeftClickDelivered = () => count++ };
            sink.SetMouseButton(MouseButton.Left, true); Check(count == 0);
            accept = true; sink.SetMouseButton(MouseButton.Left, true); sink.SetMouseButton(MouseButton.Left, true);
            sink.SetMouseButton(MouseButton.Left, false); Check(count == 1);
        });
    }
}
