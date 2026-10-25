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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunNavigationGroupChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null) throw new InvalidOperationException("Group verification requires isolated Relay without desktop output.");
        var settings = _settings; int originalPage = Pages.SelectedIndex;
        double width = Width, height = Height; var state = WindowState;
        bool ports = _portsView; string selected = _selectedInput;
        var sink = new NavigationVerificationSink();
        var commands = new List<RelayNavigationCommand>(); var focusChanges = new List<IInputElement>();
        var report = new StringBuilder(); int explored = 0, simpleCases = 0;
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { NarratorNavigation = true, ClickHoldMilliseconds = 0 },
            RelayWindowActive = true,
            RelayNavigationRequested = command => { commands.Add(command); Dispatcher.BeginInvoke(new Action(() => NavigateController(command))); }
        };
        engine.Process(default(Gamepad), .008);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        KeyboardFocusChangedEventHandler changed = (_, e) => { if (e.NewFocus != null) focusChanges.Add(e.NewFocus); };
        AddHandler(Keyboard.GotKeyboardFocusEvent, changed, true);
        void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text + $"; page {Pages.SelectedIndex}, focus {Keyboard.FocusedElement}, commands {string.Join(", ", commands)}, focus events {focusChanges.Count}.");
        }
        async Task Press(Buttons button, int frames = 1)
        {
            commands.Clear(); focusChanges.Clear();
            for (int frame = 0; frame < frames; frame++) engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008); await Drain();
            Check(commands.Count == 1 && focusChanges.Count <= 1, "One group edge produced missing/duplicate commands or transient focus");
        }
        async Task PhysicalTab()
        {
            var target = (UIElement)Keyboard.FocusedElement;
            var source = PresentationSource.FromVisual(target)!;
            var tab = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            focusChanges.Clear(); target.RaiseEvent(tab); await Drain();
            Check(tab.Handled && focusChanges.Count == 1, "Physical Tab did not use the shared navigation owner exactly once");
        }
        var preview = new ControllerStatus(true, true, true, 0, "Battery unavailable", default, null, false);
        try
        {
            WindowState = WindowState.Normal;
            foreach (var context in new[] { (Width: 1440d, Height: 900d, Scale: 1d), (Width: 640d, Height: 480d, Scale: 2d) })
                foreach (var mode in new[] { (Adaptive: false, Ports: false), (Adaptive: true, Ports: false), (Adaptive: true, Ports: true) })
                {
                    Width = context.Width; Height = context.Height;
                    _settings = settings with
                    {
                        ControllerType = mode.Adaptive ? ControllerPreference.Adaptive : ControllerPreference.Standard,
                        InterfaceScale = context.Scale,
                        VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
                    };
                    SetPortsView(mode.Ports);
                    foreach (int page in new[] { 0, 6, 9 })
                    {
                        SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout(); RefreshNavigationGroups();
                        FrameworkElement[] hosts = page switch
                        {
                            0 => [LiveSurface, ControlTilesHost],
                            6 => [MappingSurface],
                            9 => [HardwareSurface, HardwareInputsHost],
                            _ => throw new InvalidOperationException("Unexpected input page.")
                        };
                        foreach (var host in hosts)
                        {
                            var group = _navigationGroups[host];
                            var members = group.Members.Where(AvailableNavigationElement).ToArray();
                            Check(members.Length > 1 && members.Count(KeyboardNavigation.GetIsTabStop) == 1, "An ordinary group has missing members or multiple Tab entries");
                            Check(group.Entry != null && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(host)), "The group lacks a named entry");
                            foreach (var member in members)
                            {
                                var peer = UIElementAutomationPeer.CreatePeerForElement(member);
                                Check(member.Focusable && peer != null && peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider && !string.IsNullOrWhiteSpace(peer.GetName()),
                                    "Roving focus removed an actual input's named UIA action");
                            }
                            var presenter = FindControl<NavigationGroupPresenter>(host);
                            var groupPeer = presenter == null ? null : UIElementAutomationPeer.CreatePeerForElement(presenter);
                            Check(groupPeer != null && groupPeer.GetAutomationControlType() == AutomationControlType.Group &&
                                !string.IsNullOrWhiteSpace(groupPeer.GetName()), "Input region is not exposed as a real named UIA group");
                            Check(members[0].Focus(), "The first group member cannot receive actual focus"); await Drain();
                            string beforeSelection = _selectedInput;
                            for (int index = 1; index < members.Length; index++)
                            {
                                await Press(Buttons.Down, 125);
                                Check(members[index].IsKeyboardFocused && focusChanges.Count == 1 && _selectedInput == beforeSelection,
                                    "A held Down did not reach exactly the next input while preserving the inspected selection");
                            }
                            await Press(Buttons.Down, 125);
                            Check(members[^1].IsKeyboardFocused && focusChanges.Count == 0, "Group Down did not clamp at its last input");
                            for (int index = members.Length - 2; index >= 0; index--)
                            {
                                await Press(Buttons.Up);
                                Check(members[index].IsKeyboardFocused && focusChanges.Count == 1 && _selectedInput == beforeSelection,
                                    "Up failed to explore every real group input in reverse without changing selection");
                            }
                            await Press(Buttons.Up);
                            Check(members[0].IsKeyboardFocused && focusChanges.Count == 0, "Group Up did not clamp at its first input");
                            Check(members.Count(KeyboardNavigation.GetIsTabStop) == 1 && KeyboardNavigation.GetIsTabStop(members[0]), "The group's single Tab entry did not follow actual focus");
                            await Press(Buttons.RightShoulder);
                            var next = Keyboard.FocusedElement as UIElement;
                            Check(next != null && !group.Contains(next) && focusChanges.Count == 1, "RB did not leave all duplicate members in one focus step");
                            await Press(Buttons.LeftShoulder);
                            Check(members[0].IsKeyboardFocused && focusChanges.Count == 1, "LB did not return to the group's remembered entry");
                            await PhysicalTab();
                            Check(ReferenceEquals(Keyboard.FocusedElement, next), "Physical Tab routing disagrees with RB's next region/control");
                            focusChanges.Clear(); Check(AccessibilitySupport.NavigateTab(this, true), "The backwards Tab route was unavailable"); await Drain();
                            Check(members[0].IsKeyboardFocused && focusChanges.Count == 1, "Backwards Tab routing disagrees with LB's remembered group entry");
                            explored += members.Length;
                            report.AppendLine($"PASS {_pageNames[page]}/{group.Name}, {(mode.Adaptive ? mode.Ports ? "Adaptive ports" : "Adaptive front" : "Xbox")}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: {members.Length} actual named inputs; named UIA group, one Tab entry, complete clamped arrow exploration, RB/LB and Tab/backwards-Tab parity, stable inspected selection.");
                        }
                    }

                    foreach (bool oneControl in new[] { true, false })
                    {
                        _settings = _settings with
                        {
                            VisualAccess = _settings.EffectiveVisualAccess with
                            {
                                OneControlAtATime = oneControl,
                                Complexity = oneControl ? VisualComplexity.Standard : VisualComplexity.Minimal
                            }
                        };
                        foreach (int page in new[] { 0, 6, 9 })
                        {
                            SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout(); RefreshNavigationGroups();
                            var view = _simpleInputViews.Single(candidate => candidate.Host == (page == 0 ? ControlSimpleHost : page == 6 ? MappingSimpleHost : HardwareSimpleHost));
                            Check(view.Host.IsVisible && !view.Surface.IsVisible &&
                                !_navigationGroups[view.Surface].Members.Any(KeyboardNavigation.GetIsTabStop), "Simple input view retained a hidden drawing Tab entry");
                            Check(oneControl ? !view.Picker.IsVisible : view.Picker.IsVisible,
                                "One-input mode or Minimal mode did not expose its intended input chooser");
                            // Mapping always uses the front layout even when Control/Hardware
                            // show ports. Its independent expected neighbours follow that view.
                            bool rear = mode.Ports && page != 6;
                            string firstInput = mode.Adaptive ? rear ? "Y" : "Back" : "LeftTrigger";
                            string secondInput = mode.Adaptive ? rear ? "X" : "Start" : "RightTrigger";
                            SelectPresentedInput(firstInput); UpdateLayout();
                            Check(FocusNavigationTarget(view.Next), "Simple-view Next cannot receive actual focus"); await Drain();
                            await Press(Buttons.A);
                            Check(_selectedInput == secondInput && view.Next.IsKeyboardFocused && focusChanges.Count == 0,
                                "Simple-view A on Next did not inspect the next logical input while preserving button focus");
                            Check(FocusNavigationTarget(view.Previous), "Simple-view Previous cannot receive actual focus"); await Drain();
                            await Press(Buttons.A);
                            Check(_selectedInput == firstInput && view.Previous.IsKeyboardFocused && focusChanges.Count == 0,
                                "Simple-view A on Previous did not reverse its input selection while preserving focus");
                            Check(FocusNavigationTarget(view.Inspect), "The single presented input is not focusable"); await Drain();
                            await Press(Buttons.A);
                            Check(_selectedInput == firstInput && view.Inspect.IsKeyboardFocused &&
                                AutomationProperties.GetItemStatus(view.Inspect).Contains("Selected", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(AutomationProperties.GetName(view.Inspect)), "Inspecting the single input changed its selection or omitted accessible selected state");

                            var visited = new List<UIElement>();
                            AccessibilitySupport.FocusHeading(PageHeading); await Drain();
                            for (int step = 0; step < 400; step++)
                            {
                                await Press(Buttons.RightShoulder);
                                var target = (UIElement)Keyboard.FocusedElement;
                                Check(!visited.Contains(target) && AvailableNavigationElement(target), "Simple-view traversal repeated or hid a target");
                                visited.Add(target);
                                if (ReferenceEquals(target, CurrentNavigation())) break;
                            }
                            Check(CurrentNavigation().IsKeyboardFocused && new UIElement[] { view.Previous, view.Next, view.Inspect }.All(visited.Contains) &&
                                (oneControl || visited.Contains(view.Picker)), "Simple-view RB traversal omitted an actual input action or chooser");
                            Check(!visited.OfType<DependencyObject>().Any(target => NavigationContains(view.Surface, target)), "Simple-view RB traversal entered hidden artwork");
                            AccessibilitySupport.FocusHeading(PageHeading); await Drain();
                            foreach (var target in visited)
                            {
                                await PhysicalTab();
                                Check(ReferenceEquals(Keyboard.FocusedElement, target), "Physical Tab and RB disagree on simple-view page order");
                            }
                            simpleCases++;
                            report.AppendLine($"PASS {_pageNames[page]}, {(oneControl ? "one input at a time" : "Minimal with input chooser")}, {(mode.Adaptive ? mode.Ports ? "Adaptive ports" : "Adaptive front" : "Xbox")}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: Next/Previous A activation changes only inspected input; single input has named selected state; all {visited.Count} ordinary destinations have identical RB/physical-Tab order; hidden drawings skipped.");
                        }
                    }
                }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0, "Group navigation emitted native input");
            File.WriteAllText(Path.Combine(output, "controller-navigation-groups.txt"), $"PASS {explored} group-member checks and {simpleCases} simple-view/page/size/scale cases in real WPF controls through the controller engine, at desktop and 200% small-window layouts. No native input or live Narrator/hardware acceptance is claimed.\n\n" + report);
        }
        finally
        {
            RemoveHandler(Keyboard.GotKeyboardFocusEvent, changed); engine.Reset();
            _settings = settings; Width = width; Height = height; WindowState = state;
            SetPortsView(ports); SelectPresentedInput(selected); SelectPage(originalPage); UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout(); RefreshNavigationGroups(); await Drain();
        }
    }
}
