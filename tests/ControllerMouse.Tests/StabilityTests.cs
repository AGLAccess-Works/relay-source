using ControllerMouse.App;
using ControllerMouse.Core;
using System.Diagnostics;

internal static class StabilityTests
{
    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static Gamepad Pad(Buttons buttons = Buttons.None, byte lt = 0, byte rt = 0) => new(buttons, lt, rt, 0, 0, 0, 0);
    private static (InputProcessor Engine, WindowsInputSink Sink, List<NativeMethods.Input[]> Batches) Ready(Settings? settings = null)
    {
        var batches = new List<NativeMethods.Input[]>();
        var sink = new WindowsInputSink(inputs => { batches.Add(inputs); return (uint)inputs.Length; });
        var engine = new InputProcessor(sink) { Settings = settings ?? new Settings() };
        engine.Process(Pad(), .008); return (engine, sink, batches);
    }
    public static void Run(Action<string, Action> test)
    {
        test("native Windows imports load only from the system directory", () =>
        {
            var search = (System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute?)Attribute.GetCustomAttribute(
                typeof(NativeMethods).Assembly, typeof(System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute));
            Check(search?.Paths == System.Runtime.InteropServices.DllImportSearchPath.System32);
        });
        test("central input state separates press hold release and disconnect with trigger hysteresis", () =>
        {
            var state = new InputStateManager(); state.Update(Pad());
            Check(state.Update(Pad(Buttons.A, rt: 51)).WasPressed(Control.A));
            var held = state.Update(Pad(Buttons.A, rt: 36));
            Check(held.IsDown(Control.A) && held.IsDown(Control.RightTrigger) && held.Pressed == 0);
            Check(state.Update(Pad(rt: 34)).WasReleased(Control.RightTrigger));
            state.Update(Pad(Buttons.B)); Check(state.Update(null).WasReleased(Control.B) && !state.Current.Connected);
            state.Reset(); Check(state.Current == default && state.Previous == default);
        });
        foreach (var action in new[] { Shortcut.Narrator, Shortcut.ZoomIn, Shortcut.ZoomOut, Shortcut.CloseMagnifier })
            test($"{action} is one shot per intentional press including long holds", () =>
            {
                var (engine, sink, batches) = Ready(new Settings { ButtonMappings = new() { [Control.A] = new(BindingKind.Shortcut, Shortcut: action) } });
                for (int i = 0; i < 500; i++) engine.Process(Pad(Buttons.A), .008);
                Check(batches.Count == 1 && !sink.HasHeldInputs);
                engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008);
                Check(batches.Count == 2 && !sink.HasHeldInputs);
            });
        test("simultaneous Narrator and Magnifier actions emit separate balanced batches", () =>
        {
            var (engine, sink, batches) = Ready(new Settings
            {
                ButtonMappings = new()
                {
                    [Control.A] = new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator),
                    [Control.B] = new(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn)
                }
            });
            engine.Process(Pad(Buttons.A | Buttons.B), .008);
            Check(batches.Count == 2 && batches[0].Length == 6 && batches[1].Length == 4 && !sink.HasHeldInputs);
            foreach (var batch in batches) Check(batch.Take(batch.Length / 2).Select(i => i.Data.Keyboard.Key)
                .SequenceEqual(batch.Skip(batch.Length / 2).Reverse().Select(i => i.Data.Keyboard.Key)));
        });
        test("same-frame duplicate semantic actions coalesce across mappings and combinations", () =>
        {
            var (engine, _, batches) = Ready(new Settings
            {
                ButtonMappings = new() { [Control.Y] = new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator) },
                ButtonChords = [new(Control.A, Control.B, new(BindingKind.Shortcut, Shortcut: Shortcut.Narrator))]
            });
            engine.Process(Pad(Buttons.A | Buttons.B | Buttons.Y), .008); Check(batches.Count == 1);
        });
        test("every Narrator partial prefix is cleaned before Magnifier output", () =>
        {
            for (uint prefix = 0; prefix < 6; prefix++)
            {
                bool first = true; var held = new HashSet<ushort>(); var batches = new List<NativeMethods.Input[]>();
                var sink = new WindowsInputSink(inputs =>
                {
                    uint accepted = first ? prefix : (uint)inputs.Length; first = false; batches.Add(inputs);
                    foreach (var input in inputs.Take((int)accepted))
                        if ((input.Data.Keyboard.Flags & 2) != 0) held.Remove(input.Data.Keyboard.Key); else held.Add(input.Data.Keyboard.Key);
                    return accepted;
                });
                sink.Invoke(Shortcut.Narrator); Check(held.Count == 0 && !sink.HasHeldInputs);
                sink.Invoke(Shortcut.ZoomIn); Check(held.Count == 0 && !sink.HasHeldInputs);
                Check(batches.Last().Length == 4);
            }
        });
        test("exception after native key submission performs owned cleanup and preserves external keys", () =>
        {
            bool first = true; var held = new HashSet<ushort> { 0x70 }; var cleanup = new List<ushort>();
            var sink = new WindowsInputSink(inputs =>
            {
                if (first) { first = false; foreach (var input in inputs.Take(3)) held.Add(input.Data.Keyboard.Key); throw new InvalidOperationException("simulated boundary failure"); }
                foreach (var input in inputs) { Check((input.Data.Keyboard.Flags & 2) != 0); cleanup.Add(input.Data.Keyboard.Key); held.Remove(input.Data.Keyboard.Key); }
                return (uint)inputs.Length;
            }, keyDown: key => key == 0x70);
            sink.Invoke(Shortcut.Narrator);
            Check(held.SetEquals(new ushort[] { 0x70 }) && !sink.HasHeldInputs && sink.Warning != null);
            Check(cleanup.Contains(0x5B) && cleanup.Contains(0x11) && cleanup.Contains(0x0D) && !cleanup.Contains(0x70));
        });
        test("external modifier conflicts inject neither downs nor unsolicited releases", () =>
        {
            foreach (ushort held in new ushort[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0xA0, 0xA3, 0xA5 })
            {
                int calls = 0; var sink = new WindowsInputSink(inputs => { calls++; return (uint)inputs.Length; }, keyDown: key => key == held);
                sink.Invoke(Shortcut.Narrator); sink.Invoke(Shortcut.ZoomIn); sink.InvokeKeys([0x41]); sink.ReleaseAll();
                Check(calls == 0 && !sink.HasHeldInputs && sink.Warning != null);
            }
        });
        test("failed modifier cleanup also blocks same-frame pointer clicks and scroll", () =>
        {
            bool first = true; bool blocked = true; int mouseCalls = 0;
            var sink = new WindowsInputSink(inputs =>
            {
                if (inputs[0].Type == 0) { mouseCalls++; return (uint)inputs.Length; }
                if (first) { first = false; return 1; }
                return blocked ? 0 : (uint)inputs.Length;
            });
            sink.Invoke(Shortcut.Narrator); Check(sink.CleanupPending);
            sink.Move(5, 2); sink.Scroll(120, 0); sink.SetMouseButton(MouseButton.Left, true);
            Check(mouseCalls == 0 && sink.CleanupPending);
            blocked = false; sink.RetryReleases(); sink.Move(5, 2); Check(mouseCalls == 1 && !sink.CleanupPending);
        });
        test("a held physical mouse button is never taken over or released without ownership", () =>
        {
            int calls = 0; var sink = new WindowsInputSink(inputs => { calls++; return (uint)inputs.Length; }, keyDown: key => key == 1);
            sink.SetMouseButton(MouseButton.Left, true); sink.SetMouseButton(MouseButton.Left, false); sink.ReleaseAll();
            Check(calls == 0 && !sink.HasHeldInputs);
        });
        test("concurrent accessibility and ordinary shortcuts cannot interleave native batches", () =>
        {
            int active = 0, maximum = 0, calls = 0;
            var sink = new WindowsInputSink(inputs =>
            {
                int now = Interlocked.Increment(ref active); maximum = Math.Max(maximum, now); Interlocked.Increment(ref calls);
                Check(inputs.Length >= 2); Interlocked.Decrement(ref active); return (uint)inputs.Length;
            });
            Parallel.For(0, 1000, i => { if (i % 3 == 0) sink.Invoke(Shortcut.Narrator); else if (i % 3 == 1) sink.Invoke(Shortcut.ZoomIn); else sink.InvokeKeys([0x11, 0x41]); });
            Check(maximum == 1 && calls == 1000 && !sink.HasHeldInputs);
        });
        test("reentrant shortcut during a native batch is rejected without nested injection", () =>
        {
            WindowsInputSink? sink = null; int calls = 0;
            sink = new WindowsInputSink(inputs => { calls++; sink!.Invoke(Shortcut.ZoomIn); return (uint)inputs.Length; });
            sink.Invoke(Shortcut.Narrator); Check(calls == 1 && !sink.HasHeldInputs);
        });
        foreach (string boundary in new[] { "disconnect", "pause", "profile", "reset" })
            test($"{boundary} cleans accepted modifier and drag state without requiring button-up", () =>
            {
                bool blocked = true; bool firstKeyboard = true; var held = new HashSet<ushort>(); bool mouse = false;
                var sink = new WindowsInputSink(inputs =>
                {
                    if (inputs[0].Type == 0) { mouse = inputs[0].Data.Mouse.Flags == 2; return (uint)inputs.Length; }
                    if (firstKeyboard) { firstKeyboard = false; held.Add(inputs[0].Data.Keyboard.Key); return 1; }
                    if (blocked) return 0;
                    foreach (var input in inputs) if ((input.Data.Keyboard.Flags & 2) != 0) held.Remove(input.Data.Keyboard.Key);
                    return (uint)inputs.Length;
                });
                var engine = new InputProcessor(sink); engine.Process(Pad(), .008); engine.Process(Pad(Buttons.A), .008);
                sink.Invoke(Shortcut.Narrator); Check(mouse && held.Count > 0 && sink.HasHeldInputs);
                blocked = false;
                switch (boundary)
                {
                    case "disconnect": engine.Process(null, .008); break;
                    case "pause": engine.SetEnabled(false); break;
                    case "profile": engine.Settings = new Settings(); break;
                    default: engine.Reset(); break;
                }
                Check(!mouse && held.Count == 0 && !sink.HasHeldInputs && !engine.Armed);
                engine.Process(Pad(Buttons.A), .008); Check(!mouse); // No held-input replay.
            });
        test("explicit repeat follows time across polling rates and skips missed bursts", () =>
        {
            int Count(double dt)
            {
                var (engine, _, batches) = Ready(new Settings
                {
                    RepeatDelayMilliseconds = 300,
                    RepeatIntervalMilliseconds = 100,
                    ButtonMappings = new() { [Control.A] = new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn) { Repeat = true } }
                });
                engine.Process(Pad(Buttons.A), dt);
                for (int i = 0; i < (int)Math.Round(1 / dt); i++) engine.Process(Pad(Buttons.A), dt);
                engine.Process(Pad(), dt); return batches.Count;
            }
            Check(Count(.008) == Count(.02) && Count(.02) == 9);
            var repeat = new RepeatScheduler(); Check(repeat.ShouldFire(Control.A, true, true, 0, .3, .1));
            Check(repeat.ShouldFire(Control.A, true, false, 5, .3, .1));
            Check(!repeat.ShouldFire(Control.A, true, false, .001, .3, .1));
            repeat.Reset(); Check(!repeat.ShouldFire(Control.A, true, false, .5, .3, .1));
        });
        test("one-shot toggles and combination actions cannot acquire a repeat policy", () =>
        {
            Check(!new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.Narrator) { Repeat = true }.IsValid);
            Check(!new ButtonChord(Control.A, Control.B, new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn) { Repeat = true }).IsValid);
            Check(new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn) { Repeat = true }.IsValid);
        });
        test("Narrator start stop and Magnifier start use semantic state checks without killing processes", () =>
        {
            var running = new HashSet<string>(); var starts = new List<string>(); var keys = new List<ushort[]>();
            var executor = new AccessibilityActionExecutor(running.Contains, name => { starts.Add(name); running.Add(name); });
            var sink = new WindowsInputSink(inputs => { keys.Add(inputs.Where(i => (i.Data.Keyboard.Flags & 2) == 0).Select(i => i.Data.Keyboard.Key).ToArray()); return (uint)inputs.Length; }, accessibility: executor);
            sink.Invoke(Shortcut.NarratorStop); Check(keys.Count == 0);
            sink.Invoke(Shortcut.NarratorStart); sink.Invoke(Shortcut.NarratorStart);
            sink.Invoke(Shortcut.MagnifierStart); sink.Invoke(Shortcut.MagnifierStart);
            Check(starts.SequenceEqual(new[] { "Narrator", "Magnify" }) && keys.Count == 0);
            sink.Invoke(Shortcut.NarratorStop); Check(keys.Single().SequenceEqual(new ushort[] { 0x5B, 0x11, 0x0D }) && !sink.HasHeldInputs);
        });
        test("accessibility launch failure does not stop subsequent pointer or shortcut output", () =>
        {
            var sent = new List<NativeMethods.Input>();
            var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return (uint)inputs.Length; },
                accessibility: new(_ => false, _ => throw new InvalidOperationException("launch failed")));
            sink.Invoke(Shortcut.NarratorStart); Check(sink.Warning != null && !sink.HasHeldInputs);
            sink.Move(5, 2); sink.Invoke(Shortcut.ZoomIn); Check(sent.Count == 5 && !sink.HasHeldInputs);
        });
        test("opt-in diagnostic ring is bounded and omits keyboard contents", () =>
        {
            var log = new InputDiagnostics(); log.Write(DiagnosticLevel.Info, "test", "off"); Check(log.Snapshot().Length == 0);
            log.Enabled = true;
            Parallel.For(0, 2000, i => log.Write(DiagnosticLevel.Trace, "test", i.ToString()));
            Check(log.Snapshot().Length == InputDiagnostics.Capacity); log.Clear();
            var sink = new WindowsInputSink(inputs => (uint)inputs.Length, diagnostics: log); sink.InvokeKeys([0x41]);
            Check(log.Snapshot().All(e => !e.Detail.Contains("0x41") && !e.Detail.Contains("Keyboard.A")));
            Check(log.Snapshot().Any(e => e.Stage == "OUTPUT"));
        });
        test("safe navigation profile is explicit and does not change the default D-pad pointer", () =>
        {
            var settings = AccessibilityProfiles.NavigationTest.Settings.Validate();
            Check(settings.ButtonChords!.Length == 0 && !settings.Haptics && !settings.TargetAssist && settings.MotionLearning == LearningMode.Off);
            Check(ControlMapping.Resolve(settings, Control.Up).Shortcut == Shortcut.Up);
            Check(ControlMapping.Resolve(new Settings(), Control.Up).Kind == BindingKind.Pointer);
            Check(ControlMapping.Resolve(settings, Control.A).Kind == BindingKind.Mouse);
            Check(ControlMapping.Resolve(settings, Control.LeftStick).Shortcut == Shortcut.Narrator);
            Check(!ControlMapping.Resolve(settings, Control.RightTrigger).Repeat);
        });
        test("synthetic rapid mixed-input torture leaves no Relay-owned keys buttons or scrolling", () =>
        {
            var held = new HashSet<ushort>(); var mouse = new HashSet<uint>();
            var sink = new WindowsInputSink(inputs =>
            {
                foreach (var i in inputs)
                {
                    if (i.Type == 1) { if ((i.Data.Keyboard.Flags & 2) != 0) held.Remove(i.Data.Keyboard.Key); else held.Add(i.Data.Keyboard.Key); }
                    else if (i.Data.Mouse.Flags is 2 or 8 or 32) mouse.Add(i.Data.Mouse.Flags);
                    else if (i.Data.Mouse.Flags is 4 or 16 or 64) mouse.Remove(i.Data.Mouse.Flags / 2);
                }
                return (uint)inputs.Length;
            });
            var settings = AccessibilityProfiles.NavigationTest.Settings;
            var engine = new InputProcessor(sink) { Settings = settings }; engine.Process(Pad(), .008);
            var random = new Random(42); var clock = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                engine.Process(new((Buttons)random.Next(65536) & ~(Buttons.Back | Buttons.Start), (byte)random.Next(256), (byte)random.Next(256),
                    (short)random.Next(-32768, 32768), (short)random.Next(-32768, 32768), (short)random.Next(-32768, 32768), (short)random.Next(-32768, 32768)), .008);
                if (i % 113 == 0) { engine.Process(null, .008); engine.Process(Pad(), .008); }
                if (i % 227 == 0) { engine.Settings = settings; engine.Process(Pad(), .008); }
                if (i % 331 == 0) { engine.SetEnabled(false); engine.SetEnabled(true); engine.Process(Pad(), .008); }
            }
            engine.Reset(); sink.ReleaseAll(); Check(held.Count == 0 && mouse.Count == 0 && !sink.HasHeldInputs && !engine.ScrollActive);
            Console.WriteLine($"  Fake-boundary torture: 10,000 frames in {clock.Elapsed.TotalMilliseconds:0.0} ms (not an OS latency measurement).");
        });
    }
}
