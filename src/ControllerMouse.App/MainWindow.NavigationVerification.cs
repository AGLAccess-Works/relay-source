using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunControllerNavigationChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Controller navigation verification requires the isolated preview without desktop output.");
        var originalSettings = _settings;
        var originalProfiles = _app.Profiles;
        int originalPage = Pages.SelectedIndex;
        var focusChanges = new List<IInputElement>();
        var commands = new List<RelayNavigationCommand>();
        var report = new StringBuilder();
        using (var pageService = new ControllerService(_settings, Path.Combine(output, "page-gate-memory.json"), "page-gate-verification"))
        {
            _app.SetVerificationService(pageService);
            try
            {
                for (int page = 0; page < Pages.Items.Count; page++)
                {
                    SelectPage(page);
                    if (pageService.ConfigurationMode) throw new InvalidOperationException("Page selection enabled the pointer-blocking configuration gate: " + _pageNames[page]);
                }
                report.AppendLine($"PASS all {Pages.Items.Count} actual PageChanged → ControllerService paths: no page selection enables the pointer-blocking configuration gate. The verification service is never started.");
                pageService.Diagnostics.Enabled = true;
                SelectPage(7);
                LightThemeCheck.IsChecked = LightThemeCheck.IsChecked != true;
                ZoomCombo.SelectedIndex = ZoomCombo.SelectedIndex == 4 ? 0 : 4;
                ReadHoveredTextCheck.IsChecked = ReadHoveredTextCheck.IsChecked != true;
                _schemeChoice!.SelectedValue = DisplayScheme.SoftLight;
                if (pageService.Diagnostics.Snapshot().Any(entry => entry.Stage == "PROFILE"))
                    throw new InvalidOperationException("Changing display/reading preferences reset the controller service.");
                SpeedSlider.Value = SpeedSlider.Value == 650 ? 700 : 650;
                if (!pageService.Diagnostics.Snapshot().Any(entry => entry.Stage == "PROFILE"))
                    throw new InvalidOperationException("The display-only update guard prevented a real pointer setting from reaching the controller service.");
                report.AppendLine("PASS real appearance, size, hover-reading and scheme controls emit no input-reset diagnostic through the actual unstarted service; changing pointer speed still applies input settings.");
            }
            finally { _app.SetVerificationService(null); _settings = originalSettings; PopulateControls(); SelectPage(originalPage); }
        }
        var sink = new NavigationVerificationSink();
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { Smoothing = 0, ClickHoldMilliseconds = 0, NarratorNavigation = true },
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command);
                // Match the production worker-to-dispatcher boundary. Each command
                // reaches the actual WPF handler, without an injected keyboard tap.
                Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        engine.Process(default(Gamepad), .008);
        KeyboardFocusChangedEventHandler changed = (_, e) =>
        {
            if (e.NewFocus != null) focusChanges.Add(e.NewFocus);
        };
        AddHandler(Keyboard.GotKeyboardFocusEvent, changed, true);
        var navigation = NavigationItems().ToArray();
        int[] expectedOrder = [0, 6, 9, 8, 3, 7, 4, 10];
        static Gamepad Pad(Buttons button = Buttons.None, short x = 0) => new(button, 0, 0, x, 0, 0, 0);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        string Name(IInputElement element) => element is UIElement ui
            ? UIElementAutomationPeer.CreatePeerForElement(ui)?.GetName() ?? ui.GetType().Name : element.GetType().Name;
        string Detail() => $"Commands [{string.Join(", ", commands)}]; focus [{string.Join(", ", focusChanges.Select(Name))}]; page {Pages.SelectedIndex}.";
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + " " + Detail());
        }
        async Task FocusSidebar(int page)
        {
            SelectPage(page);
            var radio = navigation.Single(button => button.Tag?.ToString() == page.ToString());
            radio.BringIntoView();
            Check(radio.Focus(), "Sidebar item cannot receive keyboard focus.");
            await Drain(); focusChanges.Clear(); commands.Clear();
        }
        async Task Press(Buttons button, int expectedPage, double holdSeconds, int expectedFocusChanges = 1)
        {
            focusChanges.Clear(); commands.Clear();
            for (int frame = 0; frame < Math.Max(1, Math.Ceiling(holdSeconds / .008)); frame++)
                engine.Process(Pad(button), .008);
            engine.Process(Pad(), .008);
            await Drain();
            var target = navigation.Single(radio => radio.Tag?.ToString() == expectedPage.ToString());
            Check(commands.Count == 1 && commands[0] == (button == Buttons.Down ? RelayNavigationCommand.Down : RelayNavigationCommand.Up),
                "One simulated physical D-pad edge did not produce exactly one internal command.");
            Check(focusChanges.Count == expectedFocusChanges && (expectedFocusChanges == 0 || ReferenceEquals(focusChanges[0], target)),
                "A D-pad press produced missing, duplicate or transient focus changes.");
            Check(target.IsKeyboardFocused && target.IsChecked == true && Pages.SelectedIndex == expectedPage,
                "Sidebar focus and selected page disagree after a D-pad press.");
            Check(target.IsVisible && target.IsEnabled && engine.Armed,
                "Navigation reached a hidden/disabled item or unnecessarily disarmed input.");
            report.AppendLine($"PASS {button}, hold {holdSeconds:0.000}s: {_pageNames[expectedPage]}, {focusChanges.Count} actual focus changes.");
        }
        try
        {
            Check(navigation.Select(radio => int.Parse(radio.Tag.ToString()!)).SequenceEqual(expectedOrder),
                "Sidebar traversal does not follow the visible page order.");
            await FocusSidebar(0);
            await Press(Buttons.Down, 6, .100);
            await Press(Buttons.Down, 9, 1);
            await Press(Buttons.Down, 8, .008);
            await Press(Buttons.Up, 9, .008);

            await FocusSidebar(0);
            await Press(Buttons.Up, 0, 1, 0);
            await FocusSidebar(4);
            await Press(Buttons.Down, 10, .008);
            await Press(Buttons.Down, 10, 1, 0);

            await FocusSidebar(0);
            foreach (var button in new[] { Buttons.Down, Buttons.Down, Buttons.Down, Buttons.Up, Buttons.Up, Buttons.Down })
            {
                engine.Process(Pad(button), .008); engine.Process(Pad(), .008);
            }
            await Drain();
            int[] rapidPages = [6, 9, 8, 9, 6, 9];
            Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Down, RelayNavigationCommand.Down, RelayNavigationCommand.Down,
                RelayNavigationCommand.Up, RelayNavigationCommand.Up, RelayNavigationCommand.Down }) &&
                focusChanges.SequenceEqual(rapidPages.Select(page => (IInputElement)navigation.Single(radio => radio.Tag?.ToString() == page.ToString()))),
                "Rapid released D-pad edges were lost, reordered or doubled.");
            Check(Pages.SelectedIndex == 9 && navigation.Single(radio => radio.Tag?.ToString() == "9").IsKeyboardFocused,
                "Rapid navigation did not finish on Hardware.");
            report.AppendLine("PASS rapid Down/Down/Down/Up/Up/Down: six commands and six actual focus changes in physical-edge order.");

            SelectPage(1);
            SpeedSlider.Value = 1200;
            SpeedSlider.BringIntoView(); Check(SpeedSlider.Focus(), "Pointer speed cannot receive keyboard focus.");
            await Drain(); focusChanges.Clear(); commands.Clear();
            double before = SpeedSlider.Value;
            int values = 0;
            RoutedPropertyChangedEventHandler<double> valueChanged = (_, _) => values++;
            SpeedSlider.ValueChanged += valueChanged;
            try
            {
                for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.Up), .008);
                engine.Process(Pad(), .008); await Drain();
                Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Up }) && values == 1 &&
                    Math.Abs(SpeedSlider.Value - (before + SpeedSlider.SmallChange)) < .001 && focusChanges.Count == 0 && SpeedSlider.IsKeyboardFocused,
                    "A held D-pad press changed a slider more than once or moved its focus.");
                report.AppendLine("PASS pointer-speed slider: one held Up edge changes its actual WPF value by one SmallChange; focus remains on the slider.");
            }
            finally { SpeedSlider.ValueChanged -= valueChanged; }

            await FocusSidebar(0);
            int movement = sink.MoveCalls;
            for (int frame = 0; frame < 125; frame++) engine.Process(Pad(Buttons.Down, 20000), .008);
            engine.Process(Pad(), .008); await Drain();
            Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Down }) && focusChanges.Count == 1 &&
                Pages.SelectedIndex == 6 && sink.MoveCalls > movement && engine.Armed,
                "Simultaneous stick movement and sidebar navigation suppressed the pointer, doubled focus or required reactivation.");
            report.AppendLine("PASS simultaneous stick + held Down: pointer deltas continue while the sidebar moves once to Mapping.");
            foreach (int page in Enumerable.Range(0, Pages.Items.Count))
            {
                SelectPage(page); await Drain();
                movement = sink.MoveCalls;
                for (int frame = 0; frame < 20; frame++) engine.Process(Pad(x: 20000), .008);
                engine.Process(Pad(), .008);
                Check(sink.MoveCalls > movement && engine.Armed && !engine.ConfigurationMode,
                    "Recorded pointer movement stopped while a Relay page was open.");
                report.AppendLine($"PASS {_pageNames[page]}: recorded stick movement remains armed; no page-level configuration lock.");
            }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0,
                "Internal focus navigation also emitted a native keyboard shortcut or pointer click.");
            report.Insert(0, "PASS: simulated controller engine edges reach Relay's actual WPF navigation handler through queued dispatcher commands. Exact sidebar order, endpoint clamping, 100ms/long holds, rapid released edges, one slider value change and pointer coexistence passed. Actual keyboard focus events were counted; no native keyboard, shortcut or click output was emitted. Pointer deltas were recorded without desktop injection. Real controller use and live Narrator speech require manual acceptance.\n\n");
            File.WriteAllText(Path.Combine(output, "controller-navigation.txt"), report.ToString());
        }
        finally
        {
            RemoveHandler(Keyboard.GotKeyboardFocusEvent, changed);
            engine.Reset();
            _app.Profiles = originalProfiles; _settings = originalSettings;
            PopulateControls(); PopulateProfiles();
            SelectPage(originalPage); UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
        }
        await RunNavigationPaneChecks(output);
        await RunNavigationGroupChecks(output);
        await RunNavigationInteractionChecks(output);
        await RunShortcutCaptureChecks(output);
    }

    internal async Task RunNativeSidebarPointerChecks(string output)
    {
        Directory.CreateDirectory(output);
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Native pointer verification requires the isolated owned Relay window.");
        var originalSettings = _settings;
        var originalProfiles = _app.Profiles;
        int originalPage = Pages.SelectedIndex;
        double originalWidth = Width, originalHeight = Height;
        var originalState = WindowState;
        bool originalTopmost = Topmost;
        NativeMethods.GetPhysicalCursorPos(out var originalCursor);
        var handle = new WindowInteropHelper(this).Handle;
        bool nativeLeftOwned = false;
        string? refused = null;
        int nativeMoves = 0, nativeClicks = 0, observed = 0;
        var commands = new List<RelayNavigationCommand>();
        var report = new StringBuilder();
        bool Owned(NativeMethods.Point point) => NativeMethods.GetForegroundWindow() == handle &&
            NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), 2) == handle;
        void Guard()
        {
            if (!NativeMethods.GetPhysicalCursorPos(out var point) || !Owned(point))
                throw new InvalidOperationException("Native pointer guard stopped: foreground or pointer left the owned Relay window.");
            if (!nativeLeftOwned && (NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0)
                throw new InvalidOperationException("Native pointer guard stopped: another device holds the left mouse button.");
        }
        uint SendOwned(NativeMethods.Input[] inputs)
        {
            if (inputs.Any(input => input.Type != 0 || input.Data.Mouse.Flags is not (1 or 2 or 4)))
                throw new InvalidOperationException("Pointer fixture rejected an unexpected keyboard/scroll input.");
            bool cleanup = nativeLeftOwned && inputs.All(input => input.Data.Mouse.Flags == 4);
            if (!cleanup)
            {
                try { Guard(); }
                catch (Exception error) { refused = error.Message; return 0; }
            }
            uint accepted = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
            foreach (var input in inputs.Take((int)accepted))
            {
                if (input.Data.Mouse.Flags == 1) nativeMoves++;
                if (input.Data.Mouse.Flags == 2) { nativeLeftOwned = true; nativeClicks++; }
                if (input.Data.Mouse.Flags == 4) nativeLeftOwned = false;
            }
            return accepted;
        }
        var sink = new WindowsInputSink(SendOwned, keyDown: key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0)
        {
            // Limit each relative movement so an individual native batch cannot jump
            // across a target or out of the owned window under pointer acceleration.
            MovementTransform = (x, y) => (Math.Clamp(x, -4, 4), Math.Clamp(y, -4, 4))
        };
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { Smoothing = 0, ClickHoldMilliseconds = 0, NarratorNavigation = true },
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (NativeMethods.GetForegroundWindow() == handle) NavigateController(command);
                }));
            }
        };
        engine.Process(default(Gamepad), .008);
        Action pointerObserved = () => { observed++; engine.NotifyPointerInteraction(); };
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        void Accepted()
        {
            if (refused != null || sink.Warning != null)
                throw new InvalidOperationException(refused ?? "Native pointer input was refused: " + sink.Warning);
        }
        static Gamepad Pad(Buttons buttons = Buttons.None, short x = 0, short y = 0) => new(buttons, 0, 0, x, y, 0, 0);
        Point Centre(FrameworkElement target)
        {
            target.BringIntoView(); UpdateLayout();
            if (!target.IsVisible || target.ActualWidth < 44 || target.ActualHeight < 44)
                throw new InvalidOperationException("Native pointer target is hidden or too small.");
            var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
            if (!Owned(new NativeMethods.Point { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) }))
                throw new InvalidOperationException("Native pointer target is obscured or outside the owned window.");
            return point;
        }
        async Task MoveTo(Point destination, Buttons held = Buttons.None)
        {
            if (!Owned(new NativeMethods.Point { X = (int)Math.Round(destination.X), Y = (int)Math.Round(destination.Y) }))
                throw new InvalidOperationException("Native movement destination is not in the owned window.");
            bool reached = false;
            for (int frame = 0; frame < 700; frame++)
            {
                Guard();
                NativeMethods.GetPhysicalCursorPos(out var current);
                double dx = destination.X - current.X, dy = destination.Y - current.Y;
                if (Math.Abs(dx) <= 8 && Math.Abs(dy) <= 8) { reached = true; break; }
                short x = Math.Abs(dx) <= 8 ? (short)0 : (short)(Math.Sign(dx) * 32767);
                short y = Math.Abs(dy) <= 8 ? (short)0 : (short)(-Math.Sign(dy) * 32767);
                engine.Process(Pad(held, x, y), .008); Accepted();
                await Task.Delay(8);
            }
            engine.Process(Pad(), .008); await Drain(); Guard(); Accepted();
            if (!reached) throw new InvalidOperationException("The controller engine could not move the pointer to its owned target.");
        }
        async Task Click(RadioButton target, int expectedPage)
        {
            await MoveTo(Centre(target)); Guard();
            NativeMethods.GetPhysicalCursorPos(out var point);
            var local = target.PointFromScreen(new Point(point.X, point.Y));
            if (!new Rect(target.RenderSize).Contains(local))
                throw new InvalidOperationException("Native pointer click is not within the intended sidebar button.");
            int delivered = 0;
            RoutedEventHandler clicked = (_, _) => delivered++;
            target.Click += clicked;
            try
            {
                engine.Process(Pad(Buttons.A), .008);
                engine.Process(Pad(), .008); Accepted();
                // Native mouse messages enter the queue asynchronously. Wait for the
                // real button event, rather than assuming an idle callback delivers it.
                for (int wait = 0; delivered == 0 && wait < 100; wait++)
                { await Task.Delay(10); Guard(); Accepted(); }
                await Drain(); Guard();
                if (delivered != 1 || Pages.SelectedIndex != expectedPage || !engine.Armed || sink.HasHeldInputs)
                    throw new InvalidOperationException($"Native sidebar click did not select page {expectedPage} exactly once safely: clicks {delivered}, page {Pages.SelectedIndex}, armed {engine.Armed}, held {sink.HasHeldInputs}.");
            }
            finally { target.Click -= clicked; }
        }
        try
        {
            _settings = originalSettings with { InterfaceScale = 1, ControllerType = ControllerPreference.Standard, ShowControllerLabels = false };
            WindowState = WindowState.Normal;
            Width = Math.Min(1440, SystemParameters.WorkArea.Width);
            Height = Math.Min(900, SystemParameters.WorkArea.Height);
            Topmost = true; Show(); SelectPage(0); UpdateAccessibleViewport();
            RefreshStatus(new ControllerStatus(true, true, true, 0, "Battery unavailable", default, null, false)); UpdateLayout(); await Drain();
            // A hidden process launch may consume the HWND's first show request.
            // Acquire only this owned window before input begins; later guard failures
            // abort immediately and never reactivate Relay over another application.
            await NativeVerificationFocus.Acquire(this);
            await Drain();
            if (NativeMethods.GetForegroundWindow() != handle)
                throw new InvalidOperationException($"Owned Relay pointer test could not acquire foreground focus: own HWND 0x{handle.ToInt64():X}, foreground 0x{NativeMethods.GetForegroundWindow().ToInt64():X}, visible {IsVisible}, active {IsActive}. No input was sent.");
            var initial = Centre(AccessViewport);
            if (!NativeMethods.SetCursorPos((int)initial.X, (int)initial.Y))
                throw new InvalidOperationException("Could not position the pointer within the owned Relay test window.");
            await Task.Delay(20); await Drain(); Guard();
            PointerInteractionObserved += pointerObserved;

            var navigation = NavigationItems().ToArray();
            var control = navigation.Single(radio => radio.Tag?.ToString() == "0");
            var mapping = navigation.Single(radio => radio.Tag?.ToString() == "6");
            await MoveTo(Centre(control));
            await Click(mapping, 6);
            await MoveTo(Centre(AccessViewport));
            if (observed != 0) throw new InvalidOperationException("Relay's tagged controller movement was mistaken for a separate physical mouse.");
            report.AppendLine("PASS controller pointer: main content to Control, over Mapping, native Mapping click, then back to main content; input remains armed.");

            foreach (var radio in navigation)
            {
                int page = int.Parse(radio.Tag.ToString()!);
                int before = nativeMoves;
                await Click(radio, page);
                await MoveTo(Centre(AccessViewport));
                if (nativeMoves <= before || !engine.Armed)
                    throw new InvalidOperationException("Native pointer stopped after clicking a Relay sidebar page.");
                report.AppendLine($"PASS native sidebar click and continued movement: {_pageNames[page]}.");
            }
            foreach (int page in new[] { 1, 2, 5 })
            {
                SelectPage(page); await Drain();
                int before = nativeMoves;
                await MoveTo(Centre(CurrentNavigation())); await MoveTo(Centre(AccessViewport));
                if (nativeMoves <= before || !engine.Armed)
                    throw new InvalidOperationException("Native pointer stopped on a detailed settings page.");
                report.AppendLine($"PASS native pointer across detailed page and sidebar: {_pageNames[page]}.");
            }

            SelectPage(0); control.Focus(); await Drain(); commands.Clear(); observed = 0;
            NativeMethods.GetPhysicalCursorPos(out var start);
            var destination = new Point(start.X + 32, start.Y);
            if (!Owned(new NativeMethods.Point { X = (int)destination.X, Y = (int)destination.Y }))
                destination.X = start.X - 32;
            int beforeTagged = nativeMoves;
            await MoveTo(destination, Buttons.Down);
            if (!commands.SequenceEqual(new[] { RelayNavigationCommand.Down }) || observed != 0 ||
                !engine.KeyboardNavigationActive || nativeMoves <= beforeTagged || !mapping.IsKeyboardFocused || Pages.SelectedIndex != 6)
                throw new InvalidOperationException($"Tagged stick + D-pad movement cancelled or doubled menu navigation: commands {string.Join(",", commands)}, observed {observed}, navigation {engine.KeyboardNavigationActive}, page {Pages.SelectedIndex}.");
            report.AppendLine("PASS tagged native stick movement + held D-pad: one menu step, continuous pointer movement, no physical-mouse notification.");

            Guard();
            var unmarked = new NativeMethods.Input { Data = new() { Mouse = new() { Flags = 1, X = 1, Extra = UIntPtr.Zero } } };
            if (SendOwned([unmarked]) != 1) throw new InvalidOperationException("Owned unmarked mouse fixture was not delivered.");
            await Task.Delay(20); await Drain(); Guard();
            if (observed < 1 || engine.KeyboardNavigationActive || !engine.Armed)
                throw new InvalidOperationException("A separate unmarked mouse movement did not restore controller pointer clicks without disarming.");
            commands.Clear(); await Click(control, 0);
            if (commands.Count != 0 || nativeClicks < 1)
                throw new InvalidOperationException("Pointer recovery replayed an internal selection command instead of delivering the sidebar click.");
            report.AppendLine("PASS explicitly unmarked simulated mouse movement: pointer click binding restored, followed by a native Control sidebar click.");
            if (sink.HasHeldInputs || nativeLeftOwned) throw new InvalidOperationException("Native pointer fixture retained an owned input.");
            CaptureRelay(output, "pointer-sidebar-tested");
            report.Insert(0, $"PASS: {nativeMoves} native pointer movement batches and {nativeClicks} balanced clicks from the controller engine reached the isolated owned Relay window. Every movement/down was foreground and window guarded; owned mouse-up cleanup is always allowed. Sidebar clicks, movement on all {Pages.Items.Count} pages, tagged controller/mouse coexistence and unmarked simulated-mouse recovery passed. No keyboard input, external app click or Windows preference change was requested. Physical controller and physical mouse hardware still require manual validation.\n\n");
            File.WriteAllText(Path.Combine(output, "pointer-sidebar-input.txt"), report.ToString());
        }
        finally
        {
            PointerInteractionObserved -= pointerObserved;
            engine.Reset(); sink.ReleaseAll();
            if (nativeLeftOwned) SendOwned([new NativeMethods.Input { Data = new() { Mouse = new() { Flags = 4, Extra = (UIntPtr)WindowsInputSink.RelayMouseMarker } } }]);
            if (NativeMethods.GetForegroundWindow() == handle) NativeMethods.SetCursorPos(originalCursor.X, originalCursor.Y);
            _app.Profiles = originalProfiles; _settings = originalSettings;
            Width = originalWidth; Height = originalHeight; WindowState = originalState; Topmost = originalTopmost;
            PopulateControls(); PopulateProfiles(); SelectPage(originalPage); UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
        }
    }

    private sealed class NavigationVerificationSink : IInputSink, IRelayInputCleanup
    {
        internal int MoveCalls;
        internal readonly List<ushort[]> Keys = [];
        internal readonly List<Shortcut> Shortcuts = [];
        internal readonly List<(ControllerMouse.Core.MouseButton Button, bool Down)> MouseChanges = [];
        public void Move(int x, int y) { if (x != 0 || y != 0) MoveCalls++; }
        public void Scroll(int vertical, int horizontal) { }
        public void SetMouseButton(ControllerMouse.Core.MouseButton button, bool down) => MouseChanges.Add((button, down));
        public void Invoke(Shortcut shortcut) => Shortcuts.Add(shortcut);
        public void InvokeKeys(ushort[] keys) => Keys.Add((ushort[])keys.Clone());
        public void ReleaseAll() { }
    }
}
