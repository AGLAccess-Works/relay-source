using ControllerMouse.Core;
using ControllerMouse.App;

internal static class ScrollHapticTests
{
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static Gamepad Pad(short y = 32767, short x = 0, Buttons buttons = Buttons.None) => new(buttons, 0, 0, 0, 0, x, y);
    private static int Travel(Settings settings, double dt = .008, int count = 125, Buttons buttons = Buttons.None)
    {
        var p = new ScrollProcessor(); int total = 0;
        for (int i = 0; i < count; i++) total += p.Process(Pad(buttons: buttons), settings, dt).Vertical;
        return total;
    }
    public static void Run(Action<string, Action> test)
    {
        test("motor output is deduplicated and a failed stop is retried", () =>
        {
            var results = new Queue<uint>(new uint[] { 0, 1, 0 });
            var sent = new List<MotorOutput>();
            var device = new HapticDevice((slot, output) => { sent.Add(output); return results.Dequeue(); });
            var pulse = new MotorOutput(1000, 2000);
            device.Apply(0, pulse); device.Apply(0, pulse); Check(sent.Count == 1);
            device.Apply(0, default); Check(device.Warning != null && sent.Count == 2);
            device.Apply(0, default); Check(device.Warning == null && sent.Count == 3 && sent[2] == default);
        });
        test("disconnect stops the old slot and reconnect does not replay a pulse", () =>
        {
            var sent = new List<(uint, MotorOutput)>();
            var device = new HapticDevice((slot, output) => { sent.Add((slot, output)); return 0; });
            device.Apply(1, new(1000, 2000)); device.Apply(-1, default); device.Apply(2, default);
            Check(sent.Count == 2 && sent[1] == (1u, default(MotorOutput)));
        });
        test("small scroll nudges emit fine wheel units promptly", () =>
        {
            var p = new ScrollProcessor(); var settings = new Settings(); int total = 0;
            for (int i = 0; i < 10; i++) total += p.Process(Pad(8191), settings, .008).Vertical;
            Check(total > 0 && total < 120);
        });
        test("scroll rate is independent of polling frequency", () =>
        {
            Check(Math.Abs(Travel(new Settings()) - Travel(new Settings(), .02, 50)) <= 1);
        });
        test("scroll neutral immediately clears motion and fractional remainder", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings();
            p.Process(Pad(), s, .02);
            for (int i = 0; i < 50; i++) Check(p.Process(default, s, .008) == default);
            Check(!p.Active);
            Check(p.Process(Pad(8000), s, .008) == new ScrollProcessor().Process(Pad(8000), s, .008));
        });
        test("scroll reversal cannot emit an old-direction tail", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollSmoothing = .15 };
            for (int i = 0; i < 30; i++) p.Process(Pad(), s, .008);
            for (int i = 0; i < 20; i++) Check(p.Process(Pad(-32768), s, .008).Vertical <= 0);
        });
        test("scroll dead zone and hysteresis suppress resting drift", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings();
            Check(p.Process(Pad(5500), s, .02) == default && !p.Active);
            p.Process(Pad(6500), s, .02); Check(p.Active);
            p.Process(Pad(5500), s, .02); Check(p.Active);
            p.Process(Pad(4000), s, .02); Check(!p.Active);
        });
        test("compatibility mode emits only full wheel notches", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollMode = ScrollMode.Compatible }; int sum = 0;
            for (int i = 0; i < 125; i++) { int delta = p.Process(Pad(), s, .008).Vertical; Check(delta % 120 == 0); sum += delta; }
            Check(sum >= 840);
        });
        test("step mode gives one notch per nudge without quick repeats", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollMode = ScrollMode.Step };
            for (int j = 0; j < 4; j++)
            {
                Check(p.Process(Pad(16383), s, .008).Vertical == 120);
                for (int i = 0; i < 20; i++) Check(p.Process(Pad(16383), s, .008).Vertical == 0);
                p.Process(default, s, .008);
            }
        });
        test("step repeat is bounded and supports immediate reversal", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollMode = ScrollMode.Step };
            int count = 0; for (int i = 0; i < 125; i++) if (p.Process(Pad(), s, .008).Vertical != 0) count++;
            Check(count >= 5 && count <= 8);
            Check(p.Process(Pad(-32768), s, .008).Vertical == -120);
        });
        test("axis preference rejects diagonal drift and allows deliberate direction changes", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollAxisLock = true, ScrollSmoothing = 0 };
            var first = p.Process(Pad(25000, 12000), s, .02); Check(first.Vertical > 0 && first.Horizontal == 0);
            var next = p.Process(Pad(5000, 25000), s, .02); Check(next.Horizontal > 0 && next.Vertical == 0);
        });
        test("scroll precision and boost change rate; precision toggle needs no held button", () =>
        {
            var s = new Settings { ScrollSmoothing = 0 };
            Check(Math.Abs(Travel(s, buttons: Buttons.LeftShoulder) - 336) <= 1);
            Check(Math.Abs(Travel(s, buttons: Buttons.RightShoulder) - 1728) <= 1);
            Check(Travel(s with { ScrollPrecision = true }) == Travel(s, buttons: Buttons.LeftShoulder));
            Check(Travel(s with { ScrollPrecision = true }, buttons: Buttons.LeftShoulder) == Travel(s, buttons: Buttons.LeftShoulder));
            Check(Travel(s with { ScrollModifiers = false }, buttons: Buttons.LeftShoulder) == Travel(s));
        });
        test("scroll stabilization adds damping without changing sustained target rate", () =>
        {
            var s = new Settings();
            Check(Travel(s with { ScrollStabilization = true }, count: 10) < Travel(s, count: 10));
            Check(Travel(s with { ScrollStabilization = true }, count: 1250) > 9500);
        });
        test("invalid elapsed time and scheduler stalls cannot cause huge scrolling", () =>
        {
            var p = new ScrollProcessor(); var s = new Settings { ScrollSmoothing = 0 };
            Check(p.Process(Pad(), s, double.NaN) == default);
            Check(p.Process(Pad(), s, 10).Vertical <= 48);
        });
        test("scroll endpoints are symmetric and inverted direction is signed", () =>
        {
            var a = new ScrollProcessor(); var b = new ScrollProcessor(); var c = new ScrollProcessor(); var s = new Settings();
            for (int i = 0; i < 125; i++)
            {
                int v = a.Process(Pad(), s, .008).Vertical;
                Check(v == -b.Process(Pad(-32768), s, .008).Vertical);
                Check(v == -c.Process(Pad(), s with { InvertScroll = true }, .008).Vertical);
            }
        });
        test("scroll settings are sanitized and old settings inherit new defaults", () =>
        {
            var s = new Settings
            {
                ScrollMode = (ScrollMode)999,
                ScrollDeadZone = double.NaN,
                ScrollCurve = 50,
                ScrollSmoothing = -10,
                HapticStrength = double.PositiveInfinity
            }.Validate();
            Check(s.ScrollMode == ScrollMode.Smooth && s.ScrollDeadZone == .18 && s.ScrollCurve == 3 &&
                s.ScrollSmoothing == 0 && s.HapticStrength == .5);
            var old = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"Haptics\":false}");
            Check(old != null && !old.Haptics && !old.TargetHaptics && old.ScrollMode == ScrollMode.Smooth);
        });
        test("settings updates and pause clear pending scrolling", () =>
        {
            var sink = new Recorder(); var p = new InputProcessor(sink);
            p.Process(default(Gamepad), .008); p.Process(Pad(), .02);
            p.Settings = new Settings { ScrollMode = ScrollMode.Step }; Check(!p.ScrollActive);
            p.SetEnabled(false); int before = sink.Vertical; p.Process(Pad(), .02); Check(sink.Vertical == before);
        });
        test("haptic strength, cooldown, expiry and cancellation are bounded", () =>
        {
            var h = new HapticScheduler(); Check(!h.Request(HapticCue.Target, 0, 0));
            Check(h.Request(HapticCue.Target, 1, .5));
            var output = h.Sample(1.01); Check(output.Right > 0 && output.Right < 22000);
            Check(!h.Request(HapticCue.Scroll, 1.02, 1));
            Check(h.Sample(1.1) == default && !h.Request(HapticCue.Target, 1.15, 1));
            Check(h.Request(HapticCue.Scroll, 1.3, 1)); h.Stop(); Check(h.Sample(1.31) == default && h.Sample(20) == default);
        });
        test("boundary haptic has two short pulses separated by silence", () =>
        {
            var h = new HapticScheduler(); h.Request(HapticCue.Boundary, 0, .5);
            Check(h.Sample(.02) != default && h.Sample(.08) == default && h.Sample(.15) != default && h.Sample(.19) == default);
        });
        test("target cues require a stable dwell and never repeat while hovering", () =>
        {
            var t = new NavigationCueTracker(); Check(!t.Hover("a", 0)); Check(!t.Hover("a", .1));
            Check(t.Hover("a", .13)); Check(!t.Hover("a", 5));
            t.Hover(null, 6); Check(!t.Hover("a", 7)); Check(t.Hover("a", 7.2));
        });
        test("boundary cues need observed movement in the commanded direction", () =>
        {
            var t = new NavigationCueTracker();
            Check(!t.Boundary("a", 100, -1, -1, 0)); Check(!t.Boundary("a", 100, -1, -1, 0));
            Check(!t.Boundary("a", 80, -1, -1, 0)); Check(t.Boundary("a", 100, -1, -1, 0));
            Check(!t.Boundary("a", 100, -1, -1, 0));
            Check(!t.Boundary("a", 0, -1, -1, 0));
        });
        test("boundary cues reject unknown providers, changed regions and changed direction", () =>
        {
            var t = new NavigationCueTracker();
            t.Boundary("a", -1, -1, -1, 0); Check(!t.Boundary("a", 100, -1, -1, 0));
            t.Boundary("a", 50, -1, -1, 0); Check(!t.Boundary("b", 100, -1, -1, 0));
            t.Boundary("b", 50, -1, -1, 0); Check(!t.Boundary("b", 0, -1, 1, 0));
            t.Reset(); Check(!t.Boundary("b", 100, -1, -1, 0));
        });
        test("boundary cues tolerate UI Automation floating-point endpoint rounding", () =>
        {
            var t = new NavigationCueTracker();
            t.Boundary("a", 95, -1, -1, 0);
            Check(t.Boundary("a", 100.00000000000001, -1, -1, 0));
        });
        test("horizontal boundary direction follows Windows wheel semantics", () =>
        {
            var t = new NavigationCueTracker();
            t.Boundary("a", -1, 50, 0, 1); Check(t.Boundary("a", -1, 100, 0, 1));
            t.Boundary("a", -1, 50, 0, -1); Check(t.Boundary("a", -1, 0, 0, -1));
        });
        test("scroll feedback only describes input accepted by Windows", () =>
        {
            var calls = new Queue<uint>(new uint[] { 0, 1, 0, 0 });
            var sink = new WindowsInputSink(_ => calls.Dequeue()); var accepted = new List<(int, int)>();
            sink.ScrollDelivered = (v, h) => accepted.Add((v, h));
            sink.Scroll(1, -3); sink.Scroll(1, 3);
            Check(accepted.SequenceEqual(new[] { (0, -3) }));
        });
    }
}
