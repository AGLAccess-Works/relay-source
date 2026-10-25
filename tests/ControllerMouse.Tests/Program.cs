using ControllerMouse.Core;
using ControllerMouse.App;

int passed = 0, failed = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"FAIL {name}: {ex.Message}"); failed++; }
}
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
(InputProcessor Engine, Recorder Sink) Ready(Settings? settings = null)
{
    var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = settings ?? new Settings() };
    engine.Process(default(Gamepad), 0.008); return (engine, sink);
}
Gamepad Pad(Buttons buttons = Buttons.None, short x = 0, short y = 0, short rx = 0, short ry = 0, byte lt = 0, byte rt = 0) => new(buttons, lt, rt, x, y, rx, ry);

Test("neutral and stick drift produce no input", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 100; i++) p.Process(Pad(x: 3000, y: -2000), 0.008);
    Check(s.Events.Count == 0);
});
Test("initial connection waits for all controls to be released", () =>
{
    var s = new Recorder(); var p = new InputProcessor(s);
    p.Process(Pad(Buttons.A, x: 32767), 0.008); Check(!p.Armed && s.Events.Count == 0);
    p.Process(Pad(), 0.008); p.Process(Pad(Buttons.A), 0.008); Check(s.Events.SequenceEqual(new[] { "Left:True" }));
});
foreach (var pair in new[] { (Buttons.A, MouseButton.Left), (Buttons.B, MouseButton.Right), (Buttons.X, MouseButton.Middle) })
    Test($"{pair.Item1} holds and releases exactly once", () =>
    {
        var (p, s) = Ready(); for (int i = 0; i < 100; i++) p.Process(Pad(pair.Item1), 0.008);
        p.Process(Pad(), 0.008); Check(s.Events.SequenceEqual(new[] { $"{pair.Item2}:True", $"{pair.Item2}:False" }));
    });
