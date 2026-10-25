using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal static class KeyboardVerification
{
    internal static async Task RenderPreview(string output)
    {
        var received = new List<ushort[]>();
        var keyboard = new KeyboardWindow(keys => { received.Add(keys); return true; });
        try
        {
            var before = NativeMethods.GetForegroundWindow();
            keyboard.ShowNearPointer(); keyboard.UpdateLayout();
            if (NativeMethods.GetForegroundWindow() != before) throw new InvalidOperationException("Keyboard preview stole foreground focus.");
            long style = NativeMethods.GetWindowLongPtr(new WindowInteropHelper(keyboard).Handle, -20).ToInt64();
            if ((style & 0x08000000) == 0) throw new InvalidOperationException("Keyboard is missing WS_EX_NOACTIVATE.");
            await VerifyHoverReading(keyboard, received, before, output);
            VerifyAutomation(keyboard);
            var shift = UIElementAutomationPeer.CreatePeerForElement(keyboard.KeyButton(0x10))!;
            var shiftToggle = shift.GetPattern(PatternInterface.Toggle) as IToggleProvider
                ?? throw new InvalidOperationException("Shift lacks a Narrator-operable Toggle pattern.");
            shiftToggle.Toggle();
            if (!keyboard.ModifierSelected(0x10) || shiftToggle.ToggleState != ToggleState.On)
                throw new InvalidOperationException("Shift's spoken toggle state does not match the selected modifier.");
            var letter = UIElementAutomationPeer.CreatePeerForElement(keyboard.KeyButton(0x41))!.GetPattern(PatternInterface.Invoke) as IInvokeProvider
                ?? throw new InvalidOperationException("Letter A lacks a Narrator-operable Invoke pattern.");
            letter.Invoke();
            await keyboard.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (received.Count != 1 || !System.Linq.Enumerable.SequenceEqual(received[0], new ushort[] { 0x10, 0x41 }) || keyboard.ModifierSelected(0x10))
                throw new InvalidOperationException("Keyboard modifier binding failed.");
            if (shiftToggle.ToggleState != ToggleState.Off || NativeMethods.GetForegroundWindow() != before)
                throw new InvalidOperationException("Narrator-operable key activation retained Shift or took editor focus.");
            foreach (ushort key in new ushort[] { 0x10, 0x11, 0x12, 0x5b })
            {
                var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(keyboard.KeyButton(key))!.GetPattern(PatternInterface.Toggle);
                toggle.Toggle();
                if (!keyboard.ModifierSelected(key) || toggle.ToggleState != ToggleState.On)
                    throw new InvalidOperationException("Automation could not select a keyboard modifier.");
                toggle.Toggle();
                if (keyboard.ModifierSelected(key) || toggle.ToggleState != ToggleState.Off)
                    throw new InvalidOperationException("Automation could not cancel a keyboard modifier.");
            }
            var status = Descendants(keyboard).OfType<TextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "RelayKeyboardStatus");
            if (keyboard.SelectedKey != 0x1b)
                throw SelectionFailure(keyboard, status, "Showing or refreshing the keyboard changed the initial Escape selection.");
            keyboard.ControllerCommand(Shortcut.KeyboardRight);
            VerifySelectedAnnouncement(keyboard, status, 0x51);
            // WPF can deliver these events when layout or a modifier rebuilds a key
            // under the unmoved pointer. They must not redirect the next controller press.
            foreach (ushort key in new ushort[] { 0x57, 0xfffe, 0xfffd })
            {
                var button = keyboard.KeyButton(key);
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent });
                VerifySelectedAnnouncement(keyboard, status, 0x51);
            }
            keyboard.ControllerCommand(Shortcut.KeyboardSelect);
            if (received.Count != 2 || received[1].Length != 1 || received[1][0] != 0x51) throw new InvalidOperationException("Controller key selection did not type Q.");
            keyboard.ControllerCommand(Shortcut.KeyboardSymbols);
            if (!keyboard.SymbolsVisible) throw new InvalidOperationException("Number page did not open.");
            VerifyAutomation(keyboard);
            Capture(keyboard, Path.Combine(output, "relay-keyboard-symbols.png"));
            keyboard.ControllerCommand(Shortcut.KeyboardSymbols);
            keyboard.ControllerCommand(Shortcut.KeyboardSpace);
            if (received[^1][0] != 0x20) throw new InvalidOperationException("Space controller shortcut failed.");
            keyboard.ControllerCommand(Shortcut.KeyboardUp);
            keyboard.ControllerCommand(Shortcut.KeyboardRight);
            Capture(keyboard, Path.Combine(output, "relay-keyboard.png"));
            keyboard.Width = 640; keyboard.Height = 340;
            await keyboard.Dispatcher.InvokeAsync(() => keyboard.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            Capture(keyboard, Path.Combine(output, "relay-keyboard-small.png"));
            await VerifyTypography(keyboard, received, before, output);
            using (var identity = new ControllerIdentityObserver())
            {
                identity.Start();
                if (!identity.FirstScan.Wait(TimeSpan.FromSeconds(3))) throw new InvalidOperationException("Controller model discovery did not complete its first scan.");
                var inventory = identity.Snapshot;
                File.WriteAllText(Path.Combine(output, "controller-model.txt"), "Controller metadata observer starts and stops without input. " +
                    (inventory.Warning ?? $"Detected {inventory.Devices.Length} Windows gamepads. Hardware recognition requires physical USB/Bluetooth validation."));
            }
            keyboard.ControllerCommand(Shortcut.KeyboardUp);
            keyboard.ControllerCommand(Shortcut.KeyboardUp);
            keyboard.ControllerCommand(Shortcut.KeyboardUp);
            keyboard.ControllerCommand(Shortcut.KeyboardUp);
            if (keyboard.SelectedKey != 0xfffe) throw new InvalidOperationException("Paste cannot be reached with controller selection.");
            keyboard.ControllerCommand(Shortcut.KeyboardSelect);
            if (!System.Linq.Enumerable.SequenceEqual(received[^1], new ushort[] { 0x11, 0x56 })) throw new InvalidOperationException("Selected Paste command was not delivered.");
            keyboard.ControllerCommand(Shortcut.KeyboardRight);
            keyboard.ControllerCommand(Shortcut.KeyboardSelect);
            if (keyboard.IsVisible) throw new InvalidOperationException("Hide cannot be selected with the controller.");
            keyboard.ShowNearPointer();
            if (keyboard.ModifierSelected(0x10)) throw new InvalidOperationException("Keyboard retained a modifier after hiding.");
            if (keyboard.SelectedKey != 0x1b || NativeMethods.GetForegroundWindow() != before)
                throw new InvalidOperationException("Reopening keyboard did not select Escape safely without stealing editor focus.");
        }
        finally { keyboard.Close(); }
        await VerifyFixedSwitchKeyboard(output);
        File.WriteAllText(Path.Combine(output, "keyboard-preview.txt"), "PASS: nonactivating keyboard; key hover reading without typing, selecting, toggling or changing editor focus; controller/rebuild/hide cancellation; UI Automation names, stable identifiers, Invoke and modifier Toggle patterns; automation activation keeps editor focus; modifier On/Off agrees with selected state; controller key selection exposed in a polite debounced live region; stationary-pointer enter/move events cannot redirect controller selection; numbers/symbols, space, modifier consumption, hide/reopen Escape reset, full-size and 640x340 layouts. Fixed switch and pedal profiles use A/B/X/Y alone to reach every enabled key and toolbar action, select recorded letters/space/erase/modifiers/Paste, change pages and hide the keyboard; visible help matches those assignments. Output recorded only; no input injected. Live Narrator speech still requires physical validation.");
    }

    private static async Task VerifyFixedSwitchKeyboard(string output)
    {
        var report = new List<string>();
        foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
        {
            int nativeCalls = 0;
            var received = new List<ushort[]>();
            var sink = new WindowsInputSink(inputs => { nativeCalls++; return (uint)inputs.Length; });
            var engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = method }, new()) };
            var keyboard = new KeyboardWindow(keys => { received.Add(keys); return true; }, () => true,
                () => engine.FixedSwitchKeyboardLayout);
            sink.KeyboardCommandRequested = keyboard.ControllerCommand;
            sink.KeyboardRequested = () => keyboard.Dispatcher.BeginInvoke(new Action(keyboard.Hide));
            keyboard.IsVisibleChanged += (_, _) => engine.SetKeyboardOpen(keyboard.IsVisible);
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            try
            {
                void Check(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException($"{method} keyboard verification: {message}");
                }
                async Task Drain() => await keyboard.Dispatcher.InvokeAsync(keyboard.UpdateLayout, DispatcherPriority.ApplicationIdle);
                async Task Press(Buttons button)
                {
                    engine.Process(default(Gamepad), .008);
                    engine.Process(new(button, 0, 0, 0, 0, 0, 0), .008);
                    engine.Process(default(Gamepad), .008);
                    await Drain();
                    Check(nativeCalls == 0 && !sink.HasHeldInputs, "a switch navigation command injected desktop input.");
                    Check(NativeMethods.GetForegroundWindow() == foreground, "keyboard navigation changed editor focus.");
                }
                async Task Reach(ushort key)
                {
                    for (int step = 0; keyboard.SelectedKey != key && step <= keyboard.AllKeyButtons.Count(); step++)
                        await Press(Buttons.B);
                    Check(keyboard.SelectedKey == key, $"B cannot reach key 0x{key:X4}.");
                }
                TextBlock Status() => Descendants(keyboard).OfType<TextBlock>()
                    .Single(text => AutomationProperties.GetAutomationId(text) == "RelayKeyboardStatus");
                async Task TraversePage(string page)
                {
                    int sent = received.Count, origin = keyboard.SelectedKey;
                    var expected = keyboard.AllKeyButtons.Where(button => button.IsEnabled).ToHashSet();
                    foreach (var direction in new[] { Buttons.B, Buttons.X })
                    {
                        var reached = new HashSet<ButtonBase>();
                        for (int step = 0; step < expected.Count; step++)
                        {
                            var selected = keyboard.KeyButton((ushort)keyboard.SelectedKey);
                            reached.Add(selected);
                            var peer = UIElementAutomationPeer.CreatePeerForElement(selected)!;
                            Check(peer.GetItemStatus().Contains("Selected key", StringComparison.Ordinal),
                                "the selected key has no spoken current state.");
                            await Press(direction);
                            VerifySelectedAnnouncement(keyboard, Status(), (ushort)keyboard.SelectedKey);
                        }
                        Check(reached.SetEquals(expected) && keyboard.SelectedKey == origin,
                            $"{direction} does not wrap through every enabled {page} key and Paste/Hide.");
                    }
                    Check(received.Count == sent, "exploring keys typed a character or toolbar action.");
                    report.Add($"PASS {method}: B and X each reach all {expected.Count} enabled {page} keys plus toolbar actions, wrap safely, expose selection in UIA/live text and send no desktop input.");
                }

                keyboard.ShowNearPointer(); await Drain(); engine.Process(default(Gamepad), .008);
                string help = AutomationProperties.GetHelpText(keyboard);
                Check(help.Contains("B: next key", StringComparison.Ordinal) && help.Contains("X: previous key", StringComparison.Ordinal) &&
                    help.Contains("Y: hide", StringComparison.Ordinal) && Status().Text.Contains("B: next key", StringComparison.Ordinal),
                    "visible and spoken help still requires a D-pad or says B hides the keyboard.");
                Check(!VisualElements(keyboard.KeyButton(0x20)).OfType<TextBlock>().Any(text => text.Text == "Y") &&
                    !VisualElements(keyboard.KeyButton(0x08)).OfType<TextBlock>().Any(text => text.Text == "X") &&
                    keyboard.KeyButton(0xfffd).Content?.ToString()?.Contains("Y", StringComparison.Ordinal) == true,
                    "key hints still describe Y as Space or X as erase rather than four-switch navigation.");
                VerifyAutomation(keyboard);
                await TraversePage("letters");
                Capture(keyboard, Path.Combine(output, $"relay-keyboard-{method.ToString().ToLowerInvariant()}.png"));

                await Reach(0x51); ushort letter = (ushort)keyboard.KeyButton(0x51).Tag;
                await Press(Buttons.A); Check(received[^1].SequenceEqual([letter]), "A did not type the selected letter.");
                await Reach(0x20); await Press(Buttons.A); Check(received[^1].SequenceEqual(new ushort[] { 0x20 }), "Space cannot be selected with four switches.");
                await Reach(0x08); await Press(Buttons.A); Check(received[^1].SequenceEqual(new ushort[] { 0x08 }), "Backspace cannot be selected with four switches.");
                await Reach(0x10); await Press(Buttons.A); Check(keyboard.ModifierSelected(0x10), "A cannot select Shift.");
                await Reach(0x51); await Press(Buttons.A);
                Check(received[^1].SequenceEqual(new ushort[] { 0x10, letter }) && !keyboard.ModifierSelected(0x10),
                    "selected Shift was not consumed once by the selected letter.");

                await Reach(0); await Press(Buttons.A); Check(keyboard.SymbolsVisible, "the symbol page requires a trigger.");
                await TraversePage("symbols"); VerifyAutomation(keyboard);
                Capture(keyboard, Path.Combine(output, $"relay-keyboard-{method.ToString().ToLowerInvariant()}-symbols.png"));
                await Reach(0); await Press(Buttons.A); Check(!keyboard.SymbolsVisible, "letters cannot be restored without a trigger.");
                await Reach(0xfffe); await Press(Buttons.A);
                Check(received[^1].SequenceEqual(new ushort[] { 0x11, 0x56 }), "Paste cannot be selected using A/B/X/Y.");
                await Reach(0xfffd); await Press(Buttons.A); Check(!keyboard.IsVisible, "A cannot select the toolbar Hide action.");
                keyboard.ShowNearPointer(); await Drain(); engine.Process(default(Gamepad), .008);
                Check(keyboard.SelectedKey == 0x1b && !keyboard.ModifierSelected(0x10), "reopening retained an old key or modifier.");
                await Press(Buttons.Y); Check(!keyboard.IsVisible && !engine.KeyboardOpen, "Y did not hide the fixed-layout keyboard.");
                report.Add($"PASS {method}: A selects the actual letter, Space, Backspace, Shift, page toggle, Paste and Hide controls; Y hides directly; reopen resets Escape and modifiers; no D-pad, bumpers, triggers or stick required.");
            }
            finally { keyboard.Close(); engine.Reset(); sink.ReleaseAll(); }
        }
        report.Add("All typing is captured by a fake output delegate. WindowsInputSink native calls remain zero; no user settings, clipboard contents or text windows are changed. Live Narrator listening remains a manual check.");
        File.WriteAllLines(Path.Combine(output, "keyboard-switches.txt"), report);
    }

    private static async Task VerifyHoverReading(KeyboardWindow keyboard, List<ushort[]> received, IntPtr foreground, string output)
    {
        // Showing and positioning the nonactivating window posts Loaded, render
        // and mouse-boundary events. Let those finish before beginning a dwell.
        await keyboard.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var report = new List<string>();
        var readings = new List<(FrameworkElement Target, string Text)>();
        bool enabled = true;
        var focused = Keyboard.FocusedElement;
        int sent = received.Count, selected = keyboard.SelectedKey;
        using var reader = new RelayHoverReader(keyboard, () => enabled,
            (target, text) => readings.Add((target, text)), () => true);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Keyboard hover verification: " + message);
        }
        void Preserved()
        {
            Check(received.Count == sent, "hover typed or sent a shortcut.");
            Check(keyboard.SelectedKey == selected, "hover changed controller key selection.");
            Check(NativeMethods.GetForegroundWindow() == foreground && Keyboard.FocusedElement == focused,
                "hover changed the editor's foreground or keyboard focus.");
        }
        Check(!keyboard.IsActive && keyboard.IsVisible, "test keyboard is not nonactivating and visible.");
        var production = RelayHoverReader.ForWindow(keyboard);
        Check(production != null && production.ObservesRoot(keyboard), "keyboard has no production hover owner.");

        var letter = keyboard.KeyButton(0x41);
        reader.Observe(letter, new Point(20, 20));
        Check(readings.Count == 0, "a key was read before dwell.");
        bool pendingAtStart = reader.HasPendingRequest;
        bool loadedAtStart = letter.IsLoaded;
        await Task.Delay(RelayHoverReader.Dwell + TimeSpan.FromMilliseconds(100));
        Check(readings.Count == 1 && readings[0].Target == letter &&
            readings[0].Text.Contains(AutomationProperties.GetName(letter), StringComparison.Ordinal),
            "a nonactivating letter key did not expose its displayed text after dwell. " +
            $"Readings={readings.Count}; requested={AutomationProperties.GetAutomationId(letter)} '{AutomationProperties.GetName(letter)}'; " +
            $"actual={string.Join(" | ", readings.Select(reading => AutomationProperties.GetAutomationId(reading.Target) + ": " + reading.Text))}; " +
            $"pending-at-start={pendingAtStart}; pending-now={reader.HasPendingRequest}; loaded-at-start={loadedAtStart}; " +
            $"loaded-now={letter.IsLoaded}; visible={letter.IsVisible}; keyboard-visible={keyboard.IsVisible}; " +
            $"active={keyboard.IsActive}; key-focused={letter.IsKeyboardFocusWithin}.");
        Preserved();
        report.Add("PASS a real dwell reads the letter key on the nonactivating keyboard; no output, controller selection or editor focus changes.");

        var shiftButton = keyboard.KeyButton(0x10);
        var shift = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(shiftButton)!.GetPattern(PatternInterface.Toggle);
        reader.Observe(shiftButton, new Point(60, 60)); reader.CompleteDwell();
        Check(readings[^1].Target == shiftButton && readings[^1].Text.Contains("Shift", StringComparison.Ordinal) &&
            readings[^1].Text.Contains("Off", StringComparison.Ordinal) && !keyboard.ModifierSelected(0x10),
            "hovering Shift changed it or omitted its off state.");
        shift.Toggle();
        reader.Observe(shiftButton, new Point(100, 100)); reader.CompleteDwell();
        Check(readings[^1].Text.Contains("On", StringComparison.Ordinal) && keyboard.ModifierSelected(0x10),
            "hovering an already selected Shift omitted its on state or cancelled it.");
        shift.Toggle();
        foreach (ushort key in new ushort[] { 0xfffe, 0xfffd })
        {
            var button = keyboard.KeyButton(key);
            reader.Observe(button, new Point(key == 0xfffe ? 140 : 180, 140)); reader.CompleteDwell();
            Check(readings[^1].Target == button && readings[^1].Text.Contains(AutomationProperties.GetName(button), StringComparison.Ordinal),
                "Paste or Hide did not expose its action name.");
            Check(keyboard.IsVisible, "hover activated Hide.");
        }
        Preserved();
        report.Add("PASS Shift exposes On/Off without changing it; Paste and Hide are read without executing their actions.");

        int spoken = readings.Count;
        reader.Observe(letter, new Point(220, 180)); reader.Cancel(); reader.CompleteDwell();
        Check(readings.Count == spoken, "cancelled key reading survived.");
        reader.Observe(letter, new Point(220, 180)); reader.CompleteDwell();
        Check(readings.Count == spoken, "cancelled hover restarted under a stationary pointer.");
        reader.Observe(letter, new Point(260, 180)); enabled = false; reader.CompleteDwell();
        Check(readings.Count == spoken, "disabled reading announced a key.");
        enabled = true;
        reader.Observe(letter, new Point(300, 180)); reader.CompleteDwell();
        Check(readings.Count == spoken + 1, "fresh movement did not resume keyboard reading.");
        Preserved();
        report.Add("PASS cancellation waits for fresh movement and the disabled reading preference suppresses keyboard hover.");

        production!.Observe(keyboard.KeyButton(0x51), new Point(340, 180));
        Check(production.HasPendingRequest, "production reader did not accept nonactivating-keyboard dwell.");
        keyboard.ControllerCommand(Shortcut.KeyboardRight);
        Check(!production.HasPendingRequest, "controller navigation did not cancel production key hover.");
        keyboard.ControllerCommand(Shortcut.KeyboardLeft);
        Check(keyboard.SelectedKey == selected, "controller cancellation fixture could not restore the starting key.");

        production.Observe(keyboard.KeyButton(0x51), new Point(380, 180));
        Check(production.HasPendingRequest, "new movement did not prepare the rebuild fixture.");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(keyboard.KeyButton(0))!.GetPattern(PatternInterface.Invoke)).Invoke();
        await keyboard.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(keyboard.SymbolsVisible && !production.HasPendingRequest, "key-page rebuilding retained a pending hover.");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(keyboard.KeyButton(0))!.GetPattern(PatternInterface.Invoke)).Invoke();
        await keyboard.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(!keyboard.SymbolsVisible, "keyboard letters were not restored.");

        production.Observe(keyboard.KeyButton(0xfffe), new Point(420, 180));
        Check(production.HasPendingRequest, "new movement did not prepare the hide fixture.");
        keyboard.Hide();
        Check(!production.HasPendingRequest, "hiding retained a pending key hover.");
        keyboard.ShowNearPointer(); keyboard.UpdateLayout();
        Preserved();
        report.Add("PASS production keyboard hover is cancelled by D-pad commands, key-page rebuilding and hide/reopen without taking editor focus.");
        report.Add("Announcement text is recorded at a fake boundary; live Narrator speech remains a listening check. No native input or user preferences changed.");
        File.WriteAllLines(Path.Combine(output, "keyboard-hover.txt"), report);
    }

    /// <summary>Opt-in integration check. Input is restricted to these two owned test windows.</summary>
    internal static async Task RunInputTest(string output)
    {
        Directory.CreateDirectory(output);
        bool record = Environment.GetCommandLineArgs().Contains("--record-live");
        var editor = new TextBox { FontSize = record ? 36 : 24, Margin = new Thickness(20), AcceptsReturn = true };
        var phase = new TextBlock { Text = "Live screen recording · automated controller input · real Windows text entry", FontSize = 18, Margin = new Thickness(20, 12, 20, 0) };
        var body = new DockPanel();
        if (record)
        {
            DockPanel.SetDock(phase, Dock.Top); body.Children.Add(phase); body.Children.Add(editor);
        }
        var target = new Window
        {
            Title = record ? "Relay live typing" : "Relay keyboard input verification",
            Width = record ? 1100 : 620,
            Height = record ? 260 : 170,
            Left = SystemParameters.WorkArea.Left + 40,
            Top = SystemParameters.WorkArea.Top + 40,
            Content = record ? body : editor
        };
        if (record) target.Topmost = true;
        var sink = new WindowsInputSink();
        IntPtr targetHandle = IntPtr.Zero;
        var engine = new InputProcessor(sink);
        bool switches = false;
        var keyboard = new KeyboardWindow(keys => NativeMethods.GetForegroundWindow() == targetHandle && editor.IsKeyboardFocused && sink.TryInvokeKeys(keys),
            fixedSwitchControls: () => switches);
        if (record) keyboard.Title = "Relay live keyboard";
        NativeMethods.GetPhysicalCursorPos(out var originalCursor);
        bool restoreCursor = false;
        try
        {
            await NativeVerificationFocus.Acquire(target); editor.Focus();
            targetHandle = new WindowInteropHelper(target).Handle;
            await target.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (NativeMethods.GetForegroundWindow() != targetHandle || !editor.IsKeyboardFocused)
                throw new InvalidOperationException("Test window could not acquire focus. No input was sent.");
            keyboard.ShowNearPointer(); keyboard.UpdateLayout();
            engine.Process(default(Gamepad), .008);
            if (record)
            {
                File.WriteAllText(Path.Combine(output, "capture-ready.txt"), "Owned native editor and Relay keyboard are visible. Waiting for recording start.");
                int waits = 0;
                while (!File.Exists(Path.Combine(output, "record-start.txt")) && waits++ < 1200) await Task.Delay(100);
                if (!File.Exists(Path.Combine(output, "record-start.txt"))) throw new InvalidOperationException("Recording was not started. No input sent.");
                if (NativeMethods.GetForegroundWindow() != targetHandle || !editor.IsKeyboardFocused)
                    throw new InvalidOperationException("Recording start requires the owned editor to have focus. No input sent.");
                phase.Text = "Standard controller layout: A pointer click → Relay key → real Windows typing";
                await Task.Delay(1000);
            }

            async Task Click(ushort key)
            {
                if (NativeMethods.GetForegroundWindow() != targetHandle || !editor.IsKeyboardFocused)
                    throw new InvalidOperationException("Focus left the test editor; input test stopped.");
                var button = keyboard.KeyButton(key);
                var point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                var position = new NativeMethods.Point { X = (int)point.X, Y = (int)point.Y };
                if (NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(position), 2) != new WindowInteropHelper(keyboard).Handle)
                    throw new InvalidOperationException("A test key is obscured; no click sent.");
                if (!NativeMethods.SetCursorPos(position.X, position.Y)) throw new InvalidOperationException("Could not position test pointer.");
                // Same controller-to-SendInput path used by the app, with no await between press/release.
                engine.Process(new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0), .008);
                engine.Process(default(Gamepad), .008);
                await Task.Delay(record ? 700 : 150);
                if (NativeMethods.GetForegroundWindow() != targetHandle || !editor.IsKeyboardFocused)
                    throw new InvalidOperationException("Clicking a keyboard key stole editor focus.");
                if (keyboard.SelectedKey != key)
                    throw new InvalidOperationException($"Actual pointer movement did not select the clicked key: expected 0x{key:X4}, selected 0x{keyboard.SelectedKey:X4}.");
            }

            string expected = System.Windows.Automation.AutomationProperties.GetName(keyboard.KeyButton(0x52))!;
            await Click(0x52);
            if (editor.Text != expected) throw new InvalidOperationException("Controller click did not type the displayed key into the editor.");
            await Click(0x10);
            if (!keyboard.ModifierSelected(0x10)) throw new InvalidOperationException("Controller could not select Shift.");
            expected += System.Windows.Automation.AutomationProperties.GetName(keyboard.KeyButton(0x41));
            await Click(0x41);
            if (editor.Text != expected || keyboard.ModifierSelected(0x10)) throw new InvalidOperationException($"Shift output failed: expected '{expected}', received '{editor.Text}', shift selected={keyboard.ModifierSelected(0x10)}, key tag={keyboard.KeyButton(0x41).Tag}, caps={NativeMethods.GetKeyState(0x14)}.");
            await Click(0x11); await Click(0x41); await Click(0x08);
            if (editor.Text.Length != 0) throw new InvalidOperationException("Ctrl+A / Backspace did not edit the target text.");
            if (record)
            {
                expected = "";
                await Click(0x10);
                foreach (char letter in "RELAY WORKS")
                {
                    ushort key = letter == ' ' ? (ushort)0x20 : (ushort)letter;
                    expected += letter == ' ' ? " " : AutomationProperties.GetName(keyboard.KeyButton(key));
                    await Click(key);
                    if (editor.Text != expected) throw new InvalidOperationException("Live pointer typing did not reach the native editor.");
                }
                phase.Text = "Adaptive switch layout: B next · X previous · A select. Real Windows text output below.";
                engine.Reset(); switches = true;
                engine = new InputProcessor(sink) { Settings = SetupPlan.Build(new() { InputMethod = SetupInputMethod.Switches }, new()) };
                sink.KeyboardCommandRequested = keyboard.ControllerCommand;
                keyboard.Hide(); keyboard.ShowNearPointer(); keyboard.UpdateLayout();
                engine.SetKeyboardOpen(true); engine.Process(default(Gamepad), .008);
                async Task Press(Buttons button)
                {
                    if (NativeMethods.GetForegroundWindow() != targetHandle || !editor.IsKeyboardFocused)
                        throw new InvalidOperationException("Live switch typing lost its owned editor; recording stopped.");
                    engine.Process(default(Gamepad), .008); engine.Process(new(button, 0, 0, 0, 0, 0, 0), .008); engine.Process(default(Gamepad), .008);
                    await Task.Delay(25);
                }
                foreach (char letter in " SWITCHES")
                {
                    ushort key = letter == ' ' ? (ushort)0x20 : (ushort)letter;
                    int attempts = 0;
                    while (keyboard.SelectedKey != key && attempts++ < 100) await Press(Buttons.B);
                    if (keyboard.SelectedKey != key) throw new InvalidOperationException("Live switches could not reach a keyboard key.");
                    expected += letter == ' ' ? " " : AutomationProperties.GetName(keyboard.KeyButton(key));
                    await Task.Delay(400); await Press(Buttons.A); await Task.Delay(500);
                    if (editor.Text != expected) throw new InvalidOperationException("Live switch selection did not type into Windows.");
                }
                phase.Text = "Verified: real controller engine → native Windows input → this editor. Physical hardware not used.";
                File.WriteAllText(Path.Combine(output, "live-keyboard-evidence.txt"), "PASS: real WindowsInputSink, actual SendInput mouse clicks, Shift, Ctrl+A, Backspace, Relay keyboard typing and fixed-switch processor navigation/selection. Exact TextBox output verified after every letter: " + editor.Text + ". Automated gamepad states; no physical hardware used. No fake output callback. Every typing action required the owned editor foreground and focus.");
                await Task.Delay(4000);
            }
            if (sink.HasHeldInputs) throw new InvalidOperationException("Input verification left held keys or mouse buttons.");
            Capture(keyboard, Path.Combine(output, "keyboard-input-tested.png"));
            restoreCursor = true;
            File.WriteAllText(Path.Combine(output, "keyboard-input.txt"),
                "PASS: actual controller engine -> SendInput mouse click -> Relay key -> SendInput keyboard -> focused test TextBox. Plain key, Shift, Ctrl+A and Backspace delivered; editor focus retained; no inputs held. Only owned test windows received input.");
        }
        finally
        {
            engine.Reset(); sink.ReleaseAll();
            if (restoreCursor && NativeMethods.GetForegroundWindow() == targetHandle) NativeMethods.SetCursorPos(originalCursor.X, originalCursor.Y);
            keyboard.Close(); target.Close();
        }
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); using var file = File.Create(path);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
    private static void VerifyAutomation(KeyboardWindow keyboard)
    {
        var identifiers = new HashSet<string>();
        foreach (var button in keyboard.AllKeyButtons)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
            string identifier = AutomationProperties.GetAutomationId(button);
            var pattern = button is ToggleButton ? PatternInterface.Toggle : PatternInterface.Invoke;
            if (peer == null || string.IsNullOrWhiteSpace(peer.GetName()) || identifier.Length == 0 || !identifiers.Add(identifier) || peer.GetPattern(pattern) == null)
                throw new InvalidOperationException("A Relay keyboard key lacks an accessible name, unique identifier or operable action.");
        }
    }
    private static async Task VerifyTypography(KeyboardWindow keyboard, List<ushort[]> received, IntPtr foreground, string output)
    {
        int before = received.Count;
        string[] keys = ["MainFontSize", "CaptionFontSize", "PageTitleFontSize", "SectionFontSize", "ControlMinHeight"];
        var originals = keys.ToDictionary(key => key, key => keyboard.Resources.Contains(key) ? keyboard.Resources[key] : null);
        try
        {
            foreach (double scale in new[] { 1d, 2d })
            {
                foreach (var (key, size) in new[] { ("MainFontSize", 16d), ("CaptionFontSize", 14d), ("PageTitleFontSize", 32d), ("SectionFontSize", 20d), ("ControlMinHeight", 44d) })
                    keyboard.Resources[key] = size * scale;
                await keyboard.Dispatcher.InvokeAsync(keyboard.UpdateLayout, DispatcherPriority.ApplicationIdle);
                var text = VisualElements(keyboard).OfType<TextBlock>().Where(label => label.IsVisible && !string.IsNullOrWhiteSpace(label.Text)).ToArray();
                if (text.Length == 0 || text.Any(label => label.FontSize + .01 < 14 * scale) || VisualElements(keyboard).OfType<Viewbox>().Any())
                    throw new InvalidOperationException("Keyboard text is below its scaled 14px minimum or is silently reduced by a Viewbox.");
                if (keyboard.AllKeyButtons.Any(button => button.ActualWidth < 44 || button.ActualHeight < 44 || button.Focusable))
                    throw new InvalidOperationException("Large keyboard text reduced an input target or made a key take editor focus.");
                var selected = keyboard.KeyButton((ushort)keyboard.SelectedKey);
                foreach (var (name, minWidth, token) in new[] { ("KeySelectionOuter", 3d, "FocusRingBrush"), ("KeySelectionInner", 1d, "FocusInnerBrush") })
                {
                    if (selected.Template.FindName(name, selected) is not Border ring || ring.Visibility != Visibility.Visible ||
                        ring.BorderThickness.Left < minWidth || !Equals(ring.BorderBrush, selected.TryFindResource(token)))
                        throw new InvalidOperationException("The nonactivating keyboard lacks its visible, theme-aware double selection outline.");
                }
                if (received.Count != before || NativeMethods.GetForegroundWindow() != foreground)
                    throw new InvalidOperationException("Keyboard typography verification typed or changed foreground focus.");
                Capture(keyboard, Path.Combine(output, $"relay-keyboard-small-{scale * 100:0}percent.png"));
            }
            File.WriteAllText(Path.Combine(output, "keyboard-typography.txt"), "PASS: 640px keyboard at 100% and 200% uses scaled text of at least 14px without Viewbox reduction; every key remains at least 44×44 and nonfocusable. The controller-selected key has both themed outline layers. No typing or foreground change occurred. This is rendering/UIA evidence, not live Narrator listening.");
        }
        finally
        {
            foreach (var key in keys) { if (originals[key] is { } value) keyboard.Resources[key] = value; else keyboard.Resources.Remove(key); }
            await keyboard.Dispatcher.InvokeAsync(keyboard.UpdateLayout, DispatcherPriority.ApplicationIdle);
        }
    }
    private static IEnumerable<DependencyObject> VisualElements(DependencyObject root)
    {
        yield return root;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var element in VisualElements(VisualTreeHelper.GetChild(root, index))) yield return element;
    }
    private static void VerifySelectedAnnouncement(KeyboardWindow keyboard, TextBlock status, ushort expected)
    {
        string name = AutomationProperties.GetName(keyboard.KeyButton(expected));
        string spoken = UIElementAutomationPeer.CreatePeerForElement(status)!.GetName();
        if (keyboard.SelectedKey != expected || !spoken.Contains(name, StringComparison.Ordinal) || !status.Text.Contains(name, StringComparison.Ordinal))
            throw SelectionFailure(keyboard, status, $"Controller key selection 0x{expected:X4} ({name}) is not exposed in the live announcement text.");
    }
    private static InvalidOperationException SelectionFailure(KeyboardWindow keyboard, TextBlock status, string reason)
    {
        string spoken = UIElementAutomationPeer.CreatePeerForElement(status)?.GetName() ?? "<no peer>";
        string hovered = string.Join(", ", keyboard.AllKeyButtons.Where(b => b.IsMouseOver).Select(AutomationProperties.GetAutomationId));
        return new InvalidOperationException($"{reason} Selected=0x{keyboard.SelectedKey:X4}; status.Text='{status.Text}'; UIAName='{spoken}'; mouse-over='{hovered}'.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child; foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
