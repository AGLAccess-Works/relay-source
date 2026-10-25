using ControllerMouse.App;
using ControllerMouse.Core;

internal static class KeyboardTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    public static void Run(Action<string, Action> test)
    {
        test("existing keyboard action opens Relay without invoking protected Windows OSK", () =>
        {
            int opened = 0, nativeCalls = 0;
            var sink = new WindowsInputSink(inputs => { nativeCalls++; return (uint)inputs.Length; }) { KeyboardRequested = () => opened++ };
            var engine = new InputProcessor(sink); engine.Process(default(Gamepad), .008);
            for (int i = 0; i < 20; i++) engine.Process(new(Buttons.LeftStick, 0, 0, 0, 0, 0, 0), .008);
            Check(opened == 1 && nativeCalls == 0 && !sink.HasHeldInputs);
        });
        test("Windows OSK is retained as a separate explicit shortcut", () =>
        {
            var sent = new List<NativeMethods.Input>();
            var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return (uint)inputs.Length; });
            sink.Invoke(Shortcut.WindowsOnScreenKeyboard);
            Check(sent.Count == 6 && sent.Take(3).Select(i => i.Data.Keyboard.Key).SequenceEqual(new ushort[] { 0x5B, 0x11, 0x4F }));
            Check(AccessibilityCatalog.Keys(Shortcut.OnScreenKeyboard).Length == 0 && !sink.HasHeldInputs);
        });
        test("keyboard modifiers latch locally and are consumed once in deterministic order", () =>
        {
            var state = new KeyboardModifiers(); state.Toggle(0x10); state.Toggle(0x12); state.Toggle(0x11);
            Check(state.Selected(0x10));
            Check(state.Consume(0x41).SequenceEqual(new ushort[] { 0x11, 0x12, 0x10, 0x41 }));
            Check(!state.Selected(0x10) && !state.Selected(0x11) && state.Consume(0x42).SequenceEqual(new ushort[] { 0x42 }));
        });
        test("keyboard modifier deselection and hiding clear pending shortcuts", () =>
        {
            var state = new KeyboardModifiers(); state.Toggle(0x5B); state.Toggle(0x5B); Check(!state.Selected(0x5B));
            state.Toggle(0x11); state.Toggle(0x10); state.Clear();
            Check(state.Consume(0x41).SequenceEqual(new ushort[] { 0x41 }));
        });
        test("failed on-screen keyboard output reports failure and cleans accepted modifiers", () =>
        {
            var results = new Queue<uint>(new uint[] { 1, 0, 1 });
            var sink = new WindowsInputSink(_ => results.Dequeue());
            Check(!sink.TryInvokeKeys([0x10, 0x41]) && sink.HasHeldInputs);
            sink.RetryReleases(); Check(!sink.HasHeldInputs);
        });
        test("an unreleased modifier prevents a subsequent keyboard chord", () =>
        {
            int calls = 0;
            var batches = new List<NativeMethods.Input[]>();
            var sink = new WindowsInputSink(inputs => { batches.Add(inputs); return ++calls == 1 ? 1u : 0u; });
            Check(!sink.TryInvokeKeys([0x11, 0x41]));
            Check(!sink.TryInvokeKeys([0x42]) && sink.HasHeldInputs);
            Check(batches.Skip(1).SelectMany(b => b).All(i => i.Type == 1 && (i.Data.Keyboard.Flags & 2) != 0));
        });
        test("four-switch key selection reaches every uneven row and toolbar action in both directions", () =>
        {
            var selection = new KeyboardSelection();
            KeyboardKey[][] rows = [[new(0xfffe), new(0xfffd)],
                [new(0x1b), new(0x51), new(0x57), new(0x08)],
                [new(0x10), new(0x41), new(0x0d)],
                [new(0), new(0x11), new(0x20, 5), new(0xffff)]];
            int[] expected = rows.SelectMany(row => row.Select(key => key.Id)).ToArray();
            selection.SetRows(rows, expected[0]);
            var forward = new List<int> { selection.Selected };
            for (int i = 1; i < expected.Length; i++) forward.Add(selection.MoveSequential(1));
            Check(forward.SequenceEqual(expected) && selection.MoveSequential(1) == expected[0]);
            var reverse = new List<int>();
            for (int i = 0; i < expected.Length; i++) reverse.Add(selection.MoveSequential(-1));
            Check(reverse.SequenceEqual(expected.Reverse()) && selection.Selected == expected[0]);
            Check(selection.MoveSequential(0) == expected[0]);
            selection.SetRows([[new(0x20, 5)]], 0x20);
            Check(selection.MoveSequential(1) == 0x20 && selection.MoveSequential(-1) == 0x20);
        });
        test("four-switch keyboard mode requires all four actual unmodified navigation assignments", () =>
        {
            var baseline = SetupPlan.Build(new() { InputMethod = SetupInputMethod.Switches }, new());
            var engine = new InputProcessor(new KeyboardRoutingSink()) { Settings = baseline };
            Check(engine.FixedSwitchKeyboardLayout);
            foreach (var control in new[] { Control.A, Control.B, Control.X, Control.Y })
            {
                var mappings = new Dictionary<Control, ControlBinding>(baseline.ButtonMappings!);
                mappings[control] = mappings[control] with { Modifiers = 1 };
                engine.Settings = baseline with { ButtonMappings = mappings };
                Check(!engine.FixedSwitchKeyboardLayout);
                engine.SetKeyboardOpen(true);
                Check(engine.ResolveBinding(Control.B).Shortcut == Shortcut.OnScreenKeyboard &&
                    engine.ResolveBinding(Control.X).Shortcut == Shortcut.KeyboardBackspace &&
                    engine.ResolveBinding(Control.Y).Shortcut == Shortcut.KeyboardSpace);
            }
            engine.Settings = new Settings { ControllerType = ControllerPreference.Adaptive };
            Check(!engine.FixedSwitchKeyboardLayout);
        });
        test("switch and pedal keyboards route A B X Y internally without native Tab Escape click or zoom output", () =>
        {
            foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
            {
                int nativeCalls = 0, hidden = 0;
                var commands = new List<Shortcut>();
                var sink = new WindowsInputSink(inputs => { nativeCalls++; return (uint)inputs.Length; })
                { KeyboardCommandRequested = commands.Add, KeyboardRequested = () => hidden++ };
                var engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = method }, new()) };
                engine.SetKeyboardOpen(true); engine.Process(default(Gamepad), .008);
                foreach (var button in new[] { Buttons.A, Buttons.B, Buttons.X, Buttons.Y })
                {
                    engine.Process(new(button, 0, 0, 0, 0, 0, 0), .008);
                    engine.Process(default(Gamepad), .008);
                }
                Check(commands.SequenceEqual([Shortcut.KeyboardSelect, Shortcut.KeyboardNext, Shortcut.KeyboardPrevious]) &&
                    hidden == 1 && nativeCalls == 0 && !sink.HasHeldInputs);
                Check(RelayActions.Category(Shortcut.KeyboardNext) == ActionCategory.Keyboard &&
                    RelayActions.Category(Shortcut.KeyboardPrevious) == ActionCategory.Keyboard);
            }
        });
        test("four-switch-only keyboard sequence selects a character space and erase then hides", () =>
        {
            var selection = new KeyboardSelection();
            selection.SetRows([[new(0xfffe), new(0xfffd)], [new(0x1b), new(0x41)],
                [new(0x20, 5), new(0x08), new(0)]], 0x1b);
            var chosen = new List<int>(); int hidden = 0, nativeCalls = 0;
            var sink = new WindowsInputSink(inputs => { nativeCalls++; return (uint)inputs.Length; })
            {
                KeyboardCommandRequested = command =>
                {
                    if (command == Shortcut.KeyboardNext) selection.MoveSequential(1);
                    else if (command == Shortcut.KeyboardPrevious) selection.MoveSequential(-1);
                    else if (command == Shortcut.KeyboardSelect) chosen.Add(selection.Selected);
                },
                KeyboardRequested = () => hidden++
            };
            var engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = SetupInputMethod.Switches }, new()) };
            engine.SetKeyboardOpen(true); engine.Process(default(Gamepad), .008);
            foreach (var button in new[] { Buttons.B, Buttons.A, Buttons.B, Buttons.A, Buttons.B, Buttons.A,
                Buttons.X, Buttons.A, Buttons.Y })
            {
                engine.Process(new(button, 0, 0, 0, 0, 0, 0), .008);
                engine.Process(default(Gamepad), .008);
            }
            Check(chosen.SequenceEqual([0x41, 0x20, 0x08, 0x20]) && hidden == 1 && nativeCalls == 0);
        });
        test("four-switch key stepping and selection fire once per press and retain neutral after hiding", () =>
        {
            var sink = new KeyboardRoutingSink();
            var engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = SetupInputMethod.Switches }, new()) };
            engine.SetKeyboardOpen(true); engine.Process(default(Gamepad), .008);
            foreach (var (button, command) in new[] { (Buttons.A, Shortcut.KeyboardSelect), (Buttons.B, Shortcut.KeyboardNext),
                (Buttons.X, Shortcut.KeyboardPrevious), (Buttons.Y, Shortcut.OnScreenKeyboard) })
            {
                sink.Events.Clear();
                for (int i = 0; i < 150; i++) engine.Process(new(button, 0, 0, 0, 0, 0, 0), .008);
                Check(sink.Events.SequenceEqual([command.ToString()]));
                engine.Process(default(Gamepad), .008);
            }
            engine.SetKeyboardOpen(false); sink.Events.Clear();
            engine.Process(new(Buttons.B, 0, 0, 0, 0, 0, 0), .008);
            Check(!engine.Armed && sink.Events.Count == 0);
            engine.Process(default(Gamepad), .008); engine.Process(new(Buttons.B, 0, 0, 0, 0, 0, 0), .008);
            Check(sink.Events.SequenceEqual(["Keys:9"]));
        });
        test("fixed switch keyboards ignore stick movement rather than changing button ownership", () =>
        {
            var sink = new KeyboardRoutingSink();
            var engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = SetupInputMethod.Pedals }, new()) };
            engine.SetKeyboardOpen(true); engine.Process(default(Gamepad), .008);
            engine.Process(new(Buttons.None, 0, 0, 32767, 0, 32767, 0), .05);
            engine.Process(new(Buttons.B, 0, 0, 32767, 0, 32767, 0), .008);
            Check(engine.KeyboardSelectionActive && sink.Events.SequenceEqual(["KeyboardNext"]));
            engine.Process(null, .008); sink.Events.Clear();
            engine.Process(new(Buttons.X, 0, 0, 0, 0, 0, 0), .008);
            Check(!engine.Armed && sink.Events.Count == 0);
            engine.Process(default(Gamepad), .008); engine.Process(new(Buttons.X, 0, 0, 0, 0, 0, 0), .008);
            Check(sink.Events.SequenceEqual(["KeyboardPrevious"]));
        });
    }

    private sealed class KeyboardRoutingSink : IInputSink
    {
        internal List<string> Events { get; } = [];
        public void Move(int x, int y) => Events.Add("Move");
        public void Scroll(int vertical, int horizontal) => Events.Add("Scroll");
        public void SetMouseButton(MouseButton button, bool down) => Events.Add(button + ":" + down);
        public void Invoke(Shortcut shortcut) => Events.Add(shortcut.ToString());
        public void InvokeKeys(ushort[] keys) => Events.Add("Keys:" + string.Join(',', keys));
    }
}