Test("rapid clicks are not dropped by arbitrary debounce", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 3; i++) { p.Process(Pad(Buttons.A), .008); p.Process(Pad(), .008); }
    Check(s.Events.Count == 6);
});
Test("disconnect releases all mouse buttons", () =>
{
    var (p, s) = Ready(); p.Process(Pad(Buttons.A | Buttons.B | Buttons.X), .008); p.Process(null, .008);
    Check(s.Events.Count(e => e.EndsWith("False")) == 3 && !p.Armed);
});
Test("reconnect cannot replay a held button or old cursor movement", () =>
{
    var (p, s) = Ready(); p.Process(Pad(Buttons.A, x: 32767), .02); p.Process(null, .008); s.Events.Clear();
    p.Process(Pad(Buttons.A, x: 32767), .008); p.Process(Pad(), .008); Check(s.Events.Count == 0);
});
Test("pause releases drag and prevents movement", () =>
{
    var (p, s) = Ready(); p.Process(Pad(Buttons.A), .008); p.SetEnabled(false);
    Check(s.Events.Last() == "Left:False"); s.Events.Clear(); p.Process(Pad(), .008); p.Process(Pad(Buttons.A, x: 32767), .02); Check(s.Events.Count == 0);
});
Test("resume waits for neutral", () =>
{
    var (p, s) = Ready(); p.SetEnabled(false); p.SetEnabled(true); p.Process(Pad(Buttons.A), .008); Check(s.Events.Count == 0);
    p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); Check(s.Events.Count == 1);
});
Test("pause chord toggles once and suppresses individual shortcuts", () =>
{
    var (p, s) = Ready(); p.Process(Pad(Buttons.Back), .008);
    for (int i = 0; i < 200; i++) p.Process(Pad(Buttons.Back | Buttons.Start), .008);
    Check(!p.Enabled && s.Events.Count == 0); p.Process(Pad(Buttons.Start), .008); p.Process(Pad(), .008);
    p.Process(Pad(Buttons.Start | Buttons.Back), .008); Check(p.Enabled && s.Events.Count == 0);
    p.Process(Pad(), .008); Check(p.Armed);
});
Test("Start is invoked once on release", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 100; i++) p.Process(Pad(Buttons.Start), .008);
    Check(s.Events.Count == 0); p.Process(Pad(), .008); Check(s.Events.SequenceEqual(new[] { "StartMenu" }));
});
Test("View opens Accessibility once on release", () =>
{
    var (p, s) = Ready(); p.Process(Pad(Buttons.Back), .008); p.Process(Pad(), .008); p.Process(Pad(), .008);
    Check(s.Events.SequenceEqual(new[] { "Accessibility" }));
});
Test("Y Magnifier close is edge-triggered", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 100; i++) p.Process(Pad(Buttons.Y), .008); Check(s.Events.SequenceEqual(new[] { "CloseMagnifier" }));
});
Test("trigger hysteresis prevents repeated zoom near threshold", () =>
{
    var (p, s) = Ready(); foreach (byte value in new byte[] { 51, 48, 52, 36, 60, 34, 55 }) p.Process(Pad(rt: value), .008);
    Check(s.Events.SequenceEqual(new[] { "ZoomIn", "ZoomIn" }));
});
Test("both triggers operate independently", () =>
{
    var (p, s) = Ready(); p.Process(Pad(lt: 70, rt: 80), .008); p.Process(Pad(lt: 70, rt: 80), .008);
    Check(s.Events.SequenceEqual(new[] { "ZoomOut", "ZoomIn" }));
});
Test("positive and negative stick endpoints are symmetric", () =>
{
    Check(InputProcessor.Shape(32767, 0, .18, 1.65) == (1d, 0d)); Check(InputProcessor.Shape(-32768, 0, .18, 1.65) == (-1d, 0d));
});
Test("radial dead zone preserves direction and caps diagonal speed", () =>
{
    var (x, y) = InputProcessor.Shape(32767, 32767, .18, 1.65); Check(Math.Abs(Math.Sqrt(x * x + y * y) - 1) < 1e-10); Check(Math.Abs(x - y) < 1e-10);
});
foreach (double smoothing in new[] { 0d, .025 })
    Test($"pointer speed is consistent at 50 and 125 Hz, smoothing {smoothing}", () =>
    {
        var a = Ready(new Settings { Smoothing = smoothing }); var b = Ready(new Settings { Smoothing = smoothing });
        for (int i = 0; i < 50; i++) a.Engine.Process(Pad(x: 32767), .02);
        for (int i = 0; i < 125; i++) b.Engine.Process(Pad(x: 32767), .008);
        Check(Math.Abs(a.Sink.X - b.Sink.X) <= 1, $"{a.Sink.X} != {b.Sink.X}");
    });
