using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunNavigationInteractionChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Navigation interaction verification requires isolated Relay preview without desktop output.");
        int originalPage = Pages.SelectedIndex;
        var originalFocus = Keyboard.FocusedElement;
        var sink = new NavigationVerificationSink();
        var commands = new List<RelayNavigationCommand>();
        var report = new StringBuilder();
        var focusTrace = new StringBuilder();
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
        engine.Process(default(Gamepad), .008);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        static Gamepad Pad(Buttons buttons = Buttons.None) => new(buttons, 0, 0, 0, 0, 0, 0);
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                var transcript = Path.Combine(output, "controller-navigation-interactions-failure.txt");
                File.WriteAllText(transcript, report + "\nFocus trace:\n" + focusTrace);
                var recentTrace = string.Join(Environment.NewLine, focusTrace.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).TakeLast(4));
                throw new InvalidOperationException(message + $" Focus: {Keyboard.FocusedElement?.GetType().Name}; commands: {string.Join(", ", commands)}. Transcript: {transcript}.\n{recentTrace}");
            }
        }
        string DescribeFocus()
        {
            if (Keyboard.FocusedElement is not UIElement focus) return "no keyboard focus";
            var owner = FindControl<ComboBox>(focus);
            var chain = new List<string>();
            for (DependencyObject? node = focus; node != null && chain.Count < 16; node = NavigationParent(node))
                chain.Add(node is UIElement element ? $"{node.GetType().Name}(visible={element.IsVisible},enabled={element.IsEnabled},hit={element.IsHitTestVisible},opacity={element.Opacity:0.00})" : node.GetType().Name);
            return $"{focus.GetType().Name} '{AutomationProperties.GetName(focus)}' content '{(focus as ContentControl)?.Content}' " +
                $"available={AvailableNavigationElement(focus)} owner={NavigationWindow(focus)?.Title} " +
                $"popup={owner?.IsDropDownOpen} selected={owner?.SelectedIndex} sliderValue={(focus as Slider)?.Value}; {string.Join(" → ", chain)}";
        }
        async Task Press(Buttons buttons, int frames = 1)
        {
            focusTrace.AppendLine($"Before {buttons}: {DescribeFocus()}");
            for (int frame = 0; frame < frames; frame++) engine.Process(Pad(buttons), .008);
            engine.Process(Pad(), .008);
            await Drain();
            // A setup-page transition reapplies its safe preview and requires neutral.
            engine.Process(Pad(), .008);
            focusTrace.AppendLine($"After {buttons}: {DescribeFocus()}");
        }
        async Task OpenModal(Window window)
        {
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Loaded += (_, _) => loaded.TrySetResult(true);
            // ShowDialog's nested dispatcher continues to deliver these queued
            // controller commands while the owner is disabled by the modal window.
            _ = Dispatcher.BeginInvoke(new Action(() => window.ShowDialog()));
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Drain();
            Check(window.IsVisible, "The owned modal navigation fixture did not open.");
        }
        try
        {
            AccessibilitySupport.FocusHeading(PageHeading);
            await Press(Buttons.Up); // Start the same automatic controller navigation mode used by Relay.

            var setup = new SetupWindow(_settings, _ => null, preview => engine.Settings = preview) { Owner = this };
            try
            {
                await OpenModal(setup); engine.Process(Pad(), .008);
                Button SetupButton(string id) => NavigationInteractionDescendants(setup).OfType<Button>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == id);
                CheckBox SetupChoice(string id) => NavigationInteractionDescendants(setup).OfType<CheckBox>()
                    .Single(check => AutomationProperties.GetAutomationId(check) == "Setup_" + id);
                async Task ReachWithSwitch(UIElement target)
                {
                    for (int step = 0; step < 40 && !target.IsKeyboardFocused; step++) await Press(Buttons.B);
                    Check(target.IsKeyboardFocused, "The next-item switch could not reach a real setup control from its current focus.");
                }
                commands.Clear(); await Press(Buttons.B, 125);
                Check(SetupButton("SetupMethod_Controller").IsKeyboardFocused && commands.SequenceEqual(new[] { RelayNavigationCommand.Next }),
                    "A held B from the initial heading did not reach the first method without a stick or bumper.");
                await Press(Buttons.B);
                Check(SetupButton("SetupMethod_Switches").IsKeyboardFocused, "B skipped the four-switch method.");
                await Press(Buttons.X);
                Check(SetupButton("SetupMethod_Controller").IsKeyboardFocused, "X did not reverse the initial method choices.");
                await Press(Buttons.B); await Press(Buttons.A);
                Check(setup.Choices.InputMethod == SetupInputMethod.Switches, "A did not choose the four-switch layout.");
                var next = NavigationInteractionDescendants(setup).OfType<Button>().Single(button => button.Content?.ToString() == "Next");
                await ReachWithSwitch(next); await Press(Buttons.A);
                await ReachWithSwitch(SetupChoice("reader")); await Press(Buttons.A);
                await ReachWithSwitch(SetupChoice("magnifier")); await Press(Buttons.A);
                Check(setup.Choices.Magnifier, "A did not tick Magnifier in the actual reading page.");
                Check(setup.Choices.ReadAloud && setup.Choices.SimpleControls == true && setup.Choices.InputMethod == SetupInputMethod.Switches,
                    "Reading support and simple presentation could not be combined with switches.");
                await ReachWithSwitch(next); await Press(Buttons.A);
                await ReachWithSwitch(next); await Press(Buttons.A);
                var heading = NavigationInteractionDescendants(setup).OfType<TextBlock>()
                    .Single(text => AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level1);
                Check(AutomationProperties.GetName(heading).StartsWith("Step 4 of 4", StringComparison.Ordinal),
                    "Switch-only selection did not reach the last setup step.");
                var practice = NavigationInteractionDescendants(setup).OfType<Button>().Single(button => button.Content?.ToString() == "Try selecting this button");
                await ReachWithSwitch(practice); await Press(Buttons.A);
                var choices = setup.Choices; commands.Clear(); await Press(Buttons.Y, 125);
                Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Back }) && setup.IsVisible && !setup.Completed &&
                    AutomationProperties.GetName(heading).StartsWith("Step 3 of 4", StringComparison.Ordinal) && setup.Choices == choices && heading.IsKeyboardFocused,
                    "A held Y closed setup, lost its choices, repeated, or failed to restore the preceding heading.");
                await Press(Buttons.Y); await Press(Buttons.Y);
                await ReachWithSwitch(SetupButton("SetupMethod_Pedals")); await Press(Buttons.A);
                await ReachWithSwitch(next); await Press(Buttons.A);
                Check(setup.Choices.InputMethod == SetupInputMethod.Pedals && setup.Choices.Magnifier && setup.Choices.ReadAloud &&
                    !SetupChoice("magnifier").IsEnabled, "Pedal zoom and independent Narrator reading needs did not survive a method change.");
                await Press(Buttons.Y);
                Check(AutomationProperties.GetName(heading).StartsWith("Step 1 of 4", StringComparison.Ordinal) && setup.Choices.InputMethod == SetupInputMethod.Pedals,
                    "Back from pedal reading lost the chosen controls or prior step.");
                report.AppendLine("PASS actual modal setup from opening through four pages using only A/B/X/Y: initial B/X method traversal, A selection, combined switch/Narrator/Magnifier/one-input choices, optional practice, held Y Back, pedal transition and preserved choices/headings. No stick, D-pad or bumper required; temporary tool-free mappings drive the real controller handler.");
            }
            finally
            {
                if (setup.IsVisible) setup.Close(); await Drain();
                engine.Settings = new Settings { NarratorNavigation = true, ClickHoldMilliseconds = 0, Smoothing = 0 };
                engine.Process(Pad(), .008);
            }

            var text = new TextBox { Text = "Alpha beta", MinHeight = 48, Margin = new Thickness(0, 8, 0, 12) };
            AutomationProperties.SetName(text, "Navigation verification text");
            var combo = new ComboBox { MinHeight = 48, Margin = new Thickness(0, 0, 0, 12), SelectedIndex = 0 };
            combo.Items.Add("Alpha"); combo.Items.Add("Beta"); combo.Items.Add("Gamma");
            AutomationProperties.SetName(combo, "Navigation verification choice");
            var panel = new StackPanel { Margin = new Thickness(24) };
            var modalHeading = new TextBlock { Text = "Controller interaction verification", TextWrapping = TextWrapping.Wrap };
            AccessibilitySupport.Heading(modalHeading);
            panel.Children.Add(modalHeading); panel.Children.Add(text); panel.Children.Add(combo);
            var unhandled = new NavigationUnhandledSlider { MinHeight = 48, Margin = new Thickness(0, 0, 0, 12), Maximum = 100, Value = 50 };
            AutomationProperties.SetName(unhandled, "Unhandled-direction verification field");
            panel.Children.Add(unhandled);
            var close = new Button { Content = "Close verification", MinHeight = 48 };
            panel.Children.Add(close);
            var dialog = new Window
            {
                Owner = this,
                Title = "Controller interaction verification",
                Width = 560,
                Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = panel
            };
            AutomationProperties.SetIsDialog(dialog, true);
            AccessibilitySupport.EnableKeyboardAccess(dialog);
            close.Click += (_, _) => dialog.Close();
            try
            {
                await OpenModal(dialog);
                int page = Pages.SelectedIndex;
                AccessibilitySupport.FocusHeading(modalHeading);
                await Press(Buttons.Down);
                Check(text.IsKeyboardFocused, "Down from a modal heading did not enter its first actual field.");
                Check(text.Focus(), "The modal text field cannot receive keyboard focus.");
                text.Select(2, 4);
                commands.Clear();
                await Press(Buttons.Right, 125);
                Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Right }) && text.IsKeyboardFocused &&
                    text.SelectionLength == 0 && text.CaretIndex == 6 && text.Text == "Alpha beta" &&
                    Window.GetWindow((DependencyObject)Keyboard.FocusedElement) == dialog && Pages.SelectedIndex == page,
                    "A held controller Right failed to collapse the text selection once, changed text or escaped its modal field.");
                await Press(Buttons.Left);
                Check(text.CaretIndex == 5 && text.IsKeyboardFocused, "Controller Left did not move the actual text caret one position.");
                await Press(Buttons.RightShoulder);
                Check(combo.IsKeyboardFocused && dialog.IsVisible, "Controller Next escaped the modal dialog instead of reaching its next field.");
                await Press(Buttons.LeftShoulder);
                Check(text.IsKeyboardFocused && dialog.IsVisible, "Controller Previous escaped the modal dialog instead of returning to its text field.");
                report.AppendLine("PASS modal text field: one held Right collapses its real selection once; Left moves the caret one character; RB/LB move between the modal fields without native keys, text changes or sidebar focus.");

                Check(combo.Focus(), "The modal choice field cannot receive keyboard focus.");
                await Press(Buttons.A);
                Check(combo.IsDropDownOpen, "Controller Select did not open the choice popup.");
                await Press(Buttons.Down);
                await Press(Buttons.Down);
                Check(Keyboard.FocusedElement is ComboBoxItem item && item.Content?.ToString() == "Gamma",
                    "Controller popup Down did not reach the actual third item.");
                await Press(Buttons.A);
                Check(combo.SelectedIndex == 2 && !combo.IsDropDownOpen && combo.IsKeyboardFocused && dialog.IsVisible,
                    "Controller Select failed to commit its focused popup item, close the popup, and restore choice-field focus.");
                report.AppendLine("PASS actual ComboBox popup: A opens; Down/Down reaches Gamma; A commits Gamma, closes the popup and restores focus to the choice field.");

                await Press(Buttons.A);
                await Press(Buttons.Up);
                Check(combo.IsDropDownOpen && Keyboard.FocusedElement is ComboBoxItem previous && previous.Content?.ToString() == "Beta",
                    "Controller popup Up did not highlight the preceding actual item.");
                await Press(Buttons.B);
                Check(!combo.IsDropDownOpen && combo.SelectedIndex == 2 && combo.IsKeyboardFocused && dialog.IsVisible && Pages.SelectedIndex == page,
                    "Controller Back closed the modal, committed the unselected popup item, or moved focus behind the modal.");
                report.AppendLine("PASS actual ComboBox cancellation: Up highlights Beta; B closes only the popup, keeps Gamma selected and restores modal choice-field focus.");
                await Press(Buttons.RightShoulder);
                Check(unhandled.IsKeyboardFocused, "Modal RB did not reach the unhandled-arrow fixture.");
                bool fieldHandled = DispatchControlKey(unhandled, Key.Down);
                focusTrace.AppendLine($"Unhandled field precondition: handled={fieldHandled}; {DescribeFocus()}");
                Check(!fieldHandled && unhandled.Value == 50 && unhandled.IsKeyboardFocused,
                    "The unhandled-arrow fixture actually handled Down, changed its value, or changed focus before testing fallback.");
                commands.Clear();
                await Press(Buttons.Down, 125);
                Check(commands.SequenceEqual(new[] { RelayNavigationCommand.Down }) && close.IsKeyboardFocused && unhandled.Value == 50 && dialog.IsVisible,
                    "An unhandled field direction was swallowed instead of moving once to its next actual modal control.");
                report.AppendLine("PASS unhandled field direction: one held Down on the custom slider reaches the next modal action without changing its value, closing the dialog, or swallowing focus navigation.");
            }
            finally { if (dialog.IsVisible) dialog.Close(); await Drain(); }

            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                "Internal modal navigation also emitted native keyboard or pointer output.");
            await RunNativePopupSwitchChecks(output);
            report.Insert(0, "PASS: simulated controller edges reach the production WPF navigation route in actual owned modal windows. Setup Back, popup commit/cancel, TextBox caret/selection, and modal Next/Previous checks passed without SendInput. Live Narrator speech and physical hardware still require manual acceptance.\n\n");
            File.WriteAllText(Path.Combine(output, "controller-navigation-interactions.txt"), report.ToString());
        }
        finally
        {
            engine.Reset(); SelectPage(originalPage);
            if (originalFocus is UIElement { IsVisible: true } element) element.Focus();
            else AccessibilitySupport.FocusHeading(PageHeading);
            await Drain();
        }
    }

    private static IEnumerable<DependencyObject> NavigationInteractionDescendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in NavigationInteractionDescendants(child)) yield return nested;
        }
    }

    // Slider's class command bindings run before OnKeyDown. Reject their commands
    // and explicitly continue the input route, so this really leaves arrows
    // unhandled rather than changing Value or consuming a disabled command.
    private sealed class NavigationUnhandledSlider : Slider
    {
        public NavigationUnhandledSlider()
        {
            AddHandler(CommandManager.PreviewCanExecuteEvent, new CanExecuteRoutedEventHandler((_, e) =>
            {
                if (e.Command == IncreaseSmall || e.Command == DecreaseSmall ||
                    e.Command == IncreaseLarge || e.Command == DecreaseLarge ||
                    e.Command == MinimizeValue || e.Command == MaximizeValue)
                {
                    e.CanExecute = false;
                    e.ContinueRouting = true;
                    e.Handled = true;
                }
            }), true);
        }

        protected override void OnKeyDown(KeyEventArgs e) { e.Handled = false; }
    }
}
