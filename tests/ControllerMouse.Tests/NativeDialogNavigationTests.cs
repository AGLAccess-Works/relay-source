using ControllerMouse.App;
using ControllerMouse.Core;

internal static class NativeDialogNavigationTests
{
    private static readonly IntPtr Dialog = new(731);
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }
    private static bool Navigate(RelayNavigationCommand command, WindowsInputSink sink,
        Func<bool>? current = null, Func<IntPtr>? foreground = null, Func<IntPtr, uint>? owner = null, IntPtr? dialog = null) =>
        NativeDialogNavigation.TryNavigate(command, dialog ?? Dialog, current ?? (() => true),
            foreground ?? (() => Dialog), owner ?? (_ => (uint)Environment.ProcessId), sink);

    public static void Run(Action<string, Action> test)
    {
        foreach (var (command, keys) in new (RelayNavigationCommand, ushort[])[]
        {
            (RelayNavigationCommand.Up, [0x26]), (RelayNavigationCommand.Down, [0x28]),
            (RelayNavigationCommand.Left, [0x25]), (RelayNavigationCommand.Right, [0x27]),
            (RelayNavigationCommand.Next, [0x09]), (RelayNavigationCommand.Previous, [0x10, 0x09]),
            (RelayNavigationCommand.Select, [0x0D]), (RelayNavigationCommand.Back, [0x1B])
        })
            test($"Native Relay dialog {command} emits exactly one safe balanced owned tap", () =>
            {
                var submitted = new List<NativeMethods.Input[]>();
                var sink = new WindowsInputSink(inputs => { submitted.Add(inputs); return (uint)inputs.Length; });
                Check(Navigate(command, sink));
                Check(submitted.Count == 1 && submitted[0].All(input => input.Type == 1), "A navigation tap emitted an extra batch or mouse input.");
                var keyboard = submitted[0].Select(input => input.Data.Keyboard).ToArray();
                Check(keyboard.Select(input => input.Key).SequenceEqual(keys.Concat(keys.Reverse())));
                Check(keyboard.Take(keys.Length).All(input => (input.Flags & 2) == 0) &&
                    keyboard.Skip(keys.Length).All(input => (input.Flags & 2) != 0));
                Check(keyboard.All(input => ((input.Flags & 1) != 0) == (input.Key is >= 0x21 and <= 0x28)));
                Check(!sink.HasHeldInputs && !sink.CleanupPending, "A successful native dialog tap retained an owned key.");
            });

        foreach (string failure in new[] { "paused or quarantined", "zero dialog", "different foreground", "different process", "invalid command" })
            test($"Native Relay dialog rejects {failure} without any Windows output", () =>
            {
                int submitted = 0;
                var sink = new WindowsInputSink(inputs => { submitted++; return (uint)inputs.Length; });
                bool result = Navigate(failure == "invalid command" ? (RelayNavigationCommand)99 : RelayNavigationCommand.Select, sink,
                    current: () => failure != "paused or quarantined",
                    foreground: () => failure == "different foreground" ? new IntPtr(99) : Dialog,
                    owner: _ => failure == "different process" ? (uint)Environment.ProcessId + 1 : (uint)Environment.ProcessId,
                    dialog: failure == "zero dialog" ? IntPtr.Zero : Dialog);
                Check(!result && submitted == 0 && !sink.HasHeldInputs);
            });

        test("Native Relay dialog rechecks foreground immediately before the owned tap", () =>
        {
            int reads = 0, submitted = 0;
            var sink = new WindowsInputSink(inputs => { submitted++; return (uint)inputs.Length; });
            Check(!Navigate(RelayNavigationCommand.Select, sink, foreground: () => ++reads == 1 ? Dialog : new IntPtr(99)));
            Check(reads == 2 && submitted == 0 && !sink.HasHeldInputs);
        });
        test("Native Relay dialog rechecks navigation cancellation before the owned tap", () =>
        {
            int reads = 0, submitted = 0;
            var sink = new WindowsInputSink(inputs => { submitted++; return (uint)inputs.Length; });
            Check(!Navigate(RelayNavigationCommand.Select, sink, current: () => ++reads == 1));
            Check(reads == 2 && submitted == 0 && !sink.HasHeldInputs);
        });
        test("Native Relay dialog preserves externally held keyboard modifiers without output", () =>
        {
            int submitted = 0;
            var sink = new WindowsInputSink(inputs => { submitted++; return (uint)inputs.Length; }, keyDown: key => key == 0x10);
            Check(!Navigate(RelayNavigationCommand.Select, sink) && submitted == 0 && !sink.HasHeldInputs);
        });
        test("Native Relay dialog partial key delivery blocks later navigation until owned cleanup succeeds", () =>
        {
            var submitted = new List<NativeMethods.Input[]>(); bool cleanupAccepted = false;
            var sink = new WindowsInputSink(inputs =>
            {
                submitted.Add(inputs);
                return inputs.Length > 1 ? 1u : cleanupAccepted ? 1u : 0u;
            });
            Check(!Navigate(RelayNavigationCommand.Select, sink) && sink.CleanupPending && sink.HasHeldInputs);
            int before = submitted.Count;
            Check(!Navigate(RelayNavigationCommand.Next, sink) && submitted.Count == before,
                "New navigation attempted an input while an owned key-up remained pending.");
            cleanupAccepted = true; sink.RetryReleases();
            Check(!sink.CleanupPending && !sink.HasHeldInputs && submitted.Last().Length == 1 &&
                submitted.Last()[0].Data.Keyboard.Key == 0x0D && (submitted.Last()[0].Data.Keyboard.Flags & 2) != 0,
                "Cleanup did not release only the key Relay owned.");
        });
        test("Native Relay dialog rejects navigation while an owned mouse release is pending", () =>
        {
            int submitted = 0;
            var sink = new WindowsInputSink(inputs =>
            {
                submitted++;
                return inputs[0].Type == 0 && inputs[0].Data.Mouse.Flags == 4 ? 0u : (uint)inputs.Length;
            });
            sink.SetMouseButton(MouseButton.Left, true); sink.SetMouseButton(MouseButton.Left, false);
            Check(sink.CleanupPending); int before = submitted;
            Check(!Navigate(RelayNavigationCommand.Select, sink) && submitted == before && sink.HasHeldInputs);
        });

        foreach (var (name, reset) in new (string, Action<InputProcessor>)[]
        {
            ("pause/resume", processor => { processor.SetEnabled(false); processor.SetEnabled(true); }),
            ("profile settings", processor => processor.Settings = new Settings()),
            ("capture/reset", processor => processor.Reset()),
            ("typing keyboard", processor => { processor.SetKeyboardOpen(true); processor.SetKeyboardOpen(false); }),
            ("disconnect/reconnect", processor => { processor.Process(null, .008); processor.Process(default(Gamepad), .008); }),
            ("cleanup quarantine", processor => processor.InvalidateRelayNavigation())
        })
            test($"Native Relay dialog discards a queued Select after {name} even when rearmed", () =>
            {
                int submitted = 0;
                var sink = new WindowsInputSink(inputs => { submitted++; return (uint)inputs.Length; });
                var processor = new InputProcessor(sink) { Settings = new Settings(), RelayWindowActive = true };
                processor.Process(default(Gamepad), .008);
                long queued = processor.NavigationGeneration;
                reset(processor); processor.Process(default(Gamepad), .008);
                Check(processor.Enabled && processor.Armed && queued != processor.NavigationGeneration);
                Check(!Navigate(RelayNavigationCommand.Select, sink,
                    current: () => processor.Enabled && processor.Armed && queued == processor.NavigationGeneration));
                Check(submitted == 0 && !sink.HasHeldInputs);
            });
    }
}