Test("small fractional movements accumulate", () =>
{
    var (p, s) = Ready(new Settings { Smoothing = 0 }); for (int i = 0; i < 125; i++) p.Process(Pad(x: 7500), .008);
    Check(s.X > 5 && s.X < 30, $"Unexpected distance {s.X}");
});
Test("neutral immediately stops smoothing tail", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 10; i++) p.Process(Pad(x: 32767), .008);
    int before = s.X; for (int i = 0; i < 20; i++) p.Process(Pad(), .008); Check(s.X == before);
});
Test("LB precision and RB boost adjust speed", () =>
{
    int Travel(Buttons button) { var (p, s) = Ready(new Settings { Smoothing = 0 }); for (int i = 0; i < 50; i++) p.Process(Pad(button, x: 32767), .02); return s.X; }
    Check(Math.Abs(Travel(Buttons.LeftShoulder) - 308) <= 1); Check(Math.Abs(Travel(Buttons.RightShoulder) - 1980) <= 1);
});
Test("long scheduling delays cannot cause giant cursor jumps", () =>
{
    var (p, s) = Ready(new Settings { Smoothing = 0 }); p.Process(Pad(x: 32767), 5); Check(s.X <= 55);
    p.Process(Pad(x: 32767), double.NaN); Check(s.X <= 55);
});
Test("right stick scrolls at a bounded, time-based rate", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 125; i++) p.Process(Pad(ry: 32767), .008);
    Check(s.Vertical is >= 840 and <= 960); Check(s.Horizontal == 0);
});
Test("horizontal and inverted scrolling", () =>
{
    var (p, s) = Ready(new Settings { InvertScroll = true }); for (int i = 0; i < 50; i++) p.Process(Pad(rx: 32767), .02);
    Check(s.Horizontal <= -840 && s.Vertical == 0);
});
Test("explicit arrow mapping repeats after delay instead of every poll", () =>
{
    var (p, s) = Ready(new Settings { ButtonMappings = new() { [Control.Up] = new(BindingKind.Shortcut, Shortcut: Shortcut.Up) } }); for (int i = 0; i < 25; i++) p.Process(Pad(Buttons.Up), .008); Check(s.Events.Count == 1);
    for (int i = 0; i < 75; i++) p.Process(Pad(Buttons.Up), .008); Check(s.Events.Count is >= 5 and <= 7);
});
Test("settings sanitize out-of-range and non-finite values", () =>
{
    var settings = new Settings { PointerSpeed = double.NaN, ScrollSpeed = 99, DeadZone = -1, Smoothing = double.PositiveInfinity }.Validate();
    Check(settings.PointerSpeed == 1100 && settings.ScrollSpeed == 24 && settings.DeadZone == .05 && settings.Smoothing == .025);
});
Test("settings round trip and malformed JSON recovery", () =>
{
    string folder = Path.Combine(Path.GetTempPath(), "XboxControllerMouse.Tests-" + Guid.NewGuid());
    try
    {
        var store = new SettingsStore(Path.Combine(folder, "settings.json")); Check(store.Load() == new Settings());
        var settings = new Settings { PointerSpeed = 750, InvertScroll = true, LightTheme = true }; store.Save(settings); Check(store.Load() == settings);
        File.WriteAllText(store.Path, "{broken"); Check(store.Load() == new Settings() && store.Warning != null);
        store.Save(settings); Check(store.Warning == null && !File.Exists(store.Path + ".tmp"));
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
});
Test("Windows INPUT and XInput structures have native sizes", () =>
{
    Check(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>() == (IntPtr.Size == 8 ? 40 : 28));
    Check(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.XState>() == 16);
    Check(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.XGamepad>() == 12);
});
Test("failed mouse release is retried without abandoning a held button", () =>
{
    var results = new Queue<uint>(new uint[] { 1, 0, 1 });
    var sent = new List<NativeMethods.Input>();
    var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return results.Dequeue(); });
    sink.SetMouseButton(MouseButton.Left, true); sink.SetMouseButton(MouseButton.Left, false);
    Check(sink.HasHeldInputs && sink.Warning != null); sink.RetryReleases();
    Check(!sink.HasHeldInputs && sent.Count == 3 && sent[2].Data.Mouse.Flags == 4);
});
Test("failed mouse down never creates a fictitious held button", () =>
{
    var sink = new WindowsInputSink(_ => 0); sink.SetMouseButton(MouseButton.Left, true);
    Check(!sink.HasHeldInputs && sink.Warning != null);
});
foreach (uint prefix in new uint[] { 1, 2, 3 })
    Test($"partial keyboard batch ({prefix} events) cleans up accepted keys", () =>
    {
        bool first = true; var releases = new List<NativeMethods.Input>();
        var sink = new WindowsInputSink(inputs =>
        {
            if (first) { first = false; return prefix; }
            releases.AddRange(inputs); return (uint)inputs.Length;
        });
        sink.Invoke(Shortcut.ZoomIn);
        Check(!sink.HasHeldInputs && releases.Count > 0);
        Check(releases.All(i => (i.Data.Keyboard.Flags & 2) != 0));
    });
