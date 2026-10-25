using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunSimpleControlChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Simple controls verification requires isolated Relay without desktop output.");
        var settings = _settings;
        var status = _lastPresentationStatus;
        string selected = _selectedInput;
        string? lastInput = _lastInputId;
        string[] activeInputs = _previousActiveInputs.ToArray();
        int page = Pages.SelectedIndex;
        bool ports = _portsView, details = HardwareInputDetails.IsExpanded;
        bool refreshRunning = _refresh.IsEnabled, saveRunning = _save.IsEnabled;
        double width = Width, height = Height;
        var windowState = WindowState;
        var report = new StringBuilder();
        var sink = new NavigationVerificationSink();
        var commands = new List<RelayNavigationCommand>();
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { NarratorNavigation = true, ClickHoldMilliseconds = 0, Smoothing = 0 },
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command);
                Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        var preview = new ControllerStatus(true, true, true, 0, "Preview", default, null, false);
        _refresh.Stop(); _save.Stop(); engine.Process(default(Gamepad), .008);
        Task Drain() => Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle).Task;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Simple controls: " + message);
        }
        async Task Press(Buttons button, int frames = 1)
        {
            commands.Clear();
            for (int frame = 0; frame < frames; frame++)
                engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008);
            await Drain();
            Check(commands.Count == 1, "one held controller selection did not produce exactly one command.");
        }
        void AssertView(bool simple, SimpleInputView view, string expected, string context)
        {
            Check(_selectedInput == expected, context + ": refresh replaced the deliberately selected input.");
            Check(view.Host.Visibility == (simple ? Visibility.Visible : Visibility.Collapsed) &&
                view.Surface.Visibility == (simple ? Visibility.Collapsed : Visibility.Visible),
                context + ": the single-input card/drawing visibility changed.");
            Check(ControlTilesHost.Visibility == (simple ? Visibility.Collapsed : Visibility.Visible) &&
                HardwareInputsHost.Visibility == (simple ? Visibility.Collapsed : Visibility.Visible),
                context + ": refresh exposed the full input list in the single-input view.");
            Check(LastInputPanel.Visibility == (simple ? Visibility.Collapsed : Visibility.Visible),
                context + ": the single-input view exposed a competing Last Input panel or the ordinary view lost it.");
            if (!simple) return;
            Check(view.Icon.Input == expected && InputTileState.GetIsSelected(view.Inspect),
                context + ": the single card lost its selected logical input.");
            var order = SimpleInputOrder(view.Surface);
            string position = $"Input {Array.IndexOf(order, expected) + 1} of {order.Length}";
            Check(view.Position.Text == position, context + ": live input altered the selected card's position/count.");
            var peer = UIElementAutomationPeer.CreatePeerForElement(view.Inspect);
            Check(peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider &&
                !string.IsNullOrWhiteSpace(peer.GetName()) && peer.GetItemStatus().StartsWith("Selected. " + position + ".", StringComparison.Ordinal),
                context + ": the selected card lost its native Invoke, name or state.");
            Check(!NavigationControls((DependencyObject)Pages.SelectedContent).Any(target => NavigationContains(view.Surface, target) ||
                NavigationContains(ControlTilesHost, target) || NavigationContains(HardwareInputsHost, target)),
                context + ": keyboard/controller traversal exposes hidden full-controller inputs.");
        }
        int cases = 0, polls = 0;
        try
        {
            WindowState = WindowState.Normal;
            // A is a pointer click until an intentional D-pad gesture enters
            // Relay's menu navigation. Use that real entry path before testing
            // selection; it must never be mistaken for a missing A command.
            engine.Process(new Gamepad(Buttons.Down, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008); await Drain();
            foreach (string adaptiveOnly in new[] { "X1", "Profile", "USBLeft" })
            {
                _settings = settings with
                {
                    ControllerType = ControllerPreference.Adaptive,
                    VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = true, Complexity = VisualComplexity.Minimal }
                };
                SelectPage(0); SetPortsView(false); RefreshStatus(preview); SelectPresentedInput(adaptiveOnly); await Drain();
                Check(_selectedInput == adaptiveOnly, "an available Adaptive input could not be deliberately inspected.");
                _settings = _settings with { ControllerType = ControllerPreference.Standard };
                RefreshStatus(preview); await Drain();
                var view = _simpleInputViews.Single(candidate => candidate.Host == ControlSimpleHost);
                Check(SimpleInputOrder(view.Surface).Contains(_selectedInput),
                    "an explicit Adaptive-to-standard change retained the unavailable input " + adaptiveOnly + ".");
                Check(_selectedInput == SimpleInputOrder(view.Surface)[0],
                    "an unavailable Adaptive input did not move once to the new layout's first input.");
                AssertView(true, view, _selectedInput, "explicit Adaptive-to-standard layout change");
                report.AppendLine("PASS selected " + adaptiveOnly + ": an explicit hardware layout change chooses an available standard input and valid position.");
            }
            _settings = _settings with { ControllerType = ControllerPreference.Adaptive };
            RefreshStatus(preview); SelectPresentedInput("B");
            _settings = _settings with { ControllerType = ControllerPreference.Standard };
            RefreshStatus(preview); await Drain();
            var retainedView = _simpleInputViews.Single(candidate => candidate.Host == ControlSimpleHost);
            AssertView(true, retainedView, "B", "valid selection across explicit layout change");
            report.AppendLine("PASS selected B: a valid logical selection stays selected across an explicit Adaptive-to-standard layout change.");
            foreach (var size in new[] { (Width: 1440d, Height: 900d, Scale: 1d), (Width: 640d, Height: 480d, Scale: 2d) })
                foreach (var hardware in new[] { (Adaptive: false, Ports: false), (Adaptive: true, Ports: false), (Adaptive: true, Ports: true) })
                    foreach (var mode in new[] {
                (Name: "One input", One: true, Complexity: VisualComplexity.Standard, Scheme: DisplayScheme.RelayDefault),
                (Name: "CVI preset", One: true, Complexity: VisualComplexity.Minimal, Scheme: DisplayScheme.CviSimplified),
                (Name: "Minimal input chooser", One: false, Complexity: VisualComplexity.Minimal, Scheme: DisplayScheme.RelayDefault),
                (Name: "Standard", One: false, Complexity: VisualComplexity.Standard, Scheme: DisplayScheme.RelayDefault) })
                    {
                        Width = size.Width; Height = size.Height;
                        var preset = settings.WithVisualAccessScheme(mode.Scheme);
                        _settings = preset with
                        {
                            ControllerType = hardware.Adaptive ? ControllerPreference.Adaptive : ControllerPreference.Standard,
                            InterfaceScale = size.Scale,
                            AdaptiveAccessories = null,
                            VisualAccess = preset.EffectiveVisualAccess with
                            {
                                OneControlAtATime = mode.One,
                                Complexity = mode.Complexity
                            }
                        };
                        _app.ApplyVisualAccess(_settings); UpdateAccessibleViewport();
                        if (mode.Scheme == DisplayScheme.CviSimplified)
                        {
                            Check(_settings.EffectiveVisualAccess.Spacing == VisualSpacing.Wide &&
                                _settings.EffectiveVisualAccess.Motion == VisualMotion.Off &&
                                _settings.EffectiveVisualAccess.Focus == VisualFocus.VeryStrong,
                                "the CVI preset fixture lost its wide spacing, motion-off or very-strong-focus defaults.");
                            Check(_app.CurrentVisualAccess == _settings.EffectiveVisualAccess &&
                                TryFindResource("ControlMinHeight") is double minimumHeight && minimumHeight == 56 * size.Scale &&
                                TryFindResource("FocusOuterThickness") is Thickness focusThickness && focusThickness.Left == 5,
                                "the actual CVI theme, wide target height or very-strong focus resources were not applied.");
                        }
                        _lastPresentationSettings = null;
                        RefreshStatus(preview); SetPortsView(hardware.Ports);
                        foreach (int targetPage in new[] { 0, 6, 9 })
                        {
                            SelectPage(targetPage); RefreshStatus(preview); await Drain();
                            var view = _simpleInputViews.Single(candidate => candidate.Host ==
                                (targetPage == 0 ? ControlSimpleHost : targetPage == 6 ? MappingSimpleHost : HardwareSimpleHost));
                            bool simple = mode.One || mode.Complexity == VisualComplexity.Minimal;
                            string first = hardware.Adaptive ? hardware.Ports && targetPage != 6 ? "Y" : "Back" : "LeftTrigger";
                            string second = hardware.Adaptive ? hardware.Ports && targetPage != 6 ? "X" : "Start" : "RightTrigger";
                            SelectPresentedInput(first); await Drain();
                            AssertView(simple, view, first, "before polling");
                            // The unchanged status is the production timer's fast path. It
                            // previously reopened the full input row before returning early.
                            for (int poll = 0; poll < 20; poll++)
                            {
                                RefreshStatus(preview); await Drain(); AssertView(simple, view, first, "unchanged status poll"); polls++;
                            }
                            if (simple)
                            {
                                view.Next.BringIntoView(); Check(view.Next.Focus(), "Next cannot receive keyboard focus."); await Drain();
                                await Press(Buttons.A, 125);
                                AssertView(simple, view, second, "held A on Next");
                                Check(view.Next.IsKeyboardFocused, "Next lost its stable focus after selection.");
                                view.Previous.BringIntoView(); Check(view.Previous.Focus(), "Previous cannot receive keyboard focus."); await Drain();
                                await Press(Buttons.A);
                                AssertView(simple, view, first, "A on Previous");
                                Check(view.Previous.IsKeyboardFocused, "Previous lost its stable focus after selection.");
                                view.Inspect.BringIntoView(); Check(view.Inspect.Focus(), "The selected card cannot receive focus."); await Drain();
                                await Press(Buttons.A);
                                Check(view.Inspect.IsKeyboardFocused, "Inspecting the one input rebuilt its focused button.");
                            }
                            foreach (var pad in new[] {
                        new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0), new Gamepad(Buttons.B | Buttons.Y, 0, 0, 0, 0, 0, 0),
                        new Gamepad(Buttons.None, 255, 0, 18000, 8000, 0, 0), default(Gamepad) })
                            {
                                var changedStatus = preview with { State = pad };
                                for (int poll = 0; poll < 3; poll++)
                                {
                                    RefreshStatus(changedStatus); await Drain(); AssertView(simple, view, first, "live input then cached poll"); polls++;
                                    if (simple) Check(view.Inspect.IsKeyboardFocused, "Live input moved focus away from the single card.");
                                }
                            }
                            // A disconnect/reconnect must not resurrect hidden inputs or
                            // change the card the user chose before connection was lost.
                            RefreshStatus(preview with { Connected = false }); await Drain();
                            Check(ControlTilesHost.Visibility == Visibility.Collapsed, "Disconnected inputs remained visible.");
                            RefreshStatus(preview); RefreshStatus(preview); await Drain();
                            AssertView(simple, view, first, "reconnect and unchanged poll");
                            if (mode.Name == "CVI preset" && !hardware.Adaptive && targetPage == 0)
                                CaptureRelay(output, size.Scale == 1 ? "cvi-single-input-stable-desktop" : "cvi-single-input-stable-small-200");
                            cases++;
                            report.AppendLine($"PASS {mode.Name}, {(hardware.Adaptive ? hardware.Ports ? "Adaptive ports" : "Adaptive front" : "Xbox")}, {_pageNames[targetPage]}, {size.Width:0}×{size.Height:0}/{size.Scale:P0}: cached and changing input refreshes preserve layout, chosen input and navigation; {(simple ? "held A Next/Previous/Inspect retain the same button focus" : "ordinary inputs remain visible")}; reconnect keeps chosen input.");
                        }
                    }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                "single-input controller navigation emitted native desktop input.");
            File.WriteAllText(Path.Combine(output, "simple-controls-stability.txt"),
                $"PASS {cases} page/mode/layout/size cases and {polls} unchanged/changing input polls in actual WPF. Selection remains deliberate; hidden full-controller lists stay hidden. No native desktop input or live Narrator/hardware acceptance is claimed.\n\n" + report);
        }
        finally
        {
            engine.Reset(); _settings = settings; _lastPresentationSettings = null;
            _app.ApplyVisualAccess(settings);
            Width = width; Height = height; WindowState = windowState;
            SelectPage(page); SetPortsView(ports); RefreshStatus(status); SelectPresentedInput(selected);
            HardwareInputDetails.IsExpanded = details; _lastInputId = lastInput;
            _previousActiveInputs.Clear(); foreach (string input in activeInputs) _previousActiveInputs.Add(input);
            UpdateAccessibleViewport(); UpdateLayout(); RefreshNavigationGroups(); await Drain();
            if (refreshRunning) _refresh.Start(); if (saveRunning) _save.Start();
        }
    }
}