Test("blocked keyboard cleanup is retried on next poll", () =>
{
    var results = new Queue<uint>(new uint[] { 1, 0, 1 });
    var sink = new WindowsInputSink(_ => results.Dequeue());
    sink.Invoke(Shortcut.ZoomOut); Check(sink.HasHeldInputs);
    sink.RetryReleases(); Check(!sink.HasHeldInputs);
});
Test("fully blocked shortcut has no held keys", () =>
{
    var sink = new WindowsInputSink(_ => 0); sink.Invoke(Shortcut.CloseMagnifier); Check(!sink.HasHeldInputs);
});
Test("wheel delta retains signed Win32 representation", () =>
{
    var sent = new List<NativeMethods.Input>();
    var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return (uint)inputs.Length; });
    sink.Scroll(-120, 120);
    Check(unchecked((int)sent[0].Data.Mouse.Data) == -120 && sent[0].Data.Mouse.Flags == 0x800);
    Check(sent[1].Data.Mouse.Data == 120 && sent[1].Data.Mouse.Flags == 0x1000);
});
Test("live output feedback never reports blocked input as delivered", () =>
{
    var blocked = new WindowsInputSink(_ => 0);
    blocked.Move(3, 2); blocked.Scroll(1, 0); blocked.SetMouseButton(MouseButton.Right, true); blocked.Invoke(Shortcut.ZoomIn);
    Check(blocked.LastOutput == "Nothing sent");
    var accepted = new WindowsInputSink(inputs => (uint)inputs.Length);
    accepted.SetMouseButton(MouseButton.Right, true); Check(accepted.LastOutput == "Right button down");
    accepted.Scroll(1, 0); Check(accepted.LastOutput == "Scroll");
});
AssistanceTests.Run(Test);
ScrollHapticTests.Run(Test);
ProfileMappingTests.Run(Test);
AdaptiveProfileTests.Run(Test);
KeyboardTests.Run(Test);
AdaptiveMotionTests.Run(Test);
ButtonChordTests.Run(Test);
SetupPlanTests.Run(Test);
VisualAccessTests.Run(Test);
StabilityTests.Run(Test);
foreach (var direction in new[] { (Buttons.Right, 140, 0), (Buttons.Left, -140, 0), (Buttons.Up, 0, -140), (Buttons.Down, 0, 140) })
    Test($"D-pad {direction.Item1} moves actual pointer and stops on release", () =>
    {
        var (p, s) = Ready();
        for (int i = 0; i < 125; i++) p.Process(Pad(direction.Item1), .008);
        Check(Math.Abs(s.X - direction.Item2) <= 1 && Math.Abs(s.Y - direction.Item3) <= 1);
        Check(s.Events.All(e => e == "Move"));
        var before = (s.X, s.Y); p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008);
        Check(before == (s.X, s.Y) && s.Events.Last() == "Left:True");
    });
Test("D-pad diagonals have bounded speed and opposing directions cancel", () =>
{
    var (p, s) = Ready(); for (int i = 0; i < 125; i++) p.Process(Pad(Buttons.Up | Buttons.Right), .008);
    Check(Math.Abs(Math.Sqrt(s.X * s.X + s.Y * s.Y) - 140) < 2);
    var before = (s.X, s.Y); p.Process(Pad(Buttons.Up | Buttons.Down | Buttons.Left | Buttons.Right), .05);
    Check(before == (s.X, s.Y)); p.SetEnabled(false); p.Process(Pad(), .008); p.Process(Pad(Buttons.Right), .05);
    Check(before == (s.X, s.Y));
});
Test("every accessibility settings page can be mapped without injecting keys", () =>
{
    var opened = new List<string>();
    var sink = new WindowsInputSink(_ => throw new Exception("Unexpected keyboard injection"), opened.Add);
    Check(AccessibilityCatalog.Features.Select(f => f.PageAction).Distinct().Count() == AccessibilityCatalog.Features.Count);
    foreach (var feature in AccessibilityCatalog.Features)
    {
        Check(feature.PageAction != Shortcut.None && AccessibilityCatalog.IsQuickAction(feature.PageAction));
        Check(AccessibilityCatalog.SettingsUri(feature.PageAction) == feature.Uri);
        sink.Invoke(feature.PageAction); Check(opened.Last() == feature.Uri);
    }
    Check(AccessibilityCatalog.Keys(Shortcut.VoiceTyping).SequenceEqual(new ushort[] { 0x5B, 0x48 }));
});
Test("Narrator navigation uses approved arrows, tab, select and back without moving mouse", () =>
{
    var (p, s) = Ready(); p.NarratorRunning = true;
    p.Process(Pad(Buttons.Up), .008); Check(p.KeyboardNavigationActive && s.Events.SequenceEqual(new[] { "Up" }));
    foreach (var (button, expected) in new[]{(Buttons.Down,"Down"),(Buttons.Left,"Left"),(Buttons.Right,"Right"),
        (Buttons.LeftShoulder,"Keys:16,9"),(Buttons.RightShoulder,"Keys:9"),(Buttons.A,"Keys:13"),(Buttons.B,"Keys:27")})
    {
        p.Process(Pad(), .008); s.Events.Clear(); p.Process(Pad(button), .008);
        Check(s.Events.SequenceEqual(new[] { expected }), string.Join(";", s.Events));
    }
    Check(s.X == 0 && s.Y == 0);
});
Test("Narrator mode releases drag without replaying held A as select", () =>
{
    var (p, s) = Ready(); p.NarratorRunning = true; p.Process(Pad(Buttons.A), .008);
    p.Process(Pad(Buttons.A | Buttons.Up), .008); p.Process(Pad(Buttons.A), .008);
    Check(s.Events.SequenceEqual(new[] { "Left:True", "Up", "Left:False" }));
    p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); Check(s.Events.Last() == "Keys:13");
});
Test("left stick returns to mouse but held select cannot become a click", () =>
{
    var (p, s) = Ready(); p.NarratorRunning = true; p.Process(Pad(Buttons.Up), .008); p.Process(Pad(), .008);
    p.Process(Pad(Buttons.A), .008); s.Events.Clear(); p.Process(Pad(Buttons.A, x: 32767), .02);
    Check(!p.KeyboardNavigationActive && s.Events.Contains("Move") && !s.Events.Contains("Left:True"));
    p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); Check(s.Events.Last() == "Left:True");
});
Test("Narrator mode ignores stick drift and exits when Narrator stops", () =>
{
    var (p, s) = Ready(); p.NarratorRunning = true; p.Process(Pad(Buttons.Up), .008); p.Process(Pad(), .008); s.Events.Clear();
    p.Process(Pad(x: 7000), .05); Check(p.KeyboardNavigationActive && s.Events.Count == 0);
    p.NarratorRunning = false; p.Process(Pad(), .008); p.Process(Pad(Buttons.Right), .05);
    Check(!p.KeyboardNavigationActive && s.Events.Contains("Move"));
});
Test("Narrator navigation can be disabled and reset safely", () =>
{
    var (p, s) = Ready(new Settings { NarratorNavigation = false }); p.NarratorRunning = true;
    p.Process(Pad(Buttons.Right), .05); Check(!p.KeyboardNavigationActive && s.Events.Contains("Move"));
    p.Settings = new Settings(); p.Process(Pad(), .008); p.Process(Pad(Buttons.Up), .008); Check(p.KeyboardNavigationActive);
    p.Process(null, .008); Check(!p.KeyboardNavigationActive && !p.Armed);
});
Test("Narrator navigation retains button combinations", () =>
{
    var (p, s) = Ready(new Settings { ButtonChords = [new(Control.LeftShoulder, Control.RightShoulder, new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierView))] });
    p.NarratorRunning = true; p.Process(Pad(Buttons.Up), .008); p.Process(Pad(), .008); s.Events.Clear();
    p.Process(Pad(Buttons.LeftShoulder | Buttons.RightShoulder), .008);
    Check(s.Events.SequenceEqual(new[] { "MagnifierView" }));
});
foreach (ushort product in new ushort[] { 0x0b0a, 0x0b0c, 0x0b21 }) Test($"Adaptive hardware identity recognises {product:x4}", () =>
{
    Check(ControllerIdentity.Identify(0x045e, product, "Xbox Wireless Controller").Kind == ControllerKind.Adaptive);
    Check(ControllerIdentity.Identify(0x1234, product, "Adaptive").Kind == ControllerKind.Unknown);
});
Test("standard model is not inferred from unknown or ambiguous controller inventory", () =>
{
    var adaptive = ControllerIdentity.Identify(0x045e, 0x0b0a, "Adaptive"); var standard = ControllerIdentity.Identify(0x045e, 0x0b13, "Xbox");
    Check(standard.Kind == ControllerKind.Standard);
    Check(ControllerIdentity.Resolve(ControllerPreference.Automatic, [adaptive], [2], 2) == ControllerKind.Adaptive);
    Check(ControllerIdentity.Resolve(ControllerPreference.Automatic, [adaptive, standard], [0, 2], 2) == ControllerKind.Unknown);
    Check(ControllerIdentity.Resolve(ControllerPreference.Automatic, [adaptive], [2], 0) == ControllerKind.Unknown);
    Check(ControllerIdentity.Resolve(ControllerPreference.Adaptive, [], [], 0) == ControllerKind.Adaptive);
});
Test("spatial keyboard selection accounts for wide space key and clamps vertical movement", () =>
{
    var selection = new KeyboardSelection(); selection.SetRows([[new(1), new(2), new(3), new(4)], [new(5), new(6, 2), new(7)]], 2);
    Check(selection.Move(0, 1) == 6); Check(selection.Move(0, 1) == 6); Check(selection.Move(1, 0) == 7);
    Check(selection.Move(1, 0) == 5); Check(selection.Move(-1, 0) == 7);
    selection.SetRows([[new(10)]], 10); Check(selection.Move(0, -1) == 10);
});
Test("Relay keyboard commands do not leak into desktop arrows clicks or zoom", () =>
{
    var (p, s) = Ready(); p.SetKeyboardOpen(true); p.Process(Pad(), .008);
    foreach (var (button, expected) in new[]{(Buttons.Up,"KeyboardUp"),(Buttons.Down,"KeyboardDown"),(Buttons.Left,"KeyboardLeft"),(Buttons.Right,"KeyboardRight"),
        (Buttons.A,"KeyboardSelect"),(Buttons.B,"OnScreenKeyboard"),(Buttons.X,"KeyboardBackspace"),(Buttons.Y,"KeyboardSpace"),(Buttons.LeftShoulder,"Keys:37"),(Buttons.RightShoulder,"Keys:39")})
    {
        p.Process(Pad(), .008); s.Events.Clear(); p.Process(Pad(button), .008); Check(s.Events.SequenceEqual(new[] { expected }));
    }
    p.Process(Pad(), .008); s.Events.Clear(); p.Process(Pad(lt: 255), .008); Check(s.Events.SequenceEqual(new[] { "KeyboardSymbols" }));
    p.Process(Pad(), .008); s.Events.Clear(); p.Process(Pad(rt: 255), .008); Check(s.Events.SequenceEqual(new[] { "KeyboardShift" }));
    Check(s.X == 0 && s.Y == 0);
});
Test("keyboard selection takes priority over Narrator and profile combinations", () =>
{
    var (p, s) = Ready(new Settings { ButtonChords = [new(Control.A, Control.B, new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator))] });
    p.NarratorRunning = true; p.SetKeyboardOpen(true); p.Process(Pad(), .008); p.Process(Pad(Buttons.Up), .008);
    Check(s.Events.SequenceEqual(new[] { "KeyboardUp" }) && !p.KeyboardNavigationActive);
});
Test("closing keyboard requires neutral before restoring pointer bindings", () =>
{
    var (p, s) = Ready(); p.SetKeyboardOpen(true); p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); p.SetKeyboardOpen(false); s.Events.Clear();
    p.Process(Pad(Buttons.A), .008); Check(s.Events.Count == 0); p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); Check(s.Events.SequenceEqual(new[] { "Left:True" }));
});
Test("left stick permits clicking keyboard by mouse and D-pad resumes key selection", () =>
{
    var (p, s) = Ready(); p.SetKeyboardOpen(true); p.Process(Pad(), .008); p.Process(Pad(x: 32767), .02); Check(!p.KeyboardSelectionActive);
    p.Process(Pad(), .008); p.Process(Pad(Buttons.A), .008); Check(s.Events.Last() == "Left:True");
    p.Process(Pad(), .008); s.Events.Clear(); p.Process(Pad(Buttons.Right), .008); Check(p.KeyboardSelectionActive && s.Events.SequenceEqual(new[] { "KeyboardRight" }));
});
Test("keyboard repeats navigation but does not repeat select or symbol-page changes", () =>
{
    var (p, s) = Ready(); p.SetKeyboardOpen(true); p.Process(Pad(), .008);
    for (int i = 0; i < 70; i++) p.Process(Pad(Buttons.Right), .008);
    Check(s.Events.Count > 1 && s.Events.All(e => e == "KeyboardRight"));
    p.Process(Pad(), .008); s.Events.Clear(); for (int i = 0; i < 70; i++) p.Process(Pad(Buttons.A), .008);
    Check(s.Events.SequenceEqual(new[] { "KeyboardSelect" }));
    p.Process(Pad(), .008); s.Events.Clear(); for (int i = 0; i < 70; i++) p.Process(Pad(lt: 255), .008);
    Check(s.Events.SequenceEqual(new[] { "KeyboardSymbols" }));
});
Test("accessory setup validates sockets names types and logical inputs", () =>
{
    var entries = AdaptiveAccessory.Validate([new("A","Foot pedal",AccessoryKind.Pedal,[AccessorySignal.A,AccessorySignal.A]),
        new("A","Duplicate",AccessoryKind.Switch,[]),new("fake","Bad",AccessoryKind.Other,[]),
        new("USBLeft",new string('x',100),AccessoryKind.Joystick,[AccessorySignal.LeftAxis,(AccessorySignal)999])]);
    Check(entries.Length == 2 && entries[0].Signals.Length == 1 && entries[1].Name.Length == 60 && entries[1].Signals.SequenceEqual(new[] { AccessorySignal.LeftAxis }));
    Check(AdaptiveAccessory.Ports.Count == 21 && AdaptiveAccessory.Ports.Select(p => p.Id).Distinct().Count() == 21);
});
Test("accessory capture requires neutral and rejects ambiguous presses", () =>
{
    var capture = new AccessoryCapture(); Check(capture.Observe(Pad(Buttons.A)) == null); Check(capture.Observe(Pad()) == null);
    Check(capture.Observe(Pad(Buttons.A | Buttons.B)) == null); Check(capture.Observe(Pad(Buttons.A)) == null);
    capture.Observe(Pad()); Check(capture.Observe(Pad(Buttons.B)) == AccessorySignal.B);
    capture.Observe(null); Check(!capture.Armed); capture.Observe(Pad()); Check(capture.Observe(Pad(x: 20000)) == AccessorySignal.LeftAxis);
});
Test("accessory feedback distinguishes stick axes triggers and Windows-reserved guide", () =>
{
    Check(AdaptiveAccessory.Active(AccessorySignal.RightAxis, Pad(rx: 20000)));
    Check(!AdaptiveAccessory.Active(AccessorySignal.LeftAxis, Pad(rx: 20000)));
    Check(AdaptiveAccessory.Active(AccessorySignal.LeftTrigger, Pad(lt: 100)));
    Check(!AdaptiveAccessory.Active(AccessorySignal.Xbox, Pad(Buttons.A)));
});
Test("accessory setup survives saved settings and profile export without altering button mappings", () =>
{
    var settings = new Settings { AdaptiveAccessories = [new("X1", "Chin joystick", AccessoryKind.Joystick, [AccessorySignal.LeftAxis])], ButtonMappings = new() { [Control.A] = new(BindingKind.Keyboard, Key: 13) } };
    var json = System.Text.Json.JsonSerializer.Serialize(settings);
    var restored = System.Text.Json.JsonSerializer.Deserialize<Settings>(json)!.Validate();
    Check(restored.AdaptiveAccessories![0].Name == "Chin joystick" && restored.AdaptiveAccessories[0].Signals[0] == AccessorySignal.LeftAxis);
    Check(ControlMapping.Resolve(restored, Control.A).Key == 13 && new Settings().AdaptiveAccessories == null);
});
ControllerSelectionTests.Run(Test);
Test("Presentation preferences survive profile changes without copying input mappings", () =>
{
    var preferences = new Settings { InterfaceScale = 2, LightTheme = false, MinimizeToTray = false, ShowControllerLabels = true, ReduceInterfaceMotion = true, InputAnnouncements = true, ReadHoveredText = false };
    var profile = new Settings { PointerSpeed = 700, ButtonMappings = new() { [Control.A] = new(BindingKind.Keyboard, Key: 13) } };
    var applied = profile.WithInterfacePreferences(preferences);
    Check(applied.PointerSpeed == 700 && ControlMapping.Resolve(applied, Control.A).Key == 13 && applied.InterfaceScale == 2 && !applied.LightTheme && !applied.MinimizeToTray && applied.ShowControllerLabels && applied.ReduceInterfaceMotion && applied.InputAnnouncements && !applied.ReadHoveredText);
    Check(SetupPlan.Build(new(), preferences).ReduceInterfaceMotion && SetupPlan.Navigation(preferences).InputAnnouncements);
});
Test("Presentation preferences round trip and new installs use uncluttered light appearance", () =>
{
    var settings = new Settings { ShowControllerLabels = true, ReduceInterfaceMotion = true, InputAnnouncements = true, ReadHoveredText = false };
    var restored = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
    Check(restored.ShowControllerLabels && restored.ReduceInterfaceMotion && restored.InputAnnouncements && !restored.ReadHoveredText && new Settings().ReadHoveredText && new Settings().LightTheme && !new Settings().ShowControllerLabels);
});
ConfigurationNavigationTests.Run(Test);
PausedNavigationTests.Run(Test);
RelayUiInputTests.Run(Test);
NativeDialogNavigationTests.Run(Test);
Console.WriteLine($"\n{passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

sealed class Recorder : IInputSink
{
    public readonly List<string> Events = [];
    public int X, Y, Vertical, Horizontal;
    public void Move(int x, int y) { X += x; Y += y; Events.Add("Move"); }
    public void Scroll(int vertical, int horizontal) { Vertical += vertical; Horizontal += horizontal; Events.Add("Scroll"); }
    public void SetMouseButton(MouseButton button, bool down) => Events.Add($"{button}:{down}");
    public void Invoke(Shortcut shortcut) => Events.Add(shortcut.ToString());
    public void InvokeKeys(ushort[] keys) => Events.Add("Keys:" + string.Join(",", keys));
}
